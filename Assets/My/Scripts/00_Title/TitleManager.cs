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
    /// 서버 측에서 지속적으로 API를 폴링하여 외부 시작 명령을 대기합니다.
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        private bool _isTransitioning = false; 
        private float _fadeTime = 1.0f; 

        private bool _isWaitingForClient = false;
        private Coroutine _requestCoroutine;
        private Coroutine _pollCoroutine;

        private void Start()
        {
            LoadSettings();

            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
                
                // Why: API 통신은 서버만 전담해야 하므로 서버 모드일 때만 폴링 코루틴을 시작함
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
        /// 3초 주기로 API를 호출하여 방의 상태(EMPTY / USING)를 확인합니다.
        /// Why: 외부 시스템(예: 키오스크, 웹)에서 시작을 제어할 수 있도록 상태를 폴링하기 위함.
        /// </summary>
        private IEnumerator PollRoomStateRoutine()
        {
            while (!_isTransitioning && !_isWaitingForClient)
            {
                yield return CoroutineData.GetWaitForSeconds(3.0f);

                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    continue;
                }

                string url = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code=d3";

                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    request.timeout = 2; // Why: 무한 대기로 인한 코루틴 블로킹 방지
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string responseText = request.downloadHandler.text.Trim().ToUpper();
                        
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

        private void Update()
        {
            if (_isTransitioning) return; 

            // Update 내부이므로 파괴된 유니티 객체를 안전하게 걸러내는 암시적 Null 검사 사용
            if (TcpManager.Instance)
            {
                if (TcpManager.Instance.IsServer)
                {
                    // Why: API 타임아웃 상황을 대비한 수동 디버그 넘김 키
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
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
        /// 클라이언트가 Title 씬에 도착해 응답할 때까지 1초 주기로 계속 넘어갈 준비가 되었는지 물어봅니다.
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
        /// 동기화가 완료되면 최종적으로 씬을 넘기는 처리를 합니다.
        /// </summary>
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
        /// 수신된 TCP 메시지를 파싱하여 씬 전환을 완벽하게 동기화합니다.
        /// </summary>
        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg == null) return;

            // 1. [클라이언트] 서버로부터 튜토리얼로 넘어갈 거냐는 물음을 받으면, 나도 타이틀에 있다고 대답함
            if (msg.command == "REQUEST_START")
            {
                if (TcpManager.Instance && !TcpManager.Instance.IsServer)
                {
                    TcpManager.Instance.SendMessageToTarget("START_ACK", "");
                }
            }
            // 2. [서버] 클라이언트가 타이틀에 도착해 대답을 주면, 물어보기를 멈추고 씬을 넘김
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
            // 3. [클라이언트] 서버의 최종 명령(CHANGE_SCENE)을 받고 씬을 넘김
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