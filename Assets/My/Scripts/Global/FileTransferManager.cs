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
    /// 서버 PC는 HTTP 서버를 구동하여 파일 저장 및 제공을 수행하고, 클라이언트는 HTTP 요청을 통해 데이터를 동기화함.
    /// </summary>
    public class FileTransferManager : MonoBehaviour
    {
        public static FileTransferManager Instance { get; private set; }

        [Header("Server Settings")]
        public string serverIp;
        public int port;
        public string localSaveRoot = @"C:\UnitySharedPicture";

        private HttpListener _listener;
        private Thread _serverThread;
        private bool _isRunning;

        /// <summary>
        /// 싱글톤 인스턴스를 초기화하고 씬 전환 시 파괴되지 않도록 설정함.
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
            if (loadedSetting != null && !string.IsNullOrEmpty(loadedSetting.serverIP))
            {
                serverIp = loadedSetting.serverIP;
                port = loadedSetting.port;
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
        /// 메인 스레드 블로킹을 방지하기 위해 별도 백그라운드 스레드에서 요청을 수신함.
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

                Debug.Log($"[FileTransferManager] 파일 서버 가동: 포트 {port}");
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
            while (_isRunning && _listener.IsListening)
            {
                try
                {
                    HttpListenerContext context = _listener.GetContext();
                    ProcessRequest(context);
                }
                catch (HttpListenerException) { }
            }
        }

        /// <summary>
        /// 수신된 GET/POST 요청을 분석하여 파일을 전송하거나 저장함.
        /// 클라이언트의 데이터 백업 및 공유 기능을 수행하기 위함.
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
        /// 서버 PC는 로컬 저장 후 클라이언트에 통보하며, 클라이언트는 HTTP POST를 통해 서버로 데이터를 전송함.
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
                    catch { return false; }
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

            // 데이터 수신 결과가 null일 가능성을 체크하여 엔티티 할당 안전성 확보
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
        /// 지정된 경로의 사진 데이터를 가져옴. 로컬 디스크를 우선 조회하여 불필요한 통신을 방지함.
        /// 로컬에 없을 경우에만 HTTP GET 요청을 통해 서버에서 가져오며 블랙스크린 방지를 위해 타임아웃을 적용함.
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
            catch (Exception e) { Debug.LogWarning($"[FileTransferManager] 로컬 읽기 에러: {e.Message}"); }

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
                            return www.downloadHandler.data;
                    }
                    catch { }
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
                        catch { }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 객체 파괴 시 가동 중인 HTTP 서버와 이벤트를 해제함.
        /// </summary>
        private void OnDestroy()
        {
            _isRunning = false;
            if (TcpManager.Instance) TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
            
            if (_listener != null)
            {
                _listener.Stop();
                _listener.Close();
            }
        }
    }
}