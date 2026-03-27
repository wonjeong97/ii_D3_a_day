using System;
using System.Collections.Concurrent;
using System.Collections;
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
    /// <summary>
    /// TCP 통신 설정을 저장하는 데이터 클래스입니다.
    /// </summary>
    [Serializable]
    public class TcpSetting
    {
        public bool isServer;
        public string serverIP;
        public int port;
    }

    /// <summary>
    /// TCP 네트워크를 통해 전달되는 메시지 규격입니다.
    /// </summary>
    [Serializable]
    public class TcpMessage
    {
        public string command; 
        public string payload; 
    }

    /// <summary>
    /// 서버와 클라이언트 간의 TCP 통신을 관리하는 매니저 클래스입니다.
    /// 비동기 스레드를 사용하여 연결 및 수신을 처리하며, 메인 스레드 안전을 위해 큐 방식을 사용합니다.
    /// </summary>
    public class TcpManager : MonoBehaviour
    {
        public static TcpManager Instance { get; private set; }
        public Action<TcpMessage> onMessageReceived;

    
        private readonly int _maxMessagesPerFrame = 30;
        
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
            // Unity Object Null 검사 시 암시적 불리언 변환 사용
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

        /// <summary>
        /// 네트워크 설정을 로드하고 서버 또는 클라이언트를 시작합니다.
        /// </summary>
        private void InitializeNetwork()
        {
            TcpSetting loadedSetting = JsonLoader.Load<TcpSetting>(GameConstants.Path.TcpSetting);

            if (loadedSetting == null)
            {
                Debug.LogError("[TcpManager] TcpSetting.json 로드 실패.");
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
            HandleSceneTransitionRequest();
            ProcessMessageQueue();
        }

        /// <summary>
        /// 타이틀 씬으로의 복귀 요청이 있는지 확인하고 처리합니다.
        /// </summary>
        private void HandleSceneTransitionRequest()
        {
            if (_needsToReturnToTitle)
            {
                _needsToReturnToTitle = false;
                
                string currentSceneName = SceneManager.GetActiveScene().name;
                if (currentSceneName != GameConstants.Scene.Title)
                {
                    Debug.LogError("[TcpManager] 연결 실패 임계치 도달. 타이틀로 이동합니다.");
                    
                    // Update 내부 성능 최적화를 위해 ReferenceEquals 사용
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
        }

        /// <summary> 수신 큐에 쌓인 메시지를 처리합니다. </summary>
        private void ProcessMessageQueue()
        {
            int processedThisFrame = 0;

            // Why: 한 프레임에 너무 많은 메시지를 처리하면 메인 스레드 병목이 발생하므로 개수를 제한함
            while (processedThisFrame < _maxMessagesPerFrame && _messageQueue.TryDequeue(out TcpMessage message))
            {
                if (message != null)
                {
                    // 하트비트는 로직 연산에서 제외
                    if (message.command == "HEARTBEAT") 
                    {
                        processedThisFrame++;
                        continue;
                    }

                    if (onMessageReceived != null) 
                    {
                        onMessageReceived.Invoke(message);
                    }
                }
                processedThisFrame++;
            }
        }

        /// <summary>
        /// 연결 상태를 주기적으로 감시하고 하트비트를 전송합니다.
        /// </summary>
        private IEnumerator ConnectionMonitorRoutine()
        {
            while (_isRunning)
            {
                yield return CoroutineData.GetWaitForSeconds(1.0f); 
                
                if (_isConnectionActive)
                {
                    // 10초 이상 무응답 시 연결 끊김으로 간주
                    if (DateTime.UtcNow - _lastMessageReceivedTime > _timeoutThreshold)
                    {
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

        /// <summary>
        /// 서버 모드에서 클라이언트의 접속을 대기하는 루틴입니다.
        /// </summary>
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

        /// <summary>
        /// 클라이언트 모드에서 서버 접속을 시도하는 루틴입니다.
        /// </summary>
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

        /// <summary>
        /// 소켓으로부터 데이터를 읽어 메시지 큐에 삽입합니다.
        /// </summary>
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

        /// <summary>
        /// 대상에게 JSON 형식의 메시지를 전송합니다.
        /// </summary>
        /// <param name="command">명령어 키</param>
        /// <param name="payload">데이터 내용</param>
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

        /// <summary>
        /// 현재 활성화된 연결과 스트림을 안전하게 닫습니다.
        /// </summary>
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
            
            // Background Thread 종료 대기
            if (_receiveThread != null && _receiveThread.IsAlive) _receiveThread.Abort();
            if (_serverThread != null && _serverThread.IsAlive) _serverThread.Abort();
            if (_connectThread != null && _connectThread.IsAlive) _connectThread.Abort();
        }
    }
}