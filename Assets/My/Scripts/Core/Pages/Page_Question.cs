using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Network;
using My.Scripts.Hardware; 
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 질문 및 답변 선택 흐름을 관리하는 페이지 컨트롤러.
    /// RFID 및 키보드 입력을 처리하고 선택에 따른 시각적 연출과 카운트다운을 수행함.
    /// </summary>
    public class Page_Question : GamePage
    {
        private enum Phase
        {
            None,
            Holding,
            CountingDown,
            Completed
        }

        [Header("Dynamic UI Components")]
        [SerializeField] private Text textQuestion;
        [SerializeField] private Text textDescription;
        
        [Header("Answers UI")]
        [SerializeField] private Text textAnswer1;
        [SerializeField] private Text textAnswer2;
        [SerializeField] private Text textAnswer3;
        [SerializeField] private Text textAnswer4;
        [SerializeField] private Text textAnswer5;

        [Header("Answers Images")]
        [SerializeField] private Image imgAnswer1;
        [SerializeField] private Image imgAnswer2;
        [SerializeField] private Image imgAnswer3;
        [SerializeField] private Image imgAnswer4;
        [SerializeField] private Image imgAnswer5;

        [Header("Answer Objects (Cg)")]
        [SerializeField] private GameObject cgA;
        [SerializeField] private GameObject cgB;
        [SerializeField] private GameObject cgC;
        [SerializeField] private GameObject cgD;
        [SerializeField] private GameObject cgE;
        
        [Header("Next Phase UI")]
        [SerializeField] private CanvasGroup legoArrowCg;
        
        [Header("Popup UI")]
        [SerializeField] private CanvasGroup popupCanvasGroup;
        [SerializeField] private Text popupTextUI;

        [Header("Animation & Font Settings")]
        [SerializeField] private Font countdownFont;
        
        private CommonQuestionPageData _cachedData; 
        private Phase _currentPhase = Phase.None;
        private int _selectedIndex = -1;
        private bool _isFirstSelectionDone;
        private Coroutine _sequenceCoroutine;
        
        private Page_Background _background;
        private string _progressText;
        
        private bool _isAnimating;
        private bool _canAcceptInput;
        private bool _hasCompleted;

        private readonly List<AsyncOperationHandle<Sprite>> _loadedImageHandles = new List<AsyncOperationHandle<Sprite>>();
        private bool _skipDefaultCartridgeLoad;
        private CancellationTokenSource _cts;

        private Coroutine _inactivityMonitorCoroutine;
        private Coroutine _popupFadeOutCoroutine;
        
        private GameObject[] _cgObjectsCache;
        private CanvasGroup[] _cgsCache;

        public int SelectedIndex => _selectedIndex;

        public void SetSyncCommand(string command) { }

        /// <summary>
        /// 외부로부터 전달받은 질문 데이터를 메모리에 캐싱함.
        /// </summary>
        /// <param name="data">CommonQuestionPageData 타입의 데이터 객체.</param>
        public override void SetupData(object data)
        {
            CommonQuestionPageData pageData = data as CommonQuestionPageData;
            if (pageData != null) _cachedData = pageData;
        }

        /// <summary>
        /// 배경 UI에 표시될 현재 진행도 정보를 할당함.
        /// </summary>
        public void SetProgressInfo(Page_Background bg, string progress)
        {
            _background = bg;
            _progressText = progress;
        }

        /// <summary>
        /// 페이지 진입 시 연출 요소들을 초기화하고 입력 감지를 시작함.
        /// 매 진입 시 배열을 새로 생성하지 않도록 UI 컴포넌트들을 최초 1회 캐싱함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            if (_background && !string.IsNullOrEmpty(_progressText)) _background.SetQuestionText(_progressText);
            
            _currentPhase = Phase.None;
            _isFirstSelectionDone = false;
            _selectedIndex = -1;
            _isAnimating = false; 
            _canAcceptInput = false; 
            _hasCompleted = false;

            if (_cgObjectsCache == null)
            {
                _cgObjectsCache = new GameObject[] { cgA, cgB, cgC, cgD, cgE };
                _cgsCache = new CanvasGroup[5];
                for (int i = 0; i < 5; i++) _cgsCache[i] = GetOrAddCanvasGroup(_cgObjectsCache[i]);
            }

            if (legoArrowCg) legoArrowCg.alpha = 0f;
            if (popupCanvasGroup) popupCanvasGroup.alpha = 0f;

            if (RfidManager.Instance) RfidManager.Instance.onAnswerReceived += OnRfidAnswerReceived;
            if (TcpManager.Instance) TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;

            ApplyDataToUI();

            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }
            _cts = new CancellationTokenSource();

            if (!_skipDefaultCartridgeLoad)
            {
                LoadAndSetCartridgeImagesAsync(_cts.Token).Forget();
            }

            StartCoroutine(InputDelayRoutine());
        }

        /// <summary>
        /// 페이지 이탈 시 비동기 작업 및 코루틴을 중단하고 메모리를 해제함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();

            if (RfidManager.Instance)
            {
                RfidManager.Instance.StopPolling();
                RfidManager.Instance.onAnswerReceived -= OnRfidAnswerReceived;
            }

            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
            }

            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            ReleaseLoadedImages();
            _skipDefaultCartridgeLoad = false;

            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }

            if (_inactivityMonitorCoroutine != null) StopCoroutine(_inactivityMonitorCoroutine);
            if (_popupFadeOutCoroutine != null) StopCoroutine(_popupFadeOutCoroutine);
        }

        /// <summary>
        /// 세션에 기록된 현재 카트리지 정보를 바탕으로 답변 이미지를 비동기 로드함.
        /// </summary>
        private async UniTaskVoid LoadAndSetCartridgeImagesAsync(CancellationToken token)
        {
            if (!SessionManager.Instance || string.IsNullOrEmpty(SessionManager.Instance.Cartridge)) return;

            string cartridge = SessionManager.Instance.Cartridge.ToLower();
            string legoCartKey = "Lego_cart_" + cartridge; 
            string[] keys = new string[] { legoCartKey, legoCartKey, legoCartKey, legoCartKey, legoCartKey };
            
            await LoadAndSetImagesInternalAsync(keys, token);
        }

        /// <summary>
        /// 특정 테마에 맞춘 답변 이미지 키들을 주입받아 로드를 시작함.
        /// </summary>
        public async UniTask LoadAndSetSpecificImagesAsync(string[] addressableKeys)
        {
            if (addressableKeys == null || addressableKeys.Length < 5) return;
            
            _skipDefaultCartridgeLoad = true;
            
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }
            _cts = new CancellationTokenSource();

            await LoadAndSetImagesInternalAsync(addressableKeys, _cts.Token);
        }

        /// <summary>
        /// 어드레서블 핸들을 통해 이미지들을 로드하고 UI Image 컴포넌트에 할당함.
        /// </summary>
        private async UniTask LoadAndSetImagesInternalAsync(string[] keys, CancellationToken token)
        {
            ReleaseLoadedImages();

            UniTask<Sprite>[] loadTasks = new UniTask<Sprite>[5];

            for (int i = 0; i < 5; i++)
            {
                AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(keys[i]);
                _loadedImageHandles.Add(handle);
                loadTasks[i] = handle.Task.AsUniTask();
            }

            try
            {
                Sprite[] results = await UniTask.WhenAll(loadTasks);
                if (token.IsCancellationRequested) return;

                if (imgAnswer1 && results[0]) imgAnswer1.sprite = results[0];
                if (imgAnswer2 && results[1]) imgAnswer2.sprite = results[1];
                if (imgAnswer3 && results[2]) imgAnswer3.sprite = results[2];
                if (imgAnswer4 && results[3]) imgAnswer4.sprite = results[3];
                if (imgAnswer5 && results[4]) imgAnswer5.sprite = results[4];
            }
            catch (Exception e)
            {
                if (!token.IsCancellationRequested)
                {
                    Debug.LogError($"[Page_Question] 어드레서블 로드 실패: {e.Message}");
                    ReleaseLoadedImages();
                }
            }
        }

        /// <summary>
        /// 로드된 어드레서블 에셋 핸들을 해제하여 메모리 누수를 방지함.
        /// </summary>
        private void ReleaseLoadedImages()
        {
            if (_loadedImageHandles == null || _loadedImageHandles.Count == 0) return;

            foreach (AsyncOperationHandle<Sprite> handle in _loadedImageHandles)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
            _loadedImageHandles.Clear();
        }

        /// <summary>
        /// 캐싱된 질문 및 답변 데이터를 UI 텍스트 컴포넌트에 적용함.
        /// </summary>
        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;
            QuestionSetting qSetting = _cachedData.questionSetting;
            
            if (qSetting != null)
            {
                SetUIText(textQuestion, qSetting.textQuestion);
                SetUIText(textAnswer1, qSetting.textAnswer1);
                SetUIText(textAnswer2, qSetting.textAnswer2);
                SetUIText(textAnswer3, qSetting.textAnswer3);
                SetUIText(textAnswer4, qSetting.textAnswer4);
                SetUIText(textAnswer5, qSetting.textAnswer5);
            }
            SetUIText(textDescription, _cachedData.textDescription);
        }

        /// <summary>
        /// 매 프레임 키보드 디버그 입력을 검사함.
        /// 서버와 클라이언트가 서로 다른 키 세트를 사용하여 로컬 테스트 편의성을 높임.
        /// </summary>
        private void Update()
        {
            if (_currentPhase == Phase.Completed || _isAnimating) return;

            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

            if (_canAcceptInput)
            {
                if (isServer)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1)) SelectAnswer(1);
                    else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectAnswer(2);
                    else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectAnswer(3);
                    else if (Input.GetKeyDown(KeyCode.Alpha4)) SelectAnswer(4);
                    else if (Input.GetKeyDown(KeyCode.Alpha5)) SelectAnswer(5);
                }
                else
                {
                    if (Input.GetKeyDown(KeyCode.Alpha6)) SelectAnswer(1);
                    else if (Input.GetKeyDown(KeyCode.Alpha7)) SelectAnswer(2);
                    else if (Input.GetKeyDown(KeyCode.Alpha8)) SelectAnswer(3);
                    else if (Input.GetKeyDown(KeyCode.Alpha9)) SelectAnswer(4);
                    else if (Input.GetKeyDown(KeyCode.Alpha0)) SelectAnswer(5);
                }
            }
        }

        /// <summary>
        /// 씬 진입 직후 오입력을 방지하기 위해 짧은 지연 시간을 가진 후 입력을 허용함.
        /// </summary>
        private IEnumerator InputDelayRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            _canAcceptInput = true;

            if (RfidManager.Instance) RfidManager.Instance.StartPolling();
            
            if (_inactivityMonitorCoroutine != null) StopCoroutine(_inactivityMonitorCoroutine);
            _inactivityMonitorCoroutine = StartCoroutine(InactivityMonitorRoutine());
        }

        /// <summary>
        /// 무응답 시간을 감지하여 1차 경고 팝업을 띄우고, 최종적으로 타이틀로 복귀시킴.
        /// </summary>
        private IEnumerator InactivityMonitorRoutine()
        {
            // # TODO: 하드코딩된 대기 시간(20s, 10s)을 설정 파일에서 주입받도록 개선할 것.
            yield return CoroutineData.GetWaitForSeconds(20.0f);
            if (_currentPhase == Phase.Completed || _isAnimating || _selectedIndex != -1) yield break;

            if (_cachedData != null && _cachedData.textPopupWarning != null && popupTextUI)
            {
                SetUIText(popupTextUI, _cachedData.textPopupWarning);
            }

            if (popupCanvasGroup) yield return StartCoroutine(FadeCanvasGroupRoutine(popupCanvasGroup, popupCanvasGroup.alpha, 1f, 0.5f));
            yield return CoroutineData.GetWaitForSeconds(3.0f);
            if (popupCanvasGroup) yield return StartCoroutine(FadeCanvasGroupRoutine(popupCanvasGroup, popupCanvasGroup.alpha, 0f, 0.5f));

            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_23");

            yield return CoroutineData.GetWaitForSeconds(10.0f);
            if (_currentPhase == Phase.Completed || _isAnimating || _selectedIndex != -1) yield break;

            if (_cachedData != null && _cachedData.textPopupTimeout != null && popupTextUI)
            {
                SetUIText(popupTextUI, _cachedData.textPopupTimeout);
            }

            if (popupCanvasGroup) yield return StartCoroutine(FadeCanvasGroupRoutine(popupCanvasGroup, popupCanvasGroup.alpha, 1f, 0.5f));
            yield return CoroutineData.GetWaitForSeconds(3.0f);

            if (_currentPhase == Phase.Completed || _isAnimating || _selectedIndex != -1) yield break;
            
            Debug.LogWarning("[Page_Question] 장시간 무응답으로 인한 타이틀 복귀.");

            if (TcpManager.Instance) TcpManager.Instance.SendMessageToTarget("RETURN_TO_TITLE", "");
            if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
        }

        /// <summary>
        /// 상대방 기기로부터 타이틀 복귀 명령을 받았을 때 현재 기기도 동기화하여 복귀함.
        /// </summary>
        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == "RETURN_TO_TITLE")
            {
                if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
            }
        }

        /// <summary>
        /// 캔버스 그룹의 알파값을 조절하여 페이드 연출을 수행함.
        /// </summary>
        private IEnumerator FadeCanvasGroupRoutine(CanvasGroup target, float start, float end, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (target) target.alpha = Mathf.Lerp(start, end, elapsed / duration);
                yield return null;
            }
            if (target) target.alpha = end;
        }

        /// <summary>
        /// RFID 하드웨어로부터 인식된 인덱스 정보를 수신하여 답변을 선택함.
        /// </summary>
        private void OnRfidAnswerReceived(int index)
        {
            if (_currentPhase == Phase.Completed || _isAnimating || !_canAcceptInput) return;
            SelectAnswer(index);
        }

        /// <summary>
        /// 선택된 답변에 따라 애니메이션 시퀀스를 분기 실행함.
        /// 무응답 감지 코루틴을 중단하고 현재 상태(카운트다운 중인지 여부)에 맞는 연출을 수행함.
        /// </summary>
        private void SelectAnswer(int index)
        {
            if (_selectedIndex == index) return;
            _selectedIndex = index;

            if (_inactivityMonitorCoroutine != null) StopCoroutine(_inactivityMonitorCoroutine);
            if (SoundManager.Instance) SoundManager.Instance.StopSFX();
            
            if (popupCanvasGroup && popupCanvasGroup.alpha > 0f)
            {
                if (_popupFadeOutCoroutine != null) StopCoroutine(_popupFadeOutCoroutine);
                _popupFadeOutCoroutine = StartCoroutine(FadeCanvasGroupRoutine(popupCanvasGroup, popupCanvasGroup.alpha, 0f, 0.5f));
            }

            if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);

            if (_currentPhase == Phase.CountingDown) 
                _sequenceCoroutine = StartCoroutine(InterruptedCountdownRoutine(index));
            else 
                _sequenceCoroutine = StartCoroutine(SelectionSequenceRoutine(index));
        }

        /// <summary>
        /// 응답 선택 시 발생하는 UI 연출 및 5초 카운트다운을 수행함.
        /// 최초 선택 시에는 질문 텍스트를 "선택되었습니다"로 변경하고 UI 배치를 재조정함.
        /// </summary>
        /// <param name="index">선택된 답변의 인덱스 번호 (1~5).</param>
        private IEnumerator SelectionSequenceRoutine(int index)
        {
            _currentPhase = Phase.Holding;
            _isAnimating = true; 
            
            CanvasGroup qCg = GetOrAddCanvasGroup(textQuestion);
            CanvasGroup targetCg = _cgsCache[index - 1];

            if (!_isFirstSelectionDone)
            {
                _isFirstSelectionDone = true;
                float elapsed = 0f;
                float qStart = qCg ? qCg.alpha : 1f;
                float[] startAlphas = new float[5];
                for (int i = 0; i < 5; i++) startAlphas[i] = _cgsCache[i] ? _cgsCache[i].alpha : 1f;

                while (elapsed < 0.5f)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / 0.5f;

                    if (qCg) qCg.alpha = Mathf.Lerp(qStart, 0f, t);
                    for (int i = 0; i < 5; i++)
                        if (_cgsCache[i]) _cgsCache[i].alpha = Mathf.Lerp(startAlphas[i], 0f, t);
                    
                    yield return null;
                }

                SetUIText(textQuestion, _cachedData.textSelected);
                for (int i = 0; i < 5; i++)
                {
                    if (_cgObjectsCache[i])
                    {
                        RectTransform rt = _cgObjectsCache[i].GetComponent<RectTransform>();
                        if (rt) rt.anchoredPosition = new Vector2(0f, -160f);
                    }
                }

                elapsed = 0f;
                while (elapsed < 0.5f)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / 0.5f;

                    if (qCg) qCg.alpha = Mathf.Lerp(0f, 1f, t);
                    if (targetCg) targetCg.alpha = Mathf.Lerp(0f, 1f, t);
                    yield return null;
                }

                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("레고_3");
            }
            else
            {
                float elapsed = 0f;
                float[] startAlphas = new float[5];
                for (int i = 0; i < 5; i++) startAlphas[i] = _cgsCache[i] ? _cgsCache[i].alpha : 0f;

                while (elapsed < 0.5f)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / 0.5f;

                    for (int i = 0; i < 5; i++)
                        if (_cgsCache[i]) _cgsCache[i].alpha = Mathf.Lerp(startAlphas[i], 0f, t);
                    
                    yield return null;
                }

                for (int i = 0; i < 5; i++) if (_cgsCache[i]) _cgsCache[i].alpha = 0f;

                elapsed = 0f;
                while (elapsed < 0.5f)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / 0.5f;

                    if (targetCg) targetCg.alpha = Mathf.Lerp(0f, 1f, t);
                    yield return null;
                }

                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("레고_3");
            }

            _isAnimating = false; 
            yield return CoroutineData.GetWaitForSeconds(3.0f);
            _currentPhase = Phase.CountingDown;

            SetUIText(textDescription, _cachedData.textWait);

            if (textQuestion)
            {
                if (countdownFont) textQuestion.font = countdownFont;
                textQuestion.text = "5";
            }

            if (SoundManager.Instance)
            {
                SoundManager.Instance.StopSFX();
                SoundManager.Instance.PlaySFX("공통_10_5초");
            }

            _isAnimating = true;
            float fadeOutElapsed = 0f;
            float targetFinalStart = targetCg ? targetCg.alpha : 1f;

            while (fadeOutElapsed < 0.5f)
            {
                fadeOutElapsed += Time.deltaTime;
                float t = fadeOutElapsed / 0.5f;

                if (targetCg) targetCg.alpha = Mathf.Lerp(targetFinalStart, 0f, t);
                yield return null;
            }
            if (targetCg) targetCg.alpha = 0f;

            float fadeInElapsed = 0f;
            while (fadeInElapsed < 0.5f)
            {
                fadeInElapsed += Time.deltaTime;
                float t = fadeInElapsed / 0.5f;

                if (legoArrowCg) legoArrowCg.alpha = Mathf.Lerp(0f, 1f, t);
                yield return null;
            }
            if (legoArrowCg) legoArrowCg.alpha = 1f;
            
            _isAnimating = false;

            for (int i = 4; i >= 1; i--)
            {
                if (textQuestion) textQuestion.text = i.ToString();
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            _currentPhase = Phase.Completed;
            CompleteOnce();
        }

        /// <summary>
        /// 진행 중이던 카운트다운을 즉시 중단하고 새로운 선택지에 대한 카운트다운을 처음부터 다시 시작함.
        /// 유저가 선택을 번복했을 때 응답성을 확보하기 위함.
        /// </summary>
        /// <param name="index">새로 선택된 답변의 인덱스 번호.</param>
        private IEnumerator InterruptedCountdownRoutine(int index)
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            for (int i = 0; i < 5; i++)
            {
                if (_cgsCache[i]) _cgsCache[i].alpha = 0f;
            }
            if (legoArrowCg) legoArrowCg.alpha = 1f;

            if (textQuestion && countdownFont) textQuestion.font = countdownFont;

            if (SoundManager.Instance)
            {
                SoundManager.Instance.StopSFX();
                SoundManager.Instance.PlaySFX("공통_10_5초");
            }

            for (int i = 5; i >= 1; i--)
            {
                if (textQuestion) textQuestion.text = i.ToString();
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            _currentPhase = Phase.Completed;
            CompleteOnce();
        }

        /// <summary>
        /// 답변 확정 처리를 수행하고 매니저에게 페이지 완료 이벤트를 전달함.
        /// </summary>
        private void CompleteOnce()
        {
            if (_hasCompleted) return;
            _hasCompleted = true;

            if (RfidManager.Instance) RfidManager.Instance.StopPolling();

            if (onStepComplete != null) onStepComplete.Invoke(0);
        }

        /// <summary>
        /// 컴포넌트에서 CanvasGroup을 찾아 반환하거나 없을 경우 새로 생성하여 반환함.
        /// </summary>
        private CanvasGroup GetOrAddCanvasGroup(Component comp)
        {
            if (!comp) return null;
            CanvasGroup cg = comp.GetComponent<CanvasGroup>();
            if (!cg) cg = comp.gameObject.AddComponent<CanvasGroup>();
            return cg;
        }

        /// <summary>
        /// 게임 오브젝트에서 CanvasGroup을 찾아 반환하거나 없을 경우 새로 생성하여 반환함.
        /// </summary>
        private CanvasGroup GetOrAddCanvasGroup(GameObject obj)
        {
            if (!obj) return null;
            CanvasGroup cg = obj.GetComponent<CanvasGroup>();
            if (!cg) cg = obj.AddComponent<CanvasGroup>();
            return cg;
        }
    }
}