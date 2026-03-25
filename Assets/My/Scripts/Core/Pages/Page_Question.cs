using System.Collections;
using System.Collections.Generic;
using System.Threading;
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

        public int SelectedIndex => _selectedIndex;

        // 매니저 호환용 더미 메서드 (더 이상 동기화하지 않음)
        public void SetSyncCommand(string command) { }

        public override void SetupData(object data)
        {
            CommonQuestionPageData pageData = data as CommonQuestionPageData;
            if (pageData != null) _cachedData = pageData;
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
            _hasCompleted = false;

            if (legoArrowCg) legoArrowCg.alpha = 0f;

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

        public override void OnExit()
        {
            base.OnExit();

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
        }

        private async UniTaskVoid LoadAndSetCartridgeImagesAsync(CancellationToken token)
        {
            if (!SessionManager.Instance || string.IsNullOrEmpty(SessionManager.Instance.Cartridge)) return;

            string cartridge = SessionManager.Instance.Cartridge.ToLower();
            string legoCartKey = $"Lego_cart_{cartridge}"; 
            string[] keys = new string[] { legoCartKey, legoCartKey, legoCartKey, legoCartKey, legoCartKey };
            
            await LoadAndSetImagesInternalAsync(keys, token);
        }

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
            catch (System.Exception e)
            {
                if (!token.IsCancellationRequested)
                {
                    Debug.LogError($"[Page_Question] 어드레서블 로드 실패. 키: {string.Join(", ", keys)}, 에러: {e.Message}");
                    ReleaseLoadedImages();
                }
            }
        }

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

            if (_canAcceptInput)
            {
                if (isServer)
                {
                    // 서버는 1~5번 키 사용
                    if (Input.GetKeyDown(KeyCode.Alpha1)) SelectAnswer(1);
                    else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectAnswer(2);
                    else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectAnswer(3);
                    else if (Input.GetKeyDown(KeyCode.Alpha4)) SelectAnswer(4);
                    else if (Input.GetKeyDown(KeyCode.Alpha5)) SelectAnswer(5);
                }
                else
                {
                    // 클라이언트는 6~0번 키 사용하도록 수정
                    if (Input.GetKeyDown(KeyCode.Alpha6)) SelectAnswer(1);
                    else if (Input.GetKeyDown(KeyCode.Alpha7)) SelectAnswer(2);
                    else if (Input.GetKeyDown(KeyCode.Alpha8)) SelectAnswer(3);
                    else if (Input.GetKeyDown(KeyCode.Alpha9)) SelectAnswer(4);
                    else if (Input.GetKeyDown(KeyCode.Alpha0)) SelectAnswer(5);
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
                        if (cgs[i]) cgs[i].alpha = Mathf.Lerp(startAlphas[i], 0f, t);
                    
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
                        if (cgs[i]) cgs[i].alpha = Mathf.Lerp(startAlphas[i], 0f, t);
                    
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
            CompleteOnce();
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
            CompleteOnce();
        }

        private void CompleteOnce()
        {
            if (_hasCompleted) return;
            _hasCompleted = true;

            // 상대방 신호를 기다리거나 보내지 않고 독립적으로 씬 이동
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
    }
}