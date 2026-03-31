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
    /// 외부 JSON 파일에서 로드되는 TCP 통신 설정 모델.
    /// </summary>
    [Serializable]
    public class TcpSetting
    {
        public bool isServer;
        public string serverIP;
        public int port; 
        public int httpPort; 
        public string localSaveRoot; 
    }

    /// <summary>
    /// 서버와 클라이언트 간에 송수신되는 메시지 데이터 규격.
    /// </summary>
    [Serializable]
    public class TcpMessage
    {
        public string command; 
        public string payload; 
    }

    /// <summary>
    /// 서버와 클라이언트 간의 1대1 TCP 통신을 관리하는 매니저.
    /// 비동기 스레드로 수신된 데이터를 ConcurrentQueue에 적재하여 메인 스레드에서 안전하게 처리함.
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
        
        [Header("Debug Settings")]
        [Tooltip("체크하면 JSON 설정을 무시하고 아래의 inspectorIsServer 값을 사용합니다.")]
        [SerializeField] private bool overrideSettings = false;
        [Tooltip("overrideSettings가 true일 때 적용되는 서버/클라이언트 여부")]
        [SerializeField] private bool inspectorIsServer = true;

        public bool IsServer 
        {
            get 
            { 
                if (overrideSettings) return inspectorIsServer;
                return _tcpSetting != null && _tcpSetting.isServer; 
            }
        }

        /// <summary>
        /// 싱글톤 인스턴스를 초기화하고 네트워크 설정을 로드함.
        /// </summary>
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

        /// <summary>
        /// 설정값에 따라 서버 대기 또는 클라이언트 접속 스레드를 가동함.
        /// 연결 상태를 주기적으로 확인하기 위해 하트비트 모니터링 코루틴을 실행함.
        /// </summary>
        private void InitializeNetwork()
        {
            TcpSetting loadedSetting = JsonLoader.Load<TcpSetting>(GameConstants.Path.TcpSetting);

            if (loadedSetting == null)
            {
                Debug.LogError("[TcpManager] TcpSetting 로드 실패");
                return;
            }

            _tcpSetting = loadedSetting;
            _isRunning = true;

            // IsServer 프로퍼티를 통해 인스펙터 오버라이드 여부를 확인하여 서버/클라이언트를 구동함
            if (IsServer) StartServer();
            else StartClient();
            
            _heartbeatCoroutine = StartCoroutine(ConnectionMonitorRoutine());
        }

        /// <summary>
        /// 매 프레임 수신된 메시지 큐를 처리하고 연결 유실에 따른 씬 전환 여부를 확인함.
        /// </summary>
        private void Update()
        {
            HandleSceneTransitionRequest();
            ProcessMessageQueue();
        }

        /// <summary>
        /// 네트워크 연결 임계치 도달 시 타이틀 씬으로 강제 이동함.
        /// Update 내부 성능 최적화를 위해 Unity Object 비교 시 ReferenceEquals를 활용함.
        /// </summary>
        private void HandleSceneTransitionRequest()
        {
            if (_needsToReturnToTitle)
            {
                _needsToReturnToTitle = false;
                
                string currentSceneName = SceneManager.GetActiveScene().name;
                if (currentSceneName != GameConstants.Scene.Title)
                {
                    Debug.LogError("[TcpManager] 연결 유실 임계치 도달로 인한 타이틀 이동");
                    
                    if (GameManager.Instance) 
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

        /// <summary>
        /// 스레드 안전 큐(ConcurrentQueue)에서 메시지를 꺼내 등록된 액션을 실행함.
        /// 과도한 메시지 처리가 메인 스레드 프레임 드랍을 유발하지 않도록 처리 개수를 제한함.
        /// </summary>
        private void ProcessMessageQueue()
        {
            int processedThisFrame = 0;

            while (processedThisFrame < _maxMessagesPerFrame && _messageQueue.TryDequeue(out TcpMessage message))
            {
                if (message != null)
                {
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
        /// 연결된 상대방에게 주기적으로 신호를 보내 생존 여부를 확인함.
        /// 하트비트 응답이 일정 시간 부재하거나 물리적 연결이 끊기면 재접속 또는 종료 시퀀스를 트리거함.
        /// </summary>
        private IEnumerator ConnectionMonitorRoutine()
        {
            while (_isRunning)
            {
                yield return CoroutineData.GetWaitForSeconds(1.0f); 
                
                if (_isConnectionActive)
                {
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
                    // Why: 디버그 모드에서는 혼자 테스트하는 경우가 많으므로 통신 무응답으로 인한 타이틀 강제 복귀를 방지함.
                    if (!GameManager.Instance || !GameManager.Instance.isDebugMode)
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
        }

        /// <summary>
        /// 서버 모드로 리스너 스레드를 생성하여 실행함.
        /// </summary>
        private void StartServer()
        {
            _serverThread = new Thread(ServerListenRoutine);
            _serverThread.IsBackground = true;
            _serverThread.Start();
        }

        /// <summary>
        /// 클라이언트의 접속을 무한 대기하며 연결 시 수신 전전담 스레드를 가동함.
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
                        Debug.Log("[TcpManager] 클라이언트 접속을 기다리는 중...");
                        TcpClient incomingClient = _serverListener.AcceptTcpClient(); 
                        
                        if (incomingClient != null)
                        {
                            Debug.Log("[TcpManager] 클라이언트 접속 완료.");
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

        /// <summary>
        /// 클라이언트 모드로 서버 접속 시도 스레드를 실행함.
        /// </summary>
        private void StartClient()
        {
            _connectThread = new Thread(ClientConnectRoutine);
            _connectThread.IsBackground = true;
            _connectThread.Start();
        }

        /// <summary>
        /// 서버에 연결될 때까지 주기적으로 접속을 재시도함.
        /// </summary>
        private void ClientConnectRoutine()
        {
            bool isWaitingLogPrinted = false;

            while (_isRunning)
            {
                if (!_isConnectionActive)
                {
                    try
                    {
                        if (!isWaitingLogPrinted)
                        {
                            Debug.Log("[TcpManager] 서버 접속을 기다리는 중...");
                            isWaitingLogPrinted = true;
                        }

                        _connectedClient = new TcpClient(_tcpSetting.serverIP, _tcpSetting.port);
                        _networkStream = _connectedClient.GetStream();
                        
                        Debug.Log("[TcpManager] 서버 접속 완료.");
                        isWaitingLogPrinted = false;

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
        /// 소켓 버퍼에서 데이터를 읽어 JSON 객체로 역직렬화한 뒤 큐에 삽입함.
        /// 블로킹 메서드인 Read를 별도 스레드에서 수행하여 메인 루프 지연을 방지함.
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
        /// 명령어와 데이터를 JSON 문자열로 변환하여 상대방에게 전송함.
        /// </summary>
        /// <param name="command">식별 명령어</param>
        /// <param name="payload">전달할 데이터 내용</param>
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
        /// 활성화된 스트림과 소켓 연결을 종료하고 리소스를 정리함.
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

        /// <summary>
        /// 객체 파괴 시 모든 통신 스레드와 리스너를 강제 중단하고 정리함.
        /// </summary>
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