using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Network;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
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
        
        [Header("Animation & Font Settings")]
        [SerializeField] private Font countdownFont;
        
        private CommonQuestionPageData _cachedData; 
        private string _syncCommand = "DEFAULT_QUESTION_COMPLETE";
        private Phase _currentPhase = Phase.None;
        private int _selectedIndex = -1;
        private bool _isFirstSelectionDone;
        private Coroutine _sequenceCoroutine;
        
        private Page_Background _background;
        private string _progressText;
        
        private bool _isAnimating;
        private bool _canAcceptInput;

        // 비동기 로드 핸들들을 관리하는 리스트. 페이지를 나갈 때 메모리를 해제하기 위함.
        private readonly List<AsyncOperationHandle<Sprite>> _loadedImageHandles = new List<AsyncOperationHandle<Sprite>>();
        
        // Step1Manager 등 외부에서 특정 이미지를 로드하라고 요청했을 때 기본 카트리지 로드를 건너뛰기 위한 플래그.
        private bool _skipDefaultCartridgeLoad;

        public int SelectedIndex => _selectedIndex;

        public void SetSyncCommand(string command)
        {
            _syncCommand = command;
        }

        public override void SetupData(object data)
        {
            CommonQuestionPageData pageData = data as CommonQuestionPageData;
            
            if (pageData != null) _cachedData = pageData;
            else Debug.LogWarning("[Page_Question] SetupData: 전달된 데이터가 null이거나 형식이 잘못되었습니다.");
        }

        public void SetProgressInfo(Page_Background bg, string progress)
        {
            _background = bg;
            _progressText = progress;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            if (_background && !string.IsNullOrEmpty(_progressText)) _background.SetQuestionText(_progressText);
            
            _currentPhase = Phase.None;
            _isFirstSelectionDone = false;
            _selectedIndex = -1;
            _isAnimating = false; 
            _canAcceptInput = false; 

            if (legoArrowCg) legoArrowCg.alpha = 0f;

            ApplyDataToUI();

            // Why: 외부 매니저(Step1 등)에서 특정 이미지를 미리 로드하라고 지시하지 않은 경우에만 현재 카트리지 기반 기본 이미지를 로드함.
            if (!_skipDefaultCartridgeLoad)
            {
                LoadAndSetCartridgeImagesAsync().Forget();
            }

            StartCoroutine(InputDelayRoutine());
        }

        public override void OnExit()
        {
            base.OnExit();

            // 페이지를 나갈 때 비동기 로드 중인 연산을 취소하고 메모리를 해제함.
            ReleaseLoadedImages();
            
            // 플래그 초기화
            _skipDefaultCartridgeLoad = false;

            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }
        }

        /// <summary>
        /// 현재 세션에 저장된 카트리지 정보를 기반으로 어드레서블에서 5개의 기본 보기를 비동기로 로드하여 세팅함.
        /// </summary>
        private async UniTaskVoid LoadAndSetCartridgeImagesAsync()
        {
            if (!SessionManager.Instance || string.IsNullOrEmpty(SessionManager.Instance.Cartridge)) return;

            string cartridge = SessionManager.Instance.Cartridge.ToLower();
            string legoCartKey = $"Lego_cart_{cartridge}"; // 예: Lego_cart_a

            // 어드레서블에서 한 장의 이미지를 여러 버튼에 복사해서 쓰는 구조로 가정함.
            string[] keys = new string[] { legoCartKey, legoCartKey, legoCartKey, legoCartKey, legoCartKey };
            
            await LoadAndSetImagesInternalAsync(keys);
        }

        /// <summary>
        /// 외부(Step1Manager)에서 5개의 구체적인 어드레서블 키 배열을 넘겨받아 이미지를 비동기로 교체함.
        /// Why: Step1 Q1 답변에 따른 Q2 예외 이미지를 로드하기 위해 호출됨.
        /// </summary>
        public async UniTask LoadAndSetSpecificImagesAsync(string[] addressableKeys)
        {
            if (addressableKeys == null || addressableKeys.Length < 5) return;
            
            // 기본 카트리지 로드 로직을 건너뛰도록 플래그를 설정함.
            _skipDefaultCartridgeLoad = true;
            
            await LoadAndSetImagesInternalAsync(addressableKeys);
        }

        /// <summary>
        /// 키 배열을 받아 이전 메모리를 해제하고 실제 비동기 로드 및 UI 세팅을 수행함.
        /// </summary>
        private async UniTask LoadAndSetImagesInternalAsync(string[] keys)
        {
            // 새로운 로드를 시작하기 전에 이전 페이지 등에서 로드했던 핸들들을 정리함.
            ReleaseLoadedImages();

            // 5개의 비동기 작업을 담을 배열 생성
            UniTask<Sprite>[] loadTasks = new UniTask<Sprite>[5];

            for (int i = 0; i < 5; i++)
            {
                AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(keys[i]);
                _loadedImageHandles.Add(handle); // 메모리 관리 리스트에 추적 등록
                
                // Why: 배열 전체에 ToUniTask()를 적용할 수 없고 확장 패키지 누락 시 에러가 발생하므로, 네이티브 Task를 AsUniTask()로 안전하게 변환함.
                loadTasks[i] = handle.Task.AsUniTask();
            }

            try
            {
                // 5개의 로드가 모두 완료될 때까지 병렬로 대기함.
                Sprite[] results = await UniTask.WhenAll(loadTasks);

                // 로드된 스프라이트를 UI에 할당함.
                if (imgAnswer1 && results[0]) imgAnswer1.sprite = results[0];
                if (imgAnswer2 && results[1]) imgAnswer2.sprite = results[1];
                if (imgAnswer3 && results[2]) imgAnswer3.sprite = results[2];
                if (imgAnswer4 && results[3]) imgAnswer4.sprite = results[3];
                if (imgAnswer5 && results[4]) imgAnswer5.sprite = results[4];
            }
            catch (System.Exception e)
            {
                // 로드 실패 시 에러 내용을 출력하고 핸들을 정리함.
                Debug.LogError($"[Page_Question] 어드레서블 로드 실패. 키: {string.Join(", ", keys)}, 에러: {e.Message}");
                ReleaseLoadedImages();
            }
        }

        /// <summary>
        /// 현재 페이지에서 로드하여 추적 중인 모든 어드레서블 핸들의 메모리를 해제함.
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

        private void Update()
        {
            if (_currentPhase == Phase.Completed || _isAnimating) return;

            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

            bool canSkip = isServer;

#if UNITY_EDITOR
            canSkip = true;
#endif

            if (canSkip)
            {
                if (_canAcceptInput)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1)) SelectAnswer(1);
                    else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectAnswer(2);
                    else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectAnswer(3);
                    else if (Input.GetKeyDown(KeyCode.Alpha4)) SelectAnswer(4);
                    else if (Input.GetKeyDown(KeyCode.Alpha5)) SelectAnswer(5);
                }
            }
        }

        private IEnumerator InputDelayRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            _canAcceptInput = true;
        }

        private void SelectAnswer(int index)
        {
            if (_selectedIndex == index) return;
            _selectedIndex = index;

            if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);

            if (_currentPhase == Phase.CountingDown) _sequenceCoroutine = StartCoroutine(InterruptedCountdownRoutine(index));
            else _sequenceCoroutine = StartCoroutine(SelectionSequenceRoutine(index));
        }

        private IEnumerator SelectionSequenceRoutine(int index)
        {
            _currentPhase = Phase.Holding;
            _isAnimating = true; 
            
            CanvasGroup qCg = GetOrAddCanvasGroup(textQuestion);
            
            GameObject[] cgObjects = new GameObject[] { cgA, cgB, cgC, cgD, cgE };
            CanvasGroup[] cgs = new CanvasGroup[5];
            for (int i = 0; i < 5; i++) cgs[i] = GetOrAddCanvasGroup(cgObjects[i]);

            CanvasGroup targetCg = cgs[index - 1];

            if (!_isFirstSelectionDone)
            {
                _isFirstSelectionDone = true;

                float elapsed = 0f;
                float qStart = qCg ? qCg.alpha : 1f;
                float[] startAlphas = new float[5];
                for (int i = 0; i < 5; i++) startAlphas[i] = cgs[i] ? cgs[i].alpha : 1f;

                while (elapsed < 0.5f)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / 0.5f;

                    if (qCg) qCg.alpha = Mathf.Lerp(qStart, 0f, t);
                    for (int i = 0; i < 5; i++)
                    {
                        if (cgs[i]) cgs[i].alpha = Mathf.Lerp(startAlphas[i], 0f, t);
                    }
                    yield return null;
                }

                SetUIText(textQuestion, _cachedData.textSelected);

                for (int i = 0; i < 5; i++)
                {
                    if (cgObjects[i])
                    {
                        RectTransform rt = cgObjects[i].GetComponent<RectTransform>();
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
                for (int i = 0; i < 5; i++) startAlphas[i] = cgs[i] ? cgs[i].alpha : 0f;

                while (elapsed < 0.5f)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / 0.5f;

                    for (int i = 0; i < 5; i++)
                    {
                        if (cgs[i]) cgs[i].alpha = Mathf.Lerp(startAlphas[i], 0f, t);
                    }
                    yield return null;
                }

                for (int i = 0; i < 5; i++) if (cgs[i]) cgs[i].alpha = 0f;

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
            CompletePage();
        }

        private IEnumerator InterruptedCountdownRoutine(int index)
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            GameObject[] cgObjects = new GameObject[] { cgA, cgB, cgC, cgD, cgE };
            for (int i = 0; i < 5; i++)
            {
                CanvasGroup cg = GetOrAddCanvasGroup(cgObjects[i]);
                if (cg) cg.alpha = 0f;
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
            CompletePage();
        }

        private void CompletePage()
        {
            if (TcpManager.Instance && TcpManager.Instance.IsServer) TcpManager.Instance.SendMessageToTarget(_syncCommand, "");
            if (onStepComplete != null) onStepComplete.Invoke(0);
        }

        private CanvasGroup GetOrAddCanvasGroup(Component comp)
        {
            if (!comp) return null;
            CanvasGroup cg = comp.GetComponent<CanvasGroup>();
            if (!cg) cg = comp.gameObject.AddComponent<CanvasGroup>();
            return cg;
        }

        private CanvasGroup GetOrAddCanvasGroup(GameObject obj)
        {
            if (!obj) return null;
            CanvasGroup cg = obj.GetComponent<CanvasGroup>();
            if (!cg) cg = obj.AddComponent<CanvasGroup>();
            return cg;
        }

        private void OnEnable()
        {
            if (TcpManager.Instance) TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
        }

        private void OnDisable()
        {
            if (TcpManager.Instance) TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
        }

        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == _syncCommand && _currentPhase != Phase.Completed)
            {
                _currentPhase = Phase.Completed;
                if (onStepComplete != null) onStepComplete.Invoke(0);
            }
        }
    }
}