using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wonjeong.Utils;

namespace My.Scripts.Network
{
    [Serializable]
    public class TcpSetting
    {
        public bool isServer;
        public string serverIP;
        public int port;
    }

    [Serializable]
    public class TcpMessage
    {
        public string command; 
        public string payload; 
    }

    /// <summary>
    /// TCP 통신을 관리하는 싱글톤 매니저.
    /// 3초 주기의 하트비트로 연결을 체크하며, 단절 시 백그라운드 재접속을 시도하고 10회 실패 시 타이틀로 복귀함.
    /// PlaySolo 모드를 통해 에디터에서 단독 테스트가 가능함.
    /// </summary>
    public class TcpManager : MonoBehaviour
    {
        public static TcpManager Instance { get; private set; }

        public Action<TcpMessage> onMessageReceived;

        [Header("Debug Options")]
        [Tooltip("체크 시 실제 통신 없이 에코(Echo) 방식으로 단독 테스트를 진행합니다.")]
        public bool playSolo = false;

        private TcpSetting _tcpSetting;

        private TcpListener _serverListener;
        private Thread _serverThread;

        private TcpClient _connectedClient;
        private NetworkStream _networkStream;
        private Thread _receiveThread;
        private Thread _connectThread;

        private ConcurrentQueue<TcpMessage> _messageQueue = new ConcurrentQueue<TcpMessage>();
        
        private volatile bool _isRunning;
        private volatile bool _isConnectionActive;
        
        private bool _needsToReturnToTitle;
        private int _failedConnectionCount;
        private Coroutine _heartbeatCoroutine;
        
        public bool IsServer 
        {
            get 
            {
                if (playSolo) return true; 
                return _tcpSetting != null && _tcpSetting.isServer;
            }
        }

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeNetwork();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeNetwork()
        {
            if (playSolo)
            {
                Debug.Log("[TcpManager] PlaySolo 모드 활성화. 실제 네트워크 초기화 및 하트비트를 생략합니다.");
                _isRunning = true;
                _isConnectionActive = true; 
                return;
            }

            TcpSetting loadedSetting = JsonLoader.Load<TcpSetting>(GameConstants.Path.TcpSetting);

            if (loadedSetting != null)
            {
                _tcpSetting = loadedSetting;
                _isRunning = true;

                if (_tcpSetting.isServer) StartServer();
                else StartClient();
                
                _heartbeatCoroutine = StartCoroutine(ConnectionMonitorRoutine());
            }
            else
            {
                Debug.LogError("[TcpManager] JSON/TcpSetting 로드 실패. 통신을 시작할 수 없습니다.");
            }
        }

        private void Update()
        {
            if (_needsToReturnToTitle && !playSolo)
            {
                _needsToReturnToTitle = false;
                
                if (SceneManager.GetActiveScene().name != GameConstants.Scene.Title)
                {
                    Debug.LogError("[TcpManager] 10회 연속 연결 실패. 타이틀로 강제 복귀합니다.");
                    
                    if (GameManager.Instance)
                    {
                        GameManager.Instance.ReturnToTitle();
                    }
                    else
                    {
                        SceneManager.LoadScene(GameConstants.Scene.Title); 
                    }
                }
                else
                {
                    Debug.LogWarning("[TcpManager] 통신 대기 상태 유지 중 (이미 타이틀 씬이므로 재로드 생략)");
                }
            }

            while (_messageQueue.TryDequeue(out TcpMessage message))
            {
                if (message != null)
                {
                    if (message.command == "HEARTBEAT") continue;

                    if (onMessageReceived != null)
                    {
                        onMessageReceived.Invoke(message);
                    }
                }
            }
        }

        /// <summary>
        /// 3초마다 통신 상태를 모니터링하고 연결 실패 카운트를 누적하는 코루틴.
        /// </summary>
        private IEnumerator ConnectionMonitorRoutine()
        {
            while (_isRunning)
            {
                yield return CoroutineData.GetWaitForSeconds(3.0f);
                
                if (_isConnectionActive)
                {
                    _failedConnectionCount = 0;
                    SendMessageToTarget("HEARTBEAT", "");
                }
                else
                {
                    _failedConnectionCount++;
                    Debug.LogWarning($"[TcpManager] 연결 대기 중... ({_failedConnectionCount}/10)");

                    if (_failedConnectionCount >= 10)
                    {
                        _failedConnectionCount = 0; 
                        _needsToReturnToTitle = true;
                    }
                }
            }
        }

        private void StartServer()
        {
            _serverThread = new Thread(ServerListenRoutine);
            _serverThread.IsBackground = true;
            _serverThread.Start();
        }

