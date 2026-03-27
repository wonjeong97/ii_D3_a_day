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

    public class TcpManager : MonoBehaviour
    {
        public static TcpManager Instance { get; private set; }
        public Action<TcpMessage> onMessageReceived;

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

        private DateTime _lastMessageReceivedTime;
        private readonly TimeSpan _timeoutThreshold = TimeSpan.FromSeconds(10); 
        
        public bool IsServer 
        {
            get { return _tcpSetting != null && _tcpSetting.isServer; }
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
            TcpSetting loadedSetting = JsonLoader.Load<TcpSetting>(GameConstants.Path.TcpSetting);

            if (loadedSetting == null)
            {
                UnityEngine.Debug.LogError("[TcpManager] TcpSetting.json 로드 실패. 통신을 시작할 수 없습니다.");
                return;
            }

            _tcpSetting = loadedSetting;
            _isRunning = true;

            if (_tcpSetting.isServer) StartServer();
            else StartClient();
            
            _heartbeatCoroutine = StartCoroutine(ConnectionMonitorRoutine());
        }

        private void Update()
        {
            if (_needsToReturnToTitle)
            {
                _needsToReturnToTitle = false;
                
                string currentSceneName = SceneManager.GetActiveScene().name;
                if (currentSceneName != GameConstants.Scene.Title)
                {
                    UnityEngine.Debug.LogError("[TcpManager] 재연결 10회 실패. 타이틀로 강제 초기화합니다.");
                    
                    // Update 내부이므로 GameManager 유니티 객체 접근 시 극단적 최적화 연산 적용
                    if (!object.ReferenceEquals(GameManager.Instance, null)) 
                    {
                        GameManager.Instance.ReturnToTitle();
                    }
                    else 
                    {
                        SceneManager.LoadScene(GameConstants.Scene.Title); 
                    }
                }
            }

            TcpMessage message;
            while (_messageQueue.TryDequeue(out message))
            {
                if (message != null)
                {
                    if (message.command == "HEARTBEAT") continue;
                    if (onMessageReceived != null) onMessageReceived.Invoke(message);
                }
            }
            
            // # TODO: 메시지 처리량이 많아질 경우 프레임 드랍 방지를 위해 한 프레임당 최대 처리 개수 제한 로직 추가 필요
        }

        private IEnumerator ConnectionMonitorRoutine()
        {
            while (_isRunning)
            {
                yield return CoroutineData.GetWaitForSeconds(1.0f); 
                
                if (_isConnectionActive)
                {
                    if (DateTime.UtcNow - _lastMessageReceivedTime > _timeoutThreshold)
                    {
                        Debug.LogWarning("[TcpManager] 통신 10초 타임아웃. 연결을 종료하고 재시작합니다.");
                        HandleDisconnect();
                    }
                    else
                    {
                        _failedConnectionCount = 0;
                        SendMessageToTarget("HEARTBEAT", "");
                    }
                }
                else
                {
                    _failedConnectionCount++;
                    
                    // Why: 현재 권한에 따라 누구를 기다리고 있는지 콘솔에 명시적으로 알리고 재시도 횟수를 트래킹함
                    string targetName = IsServer ? "클라이언트" : "서버";
                    Debug.Log($"[TcpManager] {targetName} 연결을 기다리는 중..{_failedConnectionCount}/10");
                    
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
                            _connectedClient = incomingClient;
                            _networkStream = _connectedClient.GetStream();
                            
                            _lastMessageReceivedTime = DateTime.UtcNow;
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
            catch (SocketException) { }
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
                        
                        _lastMessageReceivedTime = DateTime.UtcNow;
                        _failedConnectionCount = 0;
                        _isConnectionActive = true;

                        _receiveThread = new Thread(ReceiveDataRoutine);
                        _receiveThread.IsBackground = true;
                        _receiveThread.Start();
                    }
                    catch (Exception)
                    {
                        Thread.Sleep(1000); 
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
                        _lastMessageReceivedTime = DateTime.UtcNow;

                        string jsonString = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        TcpMessage receivedMessage = JsonUtility.FromJson<TcpMessage>(jsonString);

                        if (receivedMessage != null) _messageQueue.Enqueue(receivedMessage);
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
            if (_isConnectionActive && _networkStream != null)
            {
                TcpMessage msg = new TcpMessage { command = command, payload = payload };
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

        private void HandleDisconnect()
        {
            if (!_isConnectionActive) return; 
            
            _isConnectionActive = false;

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