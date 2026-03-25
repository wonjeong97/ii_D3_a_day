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
    public class FileTransferManager : MonoBehaviour
    {
        public static FileTransferManager Instance { get; private set; }

        [Header("Server Settings")]
        public string serverIp = "127.0.0.1";
        public int port = 8080;
        public string localSaveRoot = @"C:\UnitySharedPicture";

        private HttpListener _listener;
        private Thread _serverThread;
        private bool _isRunning;

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

        private void Start()
        {
            TcpSetting loadedSetting = JsonLoader.Load<TcpSetting>(GameConstants.Path.TcpSetting);
            if (loadedSetting != null && !string.IsNullOrEmpty(loadedSetting.serverIP))
            {
                serverIp = loadedSetting.serverIP;
                UnityEngine.Debug.Log($"[FileTransferManager] TcpSetting IP 로드 성공: {serverIp}");
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

                UnityEngine.Debug.Log($"[FileTransferManager] 파일 서버 실행됨 (포트: {port})");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[FileTransferManager] 서버 실행 실패: {e.Message}");
            }
        }

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
                        
                        // Why: 데이터를 쓰기 전에 상태를 먼저 설정하여 오류를 방지함
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
                UnityEngine.Debug.LogError($"[FileTransferManager] 요청 처리 중 에러: {e.Message}");
                res.StatusCode = 500;
            }
            finally
            {
                res.Close();
            }
        }

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
                UnityEngine.Debug.LogError($"[FileTransferManager] 로컬 저장 실패: {e.Message}");
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

        private async UniTaskVoid DownloadAndSavePhotoAsync(string relativePath)
        {
            string fullPath = Path.Combine(localSaveRoot, relativePath.Replace('/', '\\'));
            if (File.Exists(fullPath)) return;

            byte[] data = await DownloadPhotoAsync(relativePath);
            if (data != null && data.Length > 0)
            {
                string dir = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                await File.WriteAllBytesAsync(fullPath, data);
            }
        }

        public async UniTask<byte[]> DownloadPhotoAsync(string relativePath)
        {
            // Why: 무조건 로컬 디스크를 먼저 확인하여 무의미한 HTTP 통신과 블랙스크린을 방지합니다.
            try
            {
                string fullPath = Path.Combine(localSaveRoot, relativePath.Replace('/', '\\'));
                if (File.Exists(fullPath)) 
                {
                    return await File.ReadAllBytesAsync(fullPath);
                }
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning($"[FileTransferManager] 로컬 읽기 에러: {e.Message}"); }

            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

            if (!isServer)
            {
                string url = $"http://{serverIp}:{port}/{relativePath}";
                using (UnityWebRequest www = UnityWebRequest.Get(url))
                {
                    www.timeout = 5; // 무한 대기(블랙스크린) 방지용 타임아웃 5초
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

        public async UniTask<bool> SyncAllPhotosAsync(int totalQuestions, string userId)
        {
            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

            UnityEngine.Debug.Log("[FileTransferManager] 누락된 사진 검증 및 고속 동기화 시작...");

            string targetRole = isServer ? "Client" : "Server";

            for (int i = 1; i <= totalQuestions; i++)
            {
                string relativePath = $"{userId}/{targetRole}/0_{targetRole}_Q{i}.png";
                string fullPath = Path.Combine(localSaveRoot, relativePath.Replace('/', '\\'));
                
                // Why: 기존 5초 대기 딜레이를 삭제하고, 없으면 딱 한 번만 빠르게 시도한 뒤 바로 넘어가도록 최적화
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