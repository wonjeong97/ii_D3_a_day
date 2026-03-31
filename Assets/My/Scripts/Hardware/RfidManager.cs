using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Wonjeong.Utils;

namespace My.Scripts.Hardware
{   
    [Serializable]
    public class RfidSetting
    {
        public List<string> Answer_1;
        public List<string> Answer_2;
        public List<string> Answer_3;
        public List<string> Answer_4;
        public List<string> Answer_5;
    }
    
    public class RfidManager : MonoBehaviour
    {
        public static RfidManager Instance { get; private set; }
        
        // UID가 정상적으로 매핑되었을 때 발생할 이벤트 (1~5번)
        public Action<int> onAnswerReceived;

        private RfidSetting _rfidSetting;
        private string _bridgeExePath;
        private Process _bridgeProcess;
        private NamedPipeClientStream _pipeClient;
        
        private bool _isInitializing = false;
        private bool _isProcessingCommand = false;
        
        private CancellationTokenSource _pollingCts;
        private readonly byte[] _readBuffer = new byte[256];

        /// <summary>
        /// 브릿지 프로그램으로부터 수신되는 JSON 응답을 역직렬화하기 위한 내부 클래스.
        /// </summary>
        [Serializable]
        private class BridgeMessage
        {
            public string command;
            public string payload;
        }

        /// <summary>
        /// 싱글톤 초기화 및 JSON 로드 수행.
        /// Why: 브릿지 프로세스 경로와 카드-응답 매핑 데이터를 앱 실행 시 1회 캐싱하기 위함.
        /// </summary>
        private void Awake() 
        {
            if (!Instance) 
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            
            _bridgeExePath = Path.Combine(Application.streamingAssetsPath, "CR100Bridge", "CR100Bridge.exe");
            
            _rfidSetting = JsonLoader.Load<RfidSetting>("JSON/RfidSetting");
            if (_rfidSetting == null)
            {
                UnityEngine.Debug.LogError("[RFIDManager] RfidSetting.json 로드 실패. RFID 매핑을 수행할 수 없습니다.");
            }
        }

        /// <summary>
        /// 컴포넌트 활성화 시 브릿지 프로세스 가동.
        /// Why: 비동기로 파이프 연결을 안전하게 시도하기 위함.
        /// </summary>
        private async UniTaskVoid Start()
        {
            await EnsureBridgeRunningAsync();
        }

        /// <summary>
        /// 브릿지 프로세스의 생존 여부를 확인하고 필요 시 재기동 및 파이프 재연결 수행.
        /// Why: 외부 프로세스가 예기치 않게 종료되거나 통신이 끊겼을 때 자동으로 복구하기 위함.
        /// </summary>
        private async UniTask<bool> EnsureBridgeRunningAsync()
        {
            if (_isInitializing) return false;

            if (_bridgeProcess == null || _bridgeProcess.HasExited)
            {
                _isInitializing = true;
                try
                {
                    CleanupPipe();
                    StartBridgeProcess();
                    
                    await UniTask.Delay(TimeSpan.FromSeconds(2.0f));
                    return await ConnectToPipeAsync();
                }
                finally { _isInitializing = false; }
            }

            if (_pipeClient == null || !_pipeClient.IsConnected)
            {
                _isInitializing = true;
                try { return await ConnectToPipeAsync(); }
                finally { _isInitializing = false; }
            }

            return true;
        }

