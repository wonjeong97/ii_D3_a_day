using System.Collections;
using My.Scripts.Global;
using My.Scripts.Network; 
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._00_Title
{
    /// <summary>
    /// 타이틀 화면 입력 처리 및 씬 전환 매니저.
    /// 서버 측에서 지속적으로 API를 폴링하여 외부 시작 명령을 대기함.
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        private bool _isTransitioning; 
        private float _fadeTime = 1.0f; 

        private bool _isWaitingForClient;
        private Coroutine _requestCoroutine;
        private Coroutine _pollCoroutine;
        
        [Header("API Polling Settings")]
        [SerializeField] private float pollingInterval = 3.0f;
        [SerializeField] private int apiTimeout = 10;

        /// <summary>
        /// 매니저 초기화 및 네트워크 이벤트 구독을 수행함.
        /// </summary>
        private void Start()
        {
            LoadSettings();

            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
                
                // 외부 API 통신 부하는 서버 권한을 가진 PC에서만 전담하도록 제한함.
                if (TcpManager.Instance.IsServer)
                {
                    _pollCoroutine = StartCoroutine(PollRoomStateRoutine());
                }
            }
            else
            {
                Debug.LogWarning("[TitleManager] TcpManager 인스턴스가 존재하지 않습니다.");
            }
        }

        /// <summary>
        /// 환경 설정 JSON 데이터를 메모리에 적재함.
        /// Why: 페이드 시간 및 공통 설정값을 씬 진입 시점에 즉시 적용하기 위함.
        /// </summary>
        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);

            if (settings != null)
            {
                if (SoundManager.Instance)
                {
                    SoundManager.Instance.PlayBGM("MainBGM");
                }
                else
                {
                    Debug.LogWarning("[TitleManager] SoundManager 인스턴스가 존재하지 않습니다.");
                }
                
                _fadeTime = settings.fadeTime; 
            }
            else
            {
                Debug.LogWarning("[TitleManager] Settings.json 로드 실패.");
            }
        }

        /// <summary>
        /// 외부 API를 주기적으로 호출하여 현재 방의 점유 상태를 확인함.
        /// </summary>
        private IEnumerator PollRoomStateRoutine()
        {
            while (!_isTransitioning && !_isWaitingForClient)
            {
                yield return CoroutineData.GetWaitForSeconds(pollingInterval);

                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    continue;
                }

                string url = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code=d3";

                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    // 무한 대기로 인한 코루틴 블로킹 현상을 방지함.
                    request.timeout = apiTimeout;
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string responseText = request.downloadHandler.text.Trim().ToUpper();
                        
                        // 외부 시스템의 시작 명령을 감지하여 씬 전환을 트리거함.
                        if (responseText.Contains("USING"))
                        {
                            Debug.Log("[TitleManager] 방 상태가 USING으로 확인되었습니다. 클라이언트 진입 대기를 시작합니다.");
                            _isWaitingForClient = true;
                            _requestCoroutine = StartCoroutine(RequestStartRoutine());
                            break;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[TitleManager] CheckRoomState API 호출 실패: {request.error}");
                    }
                }
            }
        }

        /// <summary>
        /// 매 프레임 수동 조작 상태를 검사함.
        /// </summary>
        private void Update()
        {
            if (_isTransitioning) return; 

            //  Update 내부이므로 파괴된 유니티 객체를 안전하게 걸러내는 암시적 불리언 변환 사용.
            if (TcpManager.Instance)
            {
                if (TcpManager.Instance.IsServer)
                {
                    // API 서버 장애 발생 시 테스트 및 원활한 시연을 위해 수동 씬 전환 단축키를 제공함.
                    if (Input.GetKeyDown(KeyCode.Return))
                    {
                        if (!_isWaitingForClient)
                        {
                            _isWaitingForClient = true;
                            Debug.Log("[TitleManager] 수동 엔터 입력. 클라이언트 진입 대기를 시작합니다...");
                            
                            if (_pollCoroutine != null) StopCoroutine(_pollCoroutine);
                            _requestCoroutine = StartCoroutine(RequestStartRoutine());
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 클라이언트 PC의 씬 진입 준비 상태를 지속적으로 확인함.
        /// Why: 양쪽 PC가 동일한 타이밍에 다음 씬으로 넘어가도록 동기화하기 위함.
        /// </summary>
        private IEnumerator RequestStartRoutine()
        {
            while (_isWaitingForClient)
            {
                if (TcpManager.Instance)
                {
                    TcpManager.Instance.SendMessageToTarget("REQUEST_START", "");
                }
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }
        }

        /// <summary>
        /// 모든 준비가 완료되었을 때 실제 씬 전환 로직을 실행함.
        /// Why: 중복 전환을 방지하고 클라이언트에게 전환 명령을 하달하기 위함.
        /// </summary>
        /// <param name="playerID">태그된 플레이어의 고유 식별자.</param>
        private void ProcessTag(int playerID)
        {
            if (_isTransitioning) return;
            _isTransitioning = true; 

            if (TcpManager.Instance && TcpManager.Instance.IsServer)
            {
                TcpManager.Instance.SendMessageToTarget("CHANGE_SCENE", GameConstants.Scene.Tutorial);
            }

            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.Tutorial, true);
            }
            else
            {
                Debug.LogWarning("[TitleManager] GameManager 인스턴스가 존재하지 않습니다.");
                SceneManager.LoadScene(GameConstants.Scene.Tutorial);
            }
        }

        /// <summary>
        /// 수신된 TCP 통신 메시지를 분석하고 동기화 로직을 처리함.
        /// </summary>
        /// <param name="msg">네트워크 수신 메시지 객체.</param>
        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg == null) return;

            // 서버의 시작 확인 요청에 대해 클라이언트가 준비 완료 상태임을 응답함.
            if (msg.command == "REQUEST_START")
            {
                if (TcpManager.Instance && !TcpManager.Instance.IsServer)
                {
                    TcpManager.Instance.SendMessageToTarget("START_ACK", "");
                }
            }
            // 클라이언트의 준비 완료 응답을 확인한 후 서버가 최종 씬 전환을 지시함.
            else if (msg.command == "START_ACK")
            {
                if (TcpManager.Instance && TcpManager.Instance.IsServer)
                {
                    if (_isWaitingForClient && !_isTransitioning)
                    {
                        _isWaitingForClient = false;
                        if (_requestCoroutine != null) StopCoroutine(_requestCoroutine);
                        
                        Debug.Log("[TitleManager] 클라이언트 준비 완료. Tutorial 씬으로 동시 이동합니다.");
                        ProcessTag(1); 
                    }
                }
            }
            // 서버의 최종 씬 전환 명령을 수신하여 클라이언트의 씬을 변경함.
            else if (msg.command == "CHANGE_SCENE")
            {
                if (!_isTransitioning)
                {
                    _isTransitioning = true;
                    
                    if (GameManager.Instance)
                    {
                        GameManager.Instance.ChangeScene(msg.payload, true);
                    }
                    else
                    {
                        Debug.LogWarning("[TitleManager] GameManager 인스턴스가 존재하지 않습니다.");
                        SceneManager.LoadScene(msg.payload);
                    }
                }
            }
        }

        /// <summary>
        /// 씬 종료 시 리소스를 해제함.
        /// Why: 메모리 누수 및 비정상적인 네트워크 콜백 실행을 방지하기 위함.
        /// </summary>
        private void OnDestroy()
        {   
            StopAllCoroutines();
            
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
            }
        }
    }
}