using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using My.Scripts.Core;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Network;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial.Pages
{   
    /// <summary>
    /// JSON에서 로드되는 튜토리얼 1페이지 데이터 구조체.
    /// </summary>
    [Serializable]
    public class TutorialPage1Data
    {
        public TextSetting descriptionText; 
    }
    
    /// <summary>
    /// 첫 번째 튜토리얼 페이지 컨트롤러.
    /// 서버에서 방 상태를 확인하고 UID를 추출한 뒤, 양쪽 PC가 모두 FetchData를 완료할 때까지 기다렸다가 동시에 전환함.
    /// </summary>
    public class TutorialPage1Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup textCanvasGroup;
        [SerializeField] private Text descriptionText;

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private string _cachedMessage = string.Empty;
        private bool _isPageActive;
        private Coroutine _fadeCoroutine;
        private Coroutine _pollCoroutine;
        
        private bool _isWaitingForClientFetch;
        private string _pendingClientUid = string.Empty;
        private Coroutine _clientFetchTimeoutCoroutine;
        [SerializeField] private float _clientFetchTimeout = 15.0f;
        
        private CancellationTokenSource _pageCts;
        private CancellationTokenSource _serverFetchCts;
        
        [Header("API Polling Settings")]
        [SerializeField] private float pollingInterval = 3.0f;
        [SerializeField] private int apiTimeout = 10;

        /// <summary>
        /// 매 프레임 수동 조작 상태를 검사함.
        /// </summary>
        private void Update()
        {
            if (!_isPageActive) return;
            
            // 파괴된 유니티 객체를 안전하게 걸러내기 위해 암시적 불리언 변환을 사용함.
            if (TcpManager.Instance)
            {
                if (TcpManager.Instance.IsServer)
                {
                    // API 서버 장애 발생 시 원활한 테스트 및 시연을 위해 수동 씬 전환 단축키를 제공함.
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    {
                        OnConfirmInput();
                    }
                }
            }
        }

        /// <summary>
        /// 페이지 초기 데이터 세팅.
        /// JSON에서 로드한 UI 텍스트 데이터를 캐싱하여 렌더링 시 사용하기 위함.
        /// </summary>
        public override void SetupData(object data)
        {
            TutorialPage1Data pageData = data as TutorialPage1Data;
            
            if (pageData != null)
            {
                if (pageData.descriptionText != null)
                {
                    _cachedMessage = pageData.descriptionText.text;
                }
                else
                {
                    Debug.LogWarning("[TutorialPage1Controller] descriptionText 데이터가 null입니다.");
                }
            }
            else
            {   
                Debug.LogError("[TutorialPage1Controller] 전달된 데이터가 null이거나 형식이 잘못되었습니다.");
            }
        }

        /// <summary>
        /// 페이지 진입 시 상태 초기화 및 연출 시작.
        /// 이전 비동기 작업을 취소하고 서버 모드일 경우 API 폴링을 시작하기 위함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter(); 

            _isPageActive = true;
            _isWaitingForClientFetch = false;
            _pendingClientUid = string.Empty;

            // 페이지 진입 시 이전 페치 작업을 취소할 수 있도록 토큰 소스를 초기화함.
            if (_pageCts != null)
            {
                _pageCts.Cancel();
                _pageCts.Dispose();
            }
            _pageCts = new CancellationTokenSource();
            
            if (descriptionText)
            {
                if (!string.IsNullOrEmpty(_cachedMessage))
                {
                    descriptionText.text = _cachedMessage;
                }
                else
                {
                    Debug.LogWarning("[TutorialPage1Controller] _cachedMessage가 비어있습니다. 텍스트를 갱신하지 않습니다.");
                }
            }

            if (textCanvasGroup)
            {
                textCanvasGroup.alpha = 0f;
                _fadeCoroutine = StartCoroutine(FadeTextRoutine(textCanvasGroup, 0f, 1f, fadeDuration));
            }

            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;

                // 외부 API 통신 부하를 줄이기 위해 서버 권한을 가진 PC에서만 통신을 전담하도록 제한함.
                if (TcpManager.Instance.IsServer)
                {
                    if (_pollCoroutine != null) StopCoroutine(_pollCoroutine);
                    _pollCoroutine = StartCoroutine(PollApiRoutine());
                }
            }
        }

        /// <summary>
        /// 주기적으로 API를 호출하여 방 상태 및 유저 존재 여부를 확인함.
        /// 유저가 룸에 입장했는지 감지하고 세션 통신을 시작하기 위함.
        /// </summary>
        private IEnumerator PollApiRoutine()
        {
            float emptyUserStartTime = -1f;

            while (_isPageActive)
            {
                yield return CoroutineData.GetWaitForSeconds(pollingInterval);

                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    continue;
                }

                ApiSettings config = GameManager.Instance.ApiConfig;
                string moduleCode = SessionManager.Instance ? SessionManager.Instance.CurrentModuleCode : "D3";
                string roomStateUrl = $"{config.CheckRoomStateUrl}?code={moduleCode}";
                string roomState = string.Empty;
                bool roomStateSuccess = false;

                for (int attempt = 0; attempt < 10; attempt++)
                {
                    using (UnityWebRequest req = UnityWebRequest.Get(roomStateUrl))
                    {
                        req.timeout = apiTimeout;
                        yield return req.SendWebRequest();

                        if (req.result == UnityWebRequest.Result.Success)
                        {
                            roomState = req.downloadHandler.text.Trim().ToUpper();
                            roomStateSuccess = true;
                            break;
                        }

                        if (attempt < 9) yield return CoroutineData.GetWaitForSeconds(1.0f); 
                    }
                }

                if (!roomStateSuccess)
                {
                    Debug.LogWarning("[TutorialPage1Controller] CheckRoomState API 호출 실패");
                    continue;
                }

                if (roomState.Contains("EMPTY"))
                {
                    Debug.Log("[TutorialPage1Controller] 방 상태가 EMPTY입니다. 타이틀로 돌아갑니다.");
                    yield return StartCoroutine(ReturnToTitleSequence());
                    yield break; 
                }
                else if (roomState.Contains("USING"))
                {   
                    string userUrl = $"{config.GetCurrentRoomUserUrl}?code={moduleCode}";
                    string userData = string.Empty;
                    bool userSuccess = false;

                    for (int attempt = 0; attempt < 10; attempt++)
                    {
                        using (UnityWebRequest req = UnityWebRequest.Get(userUrl))
                        {
                            req.timeout = apiTimeout;
                            yield return req.SendWebRequest();

                            if (req.result == UnityWebRequest.Result.Success)
                            {
                                userData = req.downloadHandler.text.Trim();
                                userSuccess = true;
                                break;
                            }

                            if (attempt < 9) yield return CoroutineData.GetWaitForSeconds(1.0f); 
                        }
                    }

                    if (!userSuccess)
                    {
                        Debug.LogWarning("[TutorialPage1Controller] GetCurrentRoomUser API 호출 실패");
                        continue;
                    }

                    // API 응답이 비어있지 않은지 검사하여 유저 입장이 완료되었음을 판별하고 UID를 추출함.
                    if (!string.IsNullOrEmpty(userData) && userData != "[]" && userData.ToLower() != "null")
                    {
                        Debug.Log("[TutorialPage1Controller] 방 상태 USING 및 유저 정보 확인됨. 개별 FetchData를 수행합니다.");
                        emptyUserStartTime = -1f; 

                        // 쉼표나 줄바꿈으로 구분된 응답 데이터에서 첫 번째 값인 UID를 안전하게 분리함.
                        string[] parts = userData.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        
                        if (parts.Length >= 1)
                        {
                            string uidLeft = parts[0].Trim();

                            if (APIManager.Instance)
                            {   
                                bool fetchSuccess = false;
                                bool fetchFaulted = false;

                                if (_serverFetchCts != null)
                                {
                                    _serverFetchCts.Cancel();
                                    _serverFetchCts.Dispose();
                                }
                                _serverFetchCts = new CancellationTokenSource();

                                yield return APIManager.Instance.FetchDataAsync(uidLeft, 25.0f, _serverFetchCts.Token)
                                                       .ToCoroutine(
                                                            r => fetchSuccess = r, 
                                                            ex => { fetchFaulted = true; }
                                                        );

                                if (fetchFaulted || !fetchSuccess || !SessionManager.Instance || SessionManager.Instance.CurrentUserIdx == 0)
                                {
                                    Debug.LogWarning("[TutorialPage1Controller] 서버 유저 데이터 Fetch 실패. 다음 주기에 재시도합니다.");
                                    continue; 
                                }
                            }
                            else
                            {
                                Debug.LogWarning("[TutorialPage1Controller] APIManager가 연결되지 않았습니다.");
                                continue;
                            }
                            
                            if (TcpManager.Instance)
                            {
                                _isWaitingForClientFetch = true;
                                _pendingClientUid = uidLeft;
                                TcpManager.Instance.SendMessageToTarget("REQUEST_CLIENT_FETCH", uidLeft);
                                
                                if (_clientFetchTimeoutCoroutine != null) StopCoroutine(_clientFetchTimeoutCoroutine);
                                _clientFetchTimeoutCoroutine = StartCoroutine(ClientFetchAckTimeoutRoutine());
                            }
                            else
                            {
                                CompletePage();
                            }
                            
                            yield break; 
                        }
                    }
                    else
                    {
                        if (emptyUserStartTime < 0f)
                        {
                            emptyUserStartTime = Time.time;
                        }

                        float elapsedEmptyTime = Time.time - emptyUserStartTime;
                        Debug.Log($"[TutorialPage1Controller] 대기 시간: {elapsedEmptyTime:F1}초");

                        if (elapsedEmptyTime >= 15f)
                        {
                            Debug.Log("[TutorialPage1Controller] 유저 데이터 15초 타임아웃. 타이틀로 돌아갑니다.");
                            yield return StartCoroutine(ReturnToTitleSequence());
                            yield break;
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 클라이언트 통신 응답 대기 시간을 초과할 경우 작동하는 안전장치.
        /// 상대방 통신 불량 시 무한 대기를 방지하고 씬을 초기화하기 위함.
        /// </summary>
        private IEnumerator ClientFetchAckTimeoutRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(_clientFetchTimeout);
            
            if (_isWaitingForClientFetch)
            {
                Debug.LogWarning("[TutorialPage1Controller] 클라이언트 Fetch 통신 응답 타임아웃. 타이틀로 돌아갑니다.");
                _isWaitingForClientFetch = false;
                _pendingClientUid = string.Empty;
                yield return StartCoroutine(ReturnToTitleSequence());
            }
        }

        /// <summary>
        /// 클라이언트에게 귀환 명령을 내리고 본인도 타이틀로 돌아가는 시퀀스 코루틴.
        /// 네트워크 오류 발생 시 양쪽 기기의 씬 상태를 타이틀로 동일하게 맞추기 위함.
        /// </summary>
        private IEnumerator ReturnToTitleSequence()
        {
            if (TcpManager.Instance)
            {
                TcpManager.Instance.SendMessageToTarget("RETURN_TO_TITLE", "");
            }
            
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            
            if (GameManager.Instance)
            {
                GameManager.Instance.ReturnToTitle();
            }
        }

        /// <summary>
        /// 강제 진행 시 빈 페이로드를 전송함.
        /// 개발 중 수동 스킵 명령을 상대 기기에도 동일하게 전달하기 위함.
        /// </summary>
        private void OnConfirmInput()
        {
            if (TcpManager.Instance)
            {
                if (TcpManager.Instance.IsServer)
                {
                    TcpManager.Instance.SendMessageToTarget("PAGE1_COMPLETE", "");
                }
            }

            CompletePage();
        }

        /// <summary>
        /// 클라이언트의 데이터를 비동기로 불러오는 요청을 처리함.
        /// 클라이언트 측에서 필요한 세션 데이터를 API를 통해 동기화하기 위함.
        /// </summary>
        /// <param name="uidToFetch">조회할 대상의 고유 ID.</param>
        /// <param name="token">작업 취소 토큰.</param>
        private async UniTaskVoid ProcessClientFetchAsync(string uidToFetch, CancellationToken token)
        {
            try
            {
                // 타임아웃 처리 로직이 APIManager 내부로 이관되어 호출부가 간결해짐.
                bool success = await APIManager.Instance.FetchDataAsync(uidToFetch, 12.0f, token);
                
                // 데이터 수신 성공이 확인된 후 서버에 준비 완료 ACK를 전송하여 다음 흐름을 유도함.
                if (success && !token.IsCancellationRequested)
                {
                    if (TcpManager.Instance) 
                    {
                        TcpManager.Instance.SendMessageToTarget("CLIENT_FETCH_ACK", uidToFetch);
                    }
                }
                else
                {
                    Debug.LogWarning("[TutorialPage1Controller] 클라이언트 데이터 Fetch 실패. ACK를 전송하지 않습니다.");
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[TutorialPage1Controller] 클라이언트 Fetch 작업이 취소되거나 타임아웃되었습니다.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TutorialPage1Controller] 클라이언트 데이터 Fetch 중 예외 발생: {ex.Message}");
            }
        }

        /// <summary>
        /// 수신된 네트워크 메시지를 파싱하여 동기화 및 씬 전환을 처리함.
        /// </summary>
        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null)
            {
                if (msg.command == "REQUEST_CLIENT_FETCH")
                {
                    if (TcpManager.Instance && !TcpManager.Instance.IsServer && APIManager.Instance && !string.IsNullOrEmpty(msg.payload) && _pageCts != null)
                    {
                        string uidToFetch = msg.payload;
                        ProcessClientFetchAsync(uidToFetch, _pageCts.Token).Forget();
                    }
                }
                else if (msg.command == "CLIENT_FETCH_ACK")
                {
                    if (TcpManager.Instance && TcpManager.Instance.IsServer && _isWaitingForClientFetch)
                    {
                        // 요청했던 UID와 응답으로 돌아온 식별자가 동일한지 검증하여 무결성을 확인함.
                        if (msg.payload == _pendingClientUid)
                        {
                            _isWaitingForClientFetch = false;
                            _pendingClientUid = string.Empty;
                            
                            if (_clientFetchTimeoutCoroutine != null)
                            {
                                StopCoroutine(_clientFetchTimeoutCoroutine);
                                _clientFetchTimeoutCoroutine = null;
                            }
                            OnConfirmInput(); 
                        }
                        else
                        {
                            Debug.LogWarning($"[TutorialPage1Controller] 잘못된 클라이언트 Fetch ACK 수신. Expected: {_pendingClientUid}, Received: {msg.payload}");
                        }
                    }
                }
                else if (msg.command == "PAGE1_COMPLETE")
                {
                    CompletePage();
                }
                else if (msg.command == "RETURN_TO_TITLE")
                {
                    if (GameManager.Instance)
                    {
                        GameManager.Instance.ReturnToTitle();
                    }
                }
            }
        }

        /// <summary>
        /// 완료 신호를 발생시켜 매니저의 다음 페이지 전환을 트리거함.
        /// </summary>
        private void CompletePage()
        {
            if (!_isPageActive) return; 
            _isPageActive = false;
            
            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }

        /// <summary>
        /// 페이지 이탈 시 실행 중인 비동기 작업 및 네트워크 구독을 해제함.
        /// 백그라운드 스레드 누수 방지 및 콜백 중복 실행을 막기 위함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            
            if (SoundManager.Instance)
            {
                SoundManager.Instance.StopBGM();
                SoundManager.Instance.PlayBGM("MainBGM");                
            }
            
            _isPageActive = false;

            if (_pageCts != null)
            {
                _pageCts.Cancel();
                _pageCts.Dispose();
                _pageCts = null;
            }

            if (_serverFetchCts != null)
            {
                _serverFetchCts.Cancel();
                _serverFetchCts.Dispose();
                _serverFetchCts = null;
            }

            if (_pollCoroutine != null)
            {
                StopCoroutine(_pollCoroutine);
                _pollCoroutine = null;
            }

            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            if (_clientFetchTimeoutCoroutine != null)
            {
                StopCoroutine(_clientFetchTimeoutCoroutine);
                _clientFetchTimeoutCoroutine = null;
            }

            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
            }
        }

        /// <summary>
        /// 텍스트 UI의 알파값을 선형 보간하여 페이드 연출을 수행함.
        /// </summary>
        private IEnumerator FadeTextRoutine(CanvasGroup target, float start, float end, float duration)
        {
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                
                if (target) 
                {
                    target.alpha = Mathf.Lerp(start, end, elapsed / duration);
                }
                
                yield return null;
            }

            if (target) 
            {
                target.alpha = end;
            }
        }
    }
}