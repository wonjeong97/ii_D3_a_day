using System;
using System.IO;
using System.Net;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using My.Scripts.Network;
using Wonjeong.Utils;

namespace My.Scripts.Global
{
    /// <summary>
    /// 양쪽 PC 간의 이미지 및 영상 파일 전송을 담당하는 매니저.
    /// 설정 파일에 명시된 독립적인 HTTP 포트를 사용하여 TCP 소켓과의 충돌을 방지함.
    /// </summary>
    public class FileTransferManager : MonoBehaviour
    {
        public static FileTransferManager Instance { get; private set; }

        [Header("Server Settings")]
        public string serverIp;
        public int port;
        public string localSaveRoot;

        private HttpListener _listener;
        private Thread _serverThread;
        private bool _isRunning;

        /// <summary>
        /// 싱글톤 인스턴스를 초기화함.
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
            }
        }

        /// <summary>
        /// 네트워크 설정 파일을 로드하고 서버 PC인 경우 HTTP 서버를 가동함.
        /// </summary>
        private void Start()
        {
            TcpSetting loadedSetting = JsonLoader.Load<TcpSetting>(GameConstants.Path.TcpSetting);
            
            if (loadedSetting != null)
            {
                if (!string.IsNullOrWhiteSpace(loadedSetting.serverIP)) 
                {
                    serverIp = loadedSetting.serverIP;
                }
                
                if (loadedSetting.httpPort > 0 && loadedSetting.httpPort <= 65535) 
                {
                    port = loadedSetting.httpPort; 
                }
                
                if (!string.IsNullOrWhiteSpace(loadedSetting.localSaveRoot))
                {
                    localSaveRoot = loadedSetting.localSaveRoot;
                }
            }
            
            if (string.IsNullOrWhiteSpace(localSaveRoot))
            {
                localSaveRoot = @"C:\UnitySharedPicture";
            }
            
            if (string.IsNullOrWhiteSpace(serverIp) || port <= 0)
            {
                Debug.LogError("[FileTransferManager] 유효한 serverIp/httpPort 설정이 없어 비활성화합니다.");
                enabled = false;
                return;
            }

            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
                
                if (TcpManager.Instance.IsServer)
                {
                    StartHttpServer();
                }
            }
        }

        /// <summary>
        /// .NET HttpListener를 사용하여 경량 파일 서버를 시작함.
        /// </summary>
        private void StartHttpServer()
        {
            try
            {
                if (!Directory.Exists(localSaveRoot)) Directory.CreateDirectory(localSaveRoot);

                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://*:{port}/");
                _listener.Start();

                _isRunning = true;
                _serverThread = new Thread(ServerListenRoutine);
                _serverThread.IsBackground = true;
                _serverThread.Start();

                Debug.Log($"[FileTransferManager] 파일 전송용 HTTP 서버 가동: 포트 {port}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileTransferManager] 서버 실행 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 스레드 루프를 통해 들어오는 HTTP 요청을 지속적으로 수신함.
        /// </summary>
        private void ServerListenRoutine()
        {
            while (_isRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    HttpListenerContext context = _listener.GetContext();
                    ProcessRequest(context);
                }
                catch (HttpListenerException) { }
                catch (Exception e)
                {
                    Debug.LogWarning($"[FileTransferManager] 리스너 루틴 예외: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 수신된 GET/POST 요청을 분석하여 파일을 전송하거나 저장함.
        /// </summary>
        /// <param name="context">HTTP 요청 컨텍스트.</param>
        private void ProcessRequest(HttpListenerContext context)
        {
            HttpListenerRequest req = context.Request;
            HttpListenerResponse res = context.Response;

            try
            {
                string relativePath = req.Url.LocalPath.TrimStart('/');
                string fullFilePath = Path.Combine(localSaveRoot, relativePath.Replace('/', '\\'));

                if (req.HttpMethod == "POST")
                {
                    string dir = Path.GetDirectoryName(fullFilePath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    using (FileStream fs = File.OpenWrite(fullFilePath))
                    {
                        req.InputStream.CopyTo(fs);
                    }
                    res.StatusCode = 200;
                }
                else if (req.HttpMethod == "GET")
                {
                    if (File.Exists(fullFilePath))
                    {
                        byte[] fileBytes = File.ReadAllBytes(fullFilePath);
                        
                        res.StatusCode = 200;
                        res.ContentType = "image/png";
                        res.ContentLength64 = fileBytes.Length;
                        res.OutputStream.Write(fileBytes, 0, fileBytes.Length);
                    }
                    else
                    {
                        res.StatusCode = 404;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileTransferManager] 요청 처리 에러: {e.Message}");
                res.StatusCode = 500;
            }
            finally
            {
                res.Close();
            }
        }

        /// <summary>
        /// 촬영된 사진을 로컬에 저장하고 역할에 따라 서버로 전송함.
        /// </summary>
        /// <param name="imageBytes">이미지 바이트 데이터.</param>
        /// <param name="relativePath">저장될 상대 경로.</param>
        /// <returns>전송 및 저장 성공 여부.</returns>
        public async UniTask<bool> UploadPhotoAsync(byte[] imageBytes, string relativePath)
        {
            if (imageBytes == null) return false;

            try
            {
                string fullPath = Path.Combine(localSaveRoot, relativePath.Replace('/', '\\'));
                string dir = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                await File.WriteAllBytesAsync(fullPath, imageBytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileTransferManager] 로컬 저장 실패: {e.Message}");
                return false;
            }

            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

            if (isServer)
            {
                if (TcpManager.Instance)
                {
                    TcpManager.Instance.SendMessageToTarget("NOTIFY_PHOTO_READY", relativePath);
                }
                return true; 
            }
            else
            {
                string url = $"http://{serverIp}:{port}/{relativePath}";
                using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
                {
                    www.uploadHandler = new UploadHandlerRaw(imageBytes);
                    www.downloadHandler = new DownloadHandlerBuffer();
                    www.SetRequestHeader("Content-Type", "image/png");
                    www.timeout = 10;

                    try
                    {
                        await www.SendWebRequest();
                        return www.result == UnityWebRequest.Result.Success;
                    }
                    catch (Exception e)
                    { 
                        Debug.LogError($"[FileTransferManager] 업로드 요청 실패: {e.Message}");
                        return false; 
                    }
                }
            }
        }

        /// <summary>
        /// 사진 준비 완료 통보를 수신하면 비동기로 다운로드를 시작함.
        /// </summary>
        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == "NOTIFY_PHOTO_READY")
            {
                if (TcpManager.Instance && !TcpManager.Instance.IsServer)
                {
                    DownloadAndSavePhotoAsync(msg.payload).Forget();
                }
            }
        }

        /// <summary>
        /// 원격 서버에서 사진을 다운로드하여 로컬 경로에 저장함.
        /// 다운로드된 데이터가 유효한지 검증하여 Null 참조 에러를 방지함.
        /// </summary>
        private async UniTaskVoid DownloadAndSavePhotoAsync(string relativePath)
        {
            string fullPath = Path.Combine(localSaveRoot, relativePath.Replace('/', '\\'));
            if (File.Exists(fullPath)) return;

            byte[] data = await DownloadPhotoAsync(relativePath);
            
            if (data != null && data.Length > 0)
            {
                try
                {
                    string dir = Path.GetDirectoryName(fullPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    await File.WriteAllBytesAsync(fullPath, data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[FileTransferManager] 파일 쓰기 중 오류: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 지정된 경로의 사진 데이터를 가져옴.
        /// </summary>
        /// <param name="relativePath">사진 상대 경로.</param>
        /// <returns>이미지 바이트 배열.</returns>
        public async UniTask<byte[]> DownloadPhotoAsync(string relativePath)
        {
            try
            {
                string fullPath = Path.Combine(localSaveRoot, relativePath.Replace('/', '\\'));
                if (File.Exists(fullPath)) 
                {
                    return await File.ReadAllBytesAsync(fullPath);
                }
            }
            catch (Exception e) 
            { 
                Debug.LogWarning($"[FileTransferManager] 로컬 읽기 에러: {e.Message}"); 
            }

            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

            if (!isServer)
            {
                string url = $"http://{serverIp}:{port}/{relativePath}";
                using (UnityWebRequest www = UnityWebRequest.Get(url))
                {
                    www.timeout = 5; 
                    try
                    {
                        await www.SendWebRequest();
                        if (www.result == UnityWebRequest.Result.Success) 
                        {
                            return www.downloadHandler.data;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[FileTransferManager] 사진 다운로드 실패: {e.Message}");
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// 특정 유저의 모든 질문 답변 사진을 동기화함.
        /// 영상 인코딩 전 누락된 사진이 없는지 교차 검증하기 위함.
        /// </summary>
        /// <param name="totalQuestions">총 질문 개수.</param>
        /// <param name="userId">유저 고유 식별자.</param>
        /// <returns>동기화 프로세스 완료 여부.</returns>
        public async UniTask<bool> SyncAllPhotosAsync(int totalQuestions, string userId)
        {
            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

            string targetRole = isServer ? "Right" : "Left";
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");

            for (int i = 1; i <= totalQuestions; i++)
            {
                string relativePath = $"{dateStr}/{userId}/{targetRole}/{userId}_{targetRole}_Q{i}.png";
                string fullPath = Path.Combine(localSaveRoot, relativePath.Replace('/', '\\'));
                
                if (!File.Exists(fullPath) && !isServer)
                {
                    byte[] data = await DownloadPhotoAsync(relativePath);
                    if (data != null && data.Length > 0)
                    {
                        try
                        {
                            string dir = Path.GetDirectoryName(fullPath);
                            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                            await File.WriteAllBytesAsync(fullPath, data);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[FileTransferManager] 누락 사진 동기화 저장 실패: {e.Message}");
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 유니티 에디터/앱 강제 종료 시 포트 점유를 확실히 해제함.
        /// </summary>
        private void OnApplicationQuit()
        {
            CleanupServer();
        }

        /// <summary>
        /// 객체 파괴 시 가동 중인 HTTP 서버와 이벤트를 해제함.
        /// </summary>
        private void OnDestroy()
        {
            CleanupServer();
        }

        /// <summary>
        /// 실행 중인 파일 서버 스레드 및 소켓을 안전하게 닫고 리소스를 반환함.
        /// 에디터 재실행 시 포트가 묶여 발생하는 에러를 방지하기 위함.
        /// </summary>
        private void CleanupServer()
        {
            _isRunning = false;
            if (TcpManager.Instance) TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
            
            try
            {
                if (_listener != null)
                {
                    if (_listener.IsListening) _listener.Stop();
                    _listener.Close();
                    _listener = null;
                }

                if (_serverThread != null && _serverThread.IsAlive)
                {
                    _serverThread.Abort();
                    _serverThread = null;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FileTransferManager] 서버 정리 중 예외 발생: {e.Message}");
            }
        }
    }
}