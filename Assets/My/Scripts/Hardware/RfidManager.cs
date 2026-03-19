using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace My.Scripts.Hardware
{
    /// <summary>
    /// 32비트 브릿지 프로세스와의 Named Pipe 통신을 관리하는 매니저.
    /// Why: 64비트 유니티 환경에서 32비트 전용 RFID DLL을 사용하기 위한 중계 계층 역할을 수행함.
    /// </summary>
    public class RfidManager : MonoBehaviour
    {
        private string _bridgeExePath;
        private Process _bridgeProcess;
        private NamedPipeClientStream _pipeClient;
        
        private bool _isInitializing = false;
        private bool _isProcessingCommand = false;

        private void Awake() 
        {
            // 경로: StreamingAssets/CR100Bridge/CR100Bridge.exe
            _bridgeExePath = Path.Combine(Application.streamingAssetsPath, "CR100Bridge", "CR100Bridge.exe");
        }

        private async UniTaskVoid Start()
        {
            await EnsureBridgeRunningAsync();
        }

        /// <summary>
        /// 브릿지 프로세스의 생존 여부를 확인하고 필요 시 재기동 및 파이프 재연결을 수행함.
        /// Why: _isInitializing 플래그로 1초 주기 호출 시 프로세스가 중복 실행되는 현상을 방지함.
        /// </summary>
        private async UniTask<bool> EnsureBridgeRunningAsync()
        {
            if (_isInitializing) return false;

            // 1. 프로세스 생존 확인
            if (_bridgeProcess == null || _bridgeProcess.HasExited)
            {
                _isInitializing = true;
                try
                {
                    CleanupPipe();
                    StartBridgeProcess();
                    
                    // Why: 브릿지가 실행되어 장치 초기화를 완료할 때까지의 대기 시간 확보
                    await UniTask.Delay(TimeSpan.FromSeconds(2.0f));
                    return await ConnectToPipeAsync();
                }
                finally { _isInitializing = false; }
            }

            // 2. 파이프 연결 확인 (프로세스는 살아있으나 통신만 끊긴 경우)
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
                    // Why: DLL을 찾지 못해 발생하는 크래시 방지를 위해 실행 파일 폴더를 작업 디렉토리로 지정
                    WorkingDirectory = Path.GetDirectoryName(_bridgeExePath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                _bridgeProcess = Process.Start(startInfo);
                UnityEngine.Debug.Log("[RFIDManager] 브릿지 프로세스 실행됨.");
            }
            catch (Exception e) { UnityEngine.Debug.LogError($"[RFIDManager] 프로세스 시작 실패: {e.Message}"); }
        }

        private async UniTask<bool> ConnectToPipeAsync()
        {
            CleanupPipe();
            _pipeClient = new NamedPipeClientStream(".", "CR100Pipe", PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                // Why: 무한 대기를 방지하기 위해 3초의 연결 타임아웃 설정
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

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) TryReadCard().Forget();
        }

        /// <summary>
        /// 카드 읽기 명령을 실행함.
        /// Why: 이전 명령이 종료되지 않았다면 실행을 건너뛰어 동기화를 유지함.
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

        private async UniTask SendCommandAsync(string command)
        {
            if (_pipeClient == null || !_pipeClient.IsConnected) return;

            try
            {
                byte[] commandBytes = Encoding.UTF8.GetBytes(command);
                await _pipeClient.WriteAsync(commandBytes, 0, commandBytes.Length).AsUniTask();

                byte[] buffer = new byte[256];
                // Why: 응답 지연 시 유니티 메인 루프가 멈추는 것을 방지하기 위해 1초 타임아웃 적용
                int bytesRead = await _pipeClient.ReadAsync(buffer, 0, buffer.Length)
                                                .AsUniTask()
                                                .Timeout(TimeSpan.FromSeconds(1.0f));

                if (bytesRead > 0)
                {
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    ProcessResponse(response);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RFIDManager] 통신 끊김 감지: {e.Message}");
                CleanupPipe(); // 오류 시 파이프를 정리하여 다음 호출 때 재연결 유도
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

        private void ProcessResponse(string response)
        {
            if (response.Contains("RFID_READ"))
            {
                UnityEngine.Debug.Log($"<color=green>[RFID] 읽기 성공: {response}</color>");
            }
        }

        private void OnDestroy()
        {
            CleanupPipe();
            if (_bridgeProcess != null && !_bridgeProcess.HasExited)
            {
                _bridgeProcess.Kill();
                _bridgeProcess.Dispose();
            }
        }
    }
}