        /// <summary>
        /// 숨김 상태로 32비트 브릿지 프로세스 실행.
        /// Why: 64비트 유니티 환경에서 32비트 전용 DLL을 구동하기 위함.
        /// </summary>
        private void StartBridgeProcess()
        {
            try
            {
                if (_bridgeProcess != null && !_bridgeProcess.HasExited) _bridgeProcess.Kill();

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = _bridgeExePath,
                    WorkingDirectory = Path.GetDirectoryName(_bridgeExePath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                _bridgeProcess = Process.Start(startInfo);
                UnityEngine.Debug.Log("[RFIDManager] 브릿지 프로세스 실행됨.");
            }
            catch (Exception e) 
            { 
                UnityEngine.Debug.LogError($"[RFIDManager] 프로세스 시작 실패: {e.Message}"); 
            }
        }

        /// <summary>
        /// 비동기 네임드 파이프 연결 수행.
        /// Why: 메인 스레드 블로킹을 방지하기 위해 비동기 타임아웃(3초)을 적용함.
        /// </summary>
        private async UniTask<bool> ConnectToPipeAsync()
        {
            CleanupPipe();
            _pipeClient = new NamedPipeClientStream(".", "CR100Pipe", PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await _pipeClient.ConnectAsync(3000).AsUniTask();
                UnityEngine.Debug.Log("[RFIDManager] 파이프 연결 성공");
                return true;
            }
            catch
            {
                CleanupPipe();
                return false;
            }
        }

        /// <summary>
        /// 입력 대기 구간 진입 시 자동 폴링 시작.
        /// Why: 타이머 기반으로 지속적인 태그 검사를 수행하기 위함.
        /// </summary>
        public void StartPolling()
        {
            if (_pollingCts != null) return;
            _pollingCts = new CancellationTokenSource();
            PollingRoutineAsync(_pollingCts.Token).Forget();
            UnityEngine.Debug.Log("[RFIDManager] RFID 자동 인식(Polling) 시작");
        }

        /// <summary>
        /// 입력 완료 또는 페이지 이탈 시 자동 폴링 중지.
        /// Why: 불필요한 하드웨어 통신과 리소스 낭비를 막기 위함.
        /// </summary>
        public void StopPolling()
        {
            if (_pollingCts != null)
            {
                _pollingCts.Cancel();
                _pollingCts.Dispose();
                _pollingCts = null;
                UnityEngine.Debug.Log("[RFIDManager] RFID 자동 인식(Polling) 중지");
            }
        }

        /// <summary>
        /// 1초 주기로 브릿지에 카드 읽기 명령을 발송하는 루틴.
        /// </summary>
        private async UniTaskVoid PollingRoutineAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!_isProcessingCommand)
                {
                    TryReadCard().Forget();
                }
                await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: token);
            }
        }

        /// <summary>
        /// 디버그용 수동 키보드 입력.
        /// </summary>
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) TryReadCard().Forget();
        }

        /// <summary>
        /// 중복 명령을 방지하며 읽기 명령을 파이프에 비동기로 전달함.
        /// </summary>
        public async UniTaskVoid TryReadCard()
        {
            if (_isProcessingCommand) return;

            _isProcessingCommand = true;
            try
            {
                bool ready = await EnsureBridgeRunningAsync();
                if (ready) await SendCommandAsync("READ_CARD");
            }
            finally { _isProcessingCommand = false; }
        }

        /// <summary>
        /// 파이프를 통해 바이트 데이터를 전송하고 응답을 수신함.
        /// Why: 1초 이상의 지연 발생 시 타임아웃 처리하여 메인 루프 멈춤을 방지함.
        /// </summary>
        private async UniTask SendCommandAsync(string command)
        {
            if (_pipeClient == null || !_pipeClient.IsConnected) return;

            try
            {
                byte[] commandBytes = Encoding.UTF8.GetBytes(command);
                await _pipeClient.WriteAsync(commandBytes, 0, commandBytes.Length).AsUniTask();

                // Why: 기존의 잦은 new byte[256] 할당을 피하고, 클래스 멤버 배열(_readBuffer)을 재사용하여 GC 스파이크를 최적화함.
                int bytesRead = await _pipeClient.ReadAsync(_readBuffer, 0, _readBuffer.Length)
                    .AsUniTask()
                    .Timeout(TimeSpan.FromSeconds(1.0f));

                if (bytesRead > 0)
                {
                    string response = Encoding.UTF8.GetString(_readBuffer, 0, bytesRead);
                    ProcessResponse(response);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RFIDManager] 통신 끊김 감지: {e.Message}");
                CleanupPipe(); 
            }
        }

        /// <summary>
        /// 파이프 객체를 메모리에서 해제함.
        /// </summary>
        private void CleanupPipe()
        {
            if (_pipeClient != null)
            {
                _pipeClient.Dispose();
                _pipeClient = null;
            }
        }

        /// <summary>
        /// 수신된 문자열을 파싱하고 매핑 이벤트를 발생시킴.
        /// Why: 브릿지가 보내는 JSON 응답을 역직렬화하여 정확한 UID(payload)만 추출하기 위함.
        /// </summary>
        private void ProcessResponse(string response)
        {
            if (string.IsNullOrEmpty(response)) return;

            try
            {
                BridgeMessage msg = JsonUtility.FromJson<BridgeMessage>(response);
                
                if (msg != null && msg.command == "RFID_READ")
                {
                    string uid = msg.payload.Trim();
                    ProcessMatchedUid(uid);
                    return;
                }
            }
            catch
            {
                // JSON 파싱 실패 시 아래 Fallback 로직으로 넘어감
            }

            // 구버전 포맷 (예: RFID_READ:FB8A1848) 수신 시 대응하는 Fallback 로직
            if (response.Contains("RFID_READ"))
            {
                string[] parts = response.Split(':');
                if (parts.Length >= 2)
                {
                    string uid = parts[parts.Length - 1].Replace("\"", "").Replace("}", "").Trim();
                    ProcessMatchedUid(uid);
                }
            }
        }

        /// <summary>
        /// 추출된 UID를 검증하고 이벤트를 발생시킴.
        /// Why: 중복 코드를 방지하고 공통된 처리 및 로그 출력을 수행하기 위함.
        /// </summary>
        private void ProcessMatchedUid(string uid)
        {
            int answerIndex = GetAnswerIndexFromUid(uid);
            
            if (answerIndex > 0)
            {
                UnityEngine.Debug.Log($"<color=green>[RFID] 인식 완료: UID({uid}) -> Answer_{answerIndex}</color>");
                if (onAnswerReceived != null) 
                {
                    onAnswerReceived.Invoke(answerIndex);
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[RFID] 등록되지 않은 UID 입니다: {uid}");
            }
        }

        /// <summary>
        /// 설정된 JSON 배열을 순회하여 UID에 해당하는 답변 인덱스를 찾음.
        /// Why: 읽어들인 태그가 1~5번 중 어느 답변에 해당하는지 식별하기 위함.
        /// </summary>
        private int GetAnswerIndexFromUid(string uid)
        {
            if (_rfidSetting == null) return -1;
            
            if (_rfidSetting.Answer_1 != null && _rfidSetting.Answer_1.Contains(uid)) return 1;
            if (_rfidSetting.Answer_2 != null && _rfidSetting.Answer_2.Contains(uid)) return 2;
            if (_rfidSetting.Answer_3 != null && _rfidSetting.Answer_3.Contains(uid)) return 3;
            if (_rfidSetting.Answer_4 != null && _rfidSetting.Answer_4.Contains(uid)) return 4;
            if (_rfidSetting.Answer_5 != null && _rfidSetting.Answer_5.Contains(uid)) return 5;
            
            return -1;
        }

        /// <summary>
        /// 앱 종료 또는 씬 해제 시 정리 작업 수행.
        /// Why: 백그라운드 프로세스가 좀비 상태로 남는 것을 방지함.
        /// </summary>
        private void OnDestroy()
        {
            StopPolling();
            CleanupPipe();
            if (_bridgeProcess != null && !_bridgeProcess.HasExited)
            {
                _bridgeProcess.Kill();
                _bridgeProcess.Dispose();
            }
        }
    }
}