using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Wonjeong.Utils;
using My.Scripts.Global; // GameConstants 사용을 위해 추가됨

namespace My.Scripts.Hardware
{
    /// <summary>
    /// RFID 태그 응답 데이터를 담는 직렬화 클래스.
    /// Why: 브릿지 프로그램에서 보내오는 JSON 문자열을 파싱하기 위함.
    /// </summary>
    [Serializable]
    public class RfidResponse
    {
        public string command;
        public string payload;
    }

    /// <summary>
    /// 외부 JSON 파일과 매핑되는 데이터베이스 구조체.
    /// Why: 하드코딩 없이 외부 파일만 수정하여 카드 UID 목록을 관리하기 위함.
    /// </summary>
    [Serializable]
    public class RfidDatabase
    {
        public List<string> group1; // ㄱ 그룹
        public List<string> group2; // ㄴ 그룹
        public List<string> group3; // ㄷ 그룹
        public List<string> group4; // ㄹ 그룹
        public List<string> group5; // ㅁ 그룹
    }

    /// <summary>
    /// 32비트 브릿지 프로세스와의 Named Pipe 통신을 관리하는 매니저.
    /// </summary>
    public class RfidManager : MonoBehaviour
    {   
        public static RfidManager Instance { get; private set; }
        
        /// <summary> 카드가 인식되었을 때 UID와 카테고리(1~5)를 외부 매니저로 전달하는 이벤트 </summary>
        public Action<string, int> onCardRead;

        private RfidDatabase _rfidDatabase; 

        private string _bridgeExePath = string.Empty;
        private Process _bridgeProcess;
        private NamedPipeClientStream _pipeClient;
        
        private bool _isInitializing = false;
        private bool _isProcessingCommand = false;

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
        }

        private async UniTaskVoid Start()
        {
            LoadRfidDatabase();
            await EnsureBridgeRunningAsync();
        }

        /// <summary>
        /// RFIDValues.json 파일을 읽어와 내부 데이터베이스에 캐싱함.
        /// Why: 매번 인식할 때마다 파일을 읽지 않고 메모리에서 빠르게 UID를 대조하기 위함.
        /// </summary>
        private void LoadRfidDatabase()
        {
            // Why: GameConstants에 추가된 상수를 사용하여 경로 하드코딩을 제거함
            _rfidDatabase = JsonLoader.Load<RfidDatabase>(GameConstants.Path.RfidValue);

            if (_rfidDatabase == null)
            {
                UnityEngine.Debug.LogError($"[RFIDManager] {GameConstants.Path.RfidValue} 파일을 찾을 수 없어 빈 데이터로 초기화합니다.");
                _rfidDatabase = new RfidDatabase 
                {
                    group1 = new List<string>(), group2 = new List<string>(), 
                    group3 = new List<string>(), group4 = new List<string>(), 
                    group5 = new List<string>()
                };
            }
            else
            {
                UnityEngine.Debug.Log("[RFIDManager] RFID UID 데이터베이스 로드 완료.");
            }
        }

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
            }
            catch (Exception e) { UnityEngine.Debug.LogError($"[RFIDManager] 프로세스 시작 실패: {e.Message}"); }
        }

        private async UniTask<bool> ConnectToPipeAsync()
        {
            CleanupPipe();
            _pipeClient = new NamedPipeClientStream(".", "CR100Pipe", PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await _pipeClient.ConnectAsync(3000).AsUniTask();
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RFIDManager] 파이프 연결 실패: {e.Message}");
                CleanupPipe();
                return false;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) TryReadCard().Forget();
        }

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

        private async UniTask SendCommandAsync(string command)
        {
            if (_pipeClient == null || !_pipeClient.IsConnected) return;

            try
            {
                byte[] commandBytes = Encoding.UTF8.GetBytes(command);
                await _pipeClient.WriteAsync(commandBytes, 0, commandBytes.Length).AsUniTask();

                byte[] buffer = new byte[256];
                int bytesRead = await _pipeClient.ReadAsync(buffer, 0, buffer.Length)
                                                .AsUniTask()
                                                .Timeout(TimeSpan.FromSeconds(1.0f));

                if (bytesRead > 0)
                {
                    ProcessResponse(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RFIDManager] 통신 끊김 감지: {e.Message}");
                CleanupPipe(); 
            }
        }

        private void CleanupPipe()
        {
            if (_pipeClient != null)
            {
                _pipeClient.Dispose();
                _pipeClient = null;
            }
        }

        /// <summary>
        /// JSON 데이터베이스와 대조하여 입력된 UID의 소속 그룹을 반환함.
        /// </summary>
        private int GetCategoryFromUid(string uid)
        {
            if (_rfidDatabase == null) return 0;

            if (_rfidDatabase.group1 != null && _rfidDatabase.group1.Contains(uid)) return 1;
            if (_rfidDatabase.group2 != null && _rfidDatabase.group2.Contains(uid)) return 2;
            if (_rfidDatabase.group3 != null && _rfidDatabase.group3.Contains(uid)) return 3;
            if (_rfidDatabase.group4 != null && _rfidDatabase.group4.Contains(uid)) return 4;
            if (_rfidDatabase.group5 != null && _rfidDatabase.group5.Contains(uid)) return 5;

            return 0; 
        }

        private void ProcessResponse(string response)
        {
            try
            {
                RfidResponse res = JsonUtility.FromJson<RfidResponse>(response);
                if (res != null && res.command == "RFID_READ")
                {
                    string uid = res.payload;
                    int category = GetCategoryFromUid(uid);

                    if (category > 0)
                    {
                        UnityEngine.Debug.Log($"<color=green>[RFID] 답변 {category}번 인식 완료 (UID: {uid})</color>");
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"<color=yellow>[RFID] 미등록 카드 태그됨: {uid}</color>");
                    }

                    onCardRead?.Invoke(uid, category);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RFIDManager] 응답 파싱 에러: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                CleanupPipe();
                if (_bridgeProcess != null && !_bridgeProcess.HasExited)
                {
                    _bridgeProcess.Kill();
                    _bridgeProcess.Dispose();
                }
                Instance = null;
            }
        }
    }
}