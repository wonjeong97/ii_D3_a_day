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
    /// 서버에서 방 상태를 확인하고 UID를 추출한 뒤, 양쪽 PC가 모두 FetchData를 완료할 때까지 기다렸다가 동시에 전환합니다.
    /// </summary>
    public class TutorialPage1Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup textCanvasGroup;
        [SerializeField] private Text descriptionText;

        [Header("Network & API")]
        [SerializeField] private APIManager apiManager; 

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

        private void Update()
        {
            if (!_isPageActive) return;
            
            // Update 루프 내부이므로 파괴된 유니티 객체를 안전하게 걸러내는 암시적 Null 검사 사용
            if (TcpManager.Instance)
            {
                if (TcpManager.Instance.IsServer)
                {
                    // Why: API 통신 지연이나 오류 상황을 대비한 수동 강제 진행 단축키
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    {
                        OnConfirmInput();
                    }
                }
            }
        }

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

        public override void OnEnter()
        {
            base.OnEnter(); 

            _isPageActive = true;
            _isWaitingForClientFetch = false;
            _pendingClientUid = string.Empty;

            // Why: 페이지 진입 시 이전 페치 작업을 취소할 수 있도록 CancellationTokenSource 초기화
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

                // Why: 서버만 방 상태 및 유저 데이터를 조회하는 책임을 가집니다.
                if (TcpManager.Instance.IsServer)
                {
                    if (_pollCoroutine != null) StopCoroutine(_pollCoroutine);
                    _pollCoroutine = StartCoroutine(PollApiRoutine());
                }
            }
        }

        /// <summary>
        /// 3초 주기로 방 상태 및 유저 존재 여부를 확인하는 폴링 코루틴.
        /// </summary>
        private IEnumerator PollApiRoutine()
        {
            float emptyUserStartTime = -1f;

            while (_isPageActive)
            {
                yield return CoroutineData.GetWaitForSeconds(3.0f);

                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    continue;
                }

                ApiSettings config = GameManager.Instance.ApiConfig;
                string roomStateUrl = $"{config.CheckRoomStateUrl}?code=d3";
                string roomState = string.Empty;
                bool roomStateSuccess = false;

                // 1. 방 상태 확인 (타임아웃 10초, 최대 10회 재시도)
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    using (UnityWebRequest req = UnityWebRequest.Get(roomStateUrl))
                    {
                        req.timeout = 10;
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
                    string userUrl = $"{config.GetCurrentRoomUserUrl}?code=d3";
                    string userData = string.Empty;
                    bool userSuccess = false;

                    // 2. 현재 방 유저 조회 (타임아웃 10초, 최대 10회 재시도)
                    for (int attempt = 0; attempt < 10; attempt++)
                    {
                        using (UnityWebRequest req = UnityWebRequest.Get(userUrl))
                        {
                            req.timeout = 10;
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

                    // Why: API 응답이 비어있지 않은지 검사하여 유저 입장이 완료되었음을 판별하고 UID를 추출합니다.
                    if (!string.IsNullOrEmpty(userData) && userData != "[]" && userData.ToLower() != "null")
                    {
                        Debug.Log("[TutorialPage1Controller] 방 상태 USING 및 유저 정보 확인됨. 개별 FetchData를 수행합니다.");
                        emptyUserStartTime = -1f; 

                        // 응답 형태(예: uid1,uid2,idx)에서 첫 번째 UID를 안전하게 분리
                        string[] parts = userData.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        
                        if (parts.Length >= 1)
                        {
                            string uidLeft = parts[0].Trim();

                            if (apiManager)
                            {   
                                bool fetchSuccess = false;
                                bool fetchFaulted = false;

                                // 3. 서버 측 자체 FetchData 수행
                                using (var fetchCts = new CancellationTokenSource())
                                {
                                    fetchCts.CancelAfter(TimeSpan.FromSeconds(25));
                                    yield return apiManager.FetchDataAsync(uidLeft, fetchCts.Token)
                                                           .ToCoroutine(
                                                                r => fetchSuccess = r, 
                                                                ex => { fetchFaulted = true; }
                                                            );
                                }

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
                            
                            // 4. 클라이언트에게 UID를 전달하고 클라이언트의 통신 완료를 대기합니다.
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
                            
                            yield break; // 폴링 코루틴 완전 종료
                        }
                    }
                    else
                    {
                        // Why: USING 상태이나 유저 데이터가 비어있는 시점부터 타이머를 측정합니다.
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
        /// 클라이언트 통신 응답 대기 시간을 초과할 경우 초기화를 진행하는 안전장치.
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
        /// 클라이언트에게 귀환 명령을 내리고 1초 대기 후 본인도 타이틀로 돌아가는 시퀀스 코루틴.
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
        /// 강제 진행 시 빈 페이로드를 전송합니다.
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

        private async UniTaskVoid ProcessClientFetchAsync(string uidToFetch, CancellationToken token)
        {
            try
            {
                // 타임아웃을 서버(15초)보다 짧은 12초로 설정하여 늦은 ACK 전송을 방지함
                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(12.0f));
                    
                    bool success = await apiManager.FetchDataAsync(uidToFetch, timeoutCts.Token);
                    
                    // Why: 데이터 수신 성공이 확인된 후 서버에 준비 완료 ACK 전송
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

        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null)
            {
                // 1. [클라이언트 측] 서버가 데이터 조회를 지시함
                if (msg.command == "REQUEST_CLIENT_FETCH")
                {
                    if (TcpManager.Instance && !TcpManager.Instance.IsServer && apiManager && !string.IsNullOrEmpty(msg.payload) && _pageCts != null)
                    {
                        string uidToFetch = msg.payload;
                        ProcessClientFetchAsync(uidToFetch, _pageCts.Token).Forget();
                    }
                }
                // 2. [서버 측] 클라이언트가 데이터 조회를 마치고 준비 완료 응답을 보냄
                else if (msg.command == "CLIENT_FETCH_ACK")
                {
                    if (TcpManager.Instance && TcpManager.Instance.IsServer && _isWaitingForClientFetch)
                    {
                        // Why: 요청했던 UID와 응답으로 돌아온 식별자가 동일한지 검증함
                        if (msg.payload == _pendingClientUid)
                        {
                            _isWaitingForClientFetch = false;
                            _pendingClientUid = string.Empty;
                            
                            if (_clientFetchTimeoutCoroutine != null)
                            {
                                StopCoroutine(_clientFetchTimeoutCoroutine);
                                _clientFetchTimeoutCoroutine = null;
                            }
                            OnConfirmInput(); // 최종 출발 명령(PAGE1_COMPLETE) 하달 및 씬 넘김
                        }
                        else
                        {
                            Debug.LogWarning($"[TutorialPage1Controller] 잘못된 클라이언트 Fetch ACK 수신. Expected: {_pendingClientUid}, Received: {msg.payload}");
                        }
                    }
                }
                // 3. [양쪽 모두] 최종 동시 전환 명령 수신
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
        /// 완료 신호를 발생시켜 매니저의 TransitionToNext를 트리거합니다.
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

        public override void OnExit()
        {
            base.OnExit();
            
            if (SoundManager.Instance)
            {
                SoundManager.Instance.StopBGM();
                SoundManager.Instance.PlayBGM("MainBGM");                
            }
            
            _isPageActive = false;

            // Why: 페이지를 벗어날 때 실행 중인 백그라운드 Fetch 작업을 취소함
            if (_pageCts != null)
            {
                _pageCts.Cancel();
                _pageCts.Dispose();
                _pageCts = null;
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