        private void ServerListenRoutine()
        {
            try
            {
                _serverListener = new TcpListener(IPAddress.Any, _tcpSetting.port);
                _serverListener.Start();

                while (_isRunning)
                {
                    if (!_isConnectionActive)
                    {
                        TcpClient incomingClient = _serverListener.AcceptTcpClient(); 
                        
                        if (incomingClient != null)
                        {
                            Debug.Log("[TcpManager] Client 접속 완료");
                            _connectedClient = incomingClient;
                            _networkStream = _connectedClient.GetStream();
                            
                            _failedConnectionCount = 0; 
                            _isConnectionActive = true;

                            _receiveThread = new Thread(ReceiveDataRoutine);
                            _receiveThread.IsBackground = true;
                            _receiveThread.Start();
                        }
                    }
                    else
                    {
                        Thread.Sleep(1000); 
                    }
                }
            }
            catch (SocketException)
            {
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TcpManager] 서버 리스닝 중단: {e.Message}");
            }
        }

        private void StartClient()
        {
            _connectThread = new Thread(ClientConnectRoutine);
            _connectThread.IsBackground = true;
            _connectThread.Start();
        }

        private void ClientConnectRoutine()
        {
            while (_isRunning)
            {
                if (!_isConnectionActive)
                {
                    try
                    {
                        _connectedClient = new TcpClient(_tcpSetting.serverIP, _tcpSetting.port);
                        _networkStream = _connectedClient.GetStream();
                        
                        _failedConnectionCount = 0;
                        _isConnectionActive = true;

                        Debug.Log($"[TcpManager] Server({_tcpSetting.serverIP}) 접속 완료");

                        _receiveThread = new Thread(ReceiveDataRoutine);
                        _receiveThread.IsBackground = true;
                        _receiveThread.Start();
                    }
                    catch (Exception)
                    {
                        Thread.Sleep(3000);
                    }
                }
                else
                {
                    Thread.Sleep(1000); 
                }
            }
        }

        private void ReceiveDataRoutine()
        {
            byte[] buffer = new byte[1024];

            while (_isRunning && _isConnectionActive && _networkStream != null)
            {
                try
                {
                    int bytesRead = _networkStream.Read(buffer, 0, buffer.Length);
                    
                    if (bytesRead > 0)
                    {
                        string jsonString = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        TcpMessage receivedMessage = JsonUtility.FromJson<TcpMessage>(jsonString);

                        if (receivedMessage != null)
                        {
                            _messageQueue.Enqueue(receivedMessage);
                        }
                    }
                    else
                    {
                        HandleDisconnect();
                        break;
                    }
                }
                catch (Exception)
                {
                    HandleDisconnect();
                    break;
                }
            }
        }

        public void SendMessageToTarget(string command, string payload = "")
        {
            if (playSolo)
            {
                Debug.Log($"[TcpManager-Solo] 가상 송신 및 에코: {command} / {payload}");
                
                // Why: 혼자 테스트할 때 상대방이 보낸 것처럼 즉시 에코(Echo) 처리하여 씬의 동기화 대기 상태를 해제함.
                _messageQueue.Enqueue(new TcpMessage { command = command, payload = payload });
                return;
            }

            if (_isConnectionActive && _networkStream != null)
            {
                TcpMessage msg = new TcpMessage 
                { 
                    command = command, 
                    payload = payload 
                };

                string jsonString = JsonUtility.ToJson(msg);
                byte[] data = Encoding.UTF8.GetBytes(jsonString);

                try
                {
                    _networkStream.Write(data, 0, data.Length);
                    _networkStream.Flush();
                }
                catch (Exception)
                {
                    HandleDisconnect();
                }
            }
        }

        /// <summary>
        /// 통신 에러 발생 시 플래그를 내리고 자원을 정리하여 연결 대기 스레드가 다시 작동하도록 유도함.
        /// </summary>
        private void HandleDisconnect()
        {
            if (!_isConnectionActive) return; 
            
            _isConnectionActive = false;
            Debug.LogWarning("[TcpManager] 통신 단절 감지. 백그라운드 재연결을 시도합니다.");

            if (_networkStream != null) 
            {
                _networkStream.Close();
                _networkStream = null;
            }
            if (_connectedClient != null) 
            {
                _connectedClient.Close();
                _connectedClient = null;
            }
        }

        private void OnDestroy()
        {
            _isRunning = false;
            _isConnectionActive = false;

            if (_heartbeatCoroutine != null) StopCoroutine(_heartbeatCoroutine);

            if (_networkStream != null) _networkStream.Close();
            if (_connectedClient != null) _connectedClient.Close();
            if (_serverListener != null) _serverListener.Stop();
            
            if (_receiveThread != null && _receiveThread.IsAlive) _receiveThread.Abort();
            if (_serverThread != null && _serverThread.IsAlive) _serverThread.Abort();
            if (_connectThread != null && _connectThread.IsAlive) _connectThread.Abort();
        }
    }
}