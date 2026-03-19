using System;
using System.Collections;
using My.Scripts.Data;
using My.Scripts.Global;
using My.Scripts.Network;
using My.Scripts.Hardware; 
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks; 
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    public class Page_Question : GamePage
    {
        private enum Phase { None, Holding, CountingDown, Completed }

        [Header("Dynamic UI Components")]
        [SerializeField] private Text textQuestion;
        [SerializeField] private Text textDescription;
        
        [Header("Answers UI")]
        [SerializeField] private Text textAnswer1;
        [SerializeField] private Text textAnswer2;
        [SerializeField] private Text textAnswer3;
        [SerializeField] private Text textAnswer4;
        [SerializeField] private Text textAnswer5;

        [Header("Answer Objects (Cg)")]
        [SerializeField] private GameObject cgA;
        [SerializeField] private GameObject cgB;
        [SerializeField] private GameObject cgC;
        [SerializeField] private GameObject cgD;
        [SerializeField] private GameObject cgE;

        [Header("Answer Background Images")]
        [SerializeField] private Image imgAnswer1;
        [SerializeField] private Image imgAnswer2;
        [SerializeField] private Image imgAnswer3;
        [SerializeField] private Image imgAnswer4;
        [SerializeField] private Image imgAnswer5;
        
        [Header("Next Phase UI")]
        [SerializeField] private CanvasGroup legoArrowCg;
        
        [Header("Animation & Font Settings")]
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float holdDuration = 3.0f;
        [SerializeField] private Font countdownFont;
        
        private readonly Vector2 _targetPosition = new Vector2(0f, -160f);

        private CommonQuestionPageData _cachedData; 
        private string _syncCommand = "DEFAULT_QUESTION_COMPLETE";
        private Phase _currentPhase = Phase.None;
        private int _selectedIndex = -1;
        private bool _isFirstSelectionDone = false;
        private Coroutine _sequenceCoroutine;
        
        private Page_Background _background;
        private string _progressText;
        
        private bool _isAnimating = false;
        private bool _canAcceptInput = false;

        // 어드레서블 메모리 관리를 위한 핸들 변수
        private AsyncOperationHandle<Sprite> _spriteHandle;
        
        public int SelectedIndex => _selectedIndex;

        public void SetSyncCommand(string command)
        {
            _syncCommand = command;
        }

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

        /// <summary>
        /// 매니저에서 전달받은 새로운 스프라이트 배열로 5개의 보기 배경 이미지를 교체함.
        /// </summary>
        public void ChangeAnswerImages(Sprite[] newSprites)
        {
            if (newSprites == null) return;

            Image[] targetImages = new Image[] { imgAnswer1, imgAnswer2, imgAnswer3, imgAnswer4, imgAnswer5 };
            
            for (int i = 0; i < 5; i++)
            {
                if (i < newSprites.Length && newSprites[i] && targetImages[i])
                {
                    targetImages[i].sprite = newSprites[i];
                }
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            if (_background && !string.IsNullOrEmpty(_progressText))
            {
                _background.SetQuestionText(_progressText);
            }
            
            _currentPhase = Phase.None;
            _isFirstSelectionDone = false;
            _selectedIndex = -1;
            _isAnimating = false; 
            _canAcceptInput = false; 

            if (legoArrowCg) legoArrowCg.alpha = 0f;

            ApplyDataToUI();

            // JSON 설정값에 따른 동적 이미지를 비동기로 불러와 씌움
            LoadDynamicImageAsync().Forget();

            if (RfidManager.Instance)
            {
                RfidManager.Instance.onCardRead += OnCardRecognized;
            }

            StartCoroutine(InputDelayRoutine());
            StartAutoReadLoop().Forget();
        }

        public override void OnExit()
        {
            base.OnExit();

            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }

            if (RfidManager.Instance)
            {
                RfidManager.Instance.onCardRead -= OnCardRecognized;
            }

            // 페이지 전환 시 불필요한 메모리 해제
            ReleaseDynamicImage();
        }

        /// <summary>
        /// JSON에 등록된 imageKey를 확인하여, '{API}' 태그를 현재 유저의 아바타 값으로 치환 후 이미지를 로드함.
        /// Why: 스크립트 수정 없이 JSON 설정만으로 유저 속성에 맞는 이미지를 동적으로 띄우기 위함.
        /// </summary>
        private async UniTaskVoid LoadDynamicImageAsync()
        {
            if (_cachedData == null || _cachedData.questionSetting == null) return;
            
            string key = _cachedData.questionSetting.imageKey;
            
            // imageKey가 지정되지 않은 경우(예: 매니저가 직접 넣어주는 Step1_Q2) 건너뜀
            if (string.IsNullOrEmpty(key)) return; 

            // '{API}' 치환 로직
            if (key.Contains("{API}"))
            {
                string apiValue = GameManager.Instance ? GameManager.Instance.CartridgeKey : "A";
                key = key.Replace("{API}", apiValue);
            }

            ReleaseDynamicImage(); 

            try
            {
                _spriteHandle = Addressables.LoadAssetAsync<Sprite>(key);
                Sprite loadedSprite = await _spriteHandle.ToUniTask();

                if (loadedSprite)
                {
                    // 로드한 1장의 이미지를 5개의 모든 보기 버튼에 동일하게 적용함
                    Sprite[] spritesToApply = new Sprite[] { loadedSprite, loadedSprite, loadedSprite, loadedSprite, loadedSprite };
                    ChangeAnswerImages(spritesToApply);
                    
                    UnityEngine.Debug.Log($"[Page_Question] 동적 이미지 로드 성공 및 적용 완료 (Key: {key})");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Page_Question] 동적 이미지 로드 실패 (Key: {key}): {e.Message}");
            }
        }

        /// <summary>
        /// 어드레서블 로드에 사용된 리소스를 안전하게 반환함.
        /// </summary>
        private void ReleaseDynamicImage()
        {
            if (_spriteHandle.IsValid())
            {
                Addressables.Release(_spriteHandle);
            }
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

        private async UniTaskVoid StartAutoReadLoop()
        {
            while (_currentPhase != Phase.Completed && !this.GetCancellationTokenOnDestroy().IsCancellationRequested)
            {
                if (_canAcceptInput && !_isAnimating)
                {
                    if (RfidManager.Instance) RfidManager.Instance.TryReadCard().Forget();
                }
                await UniTask.Delay(TimeSpan.FromSeconds(1.0f), delayTiming: PlayerLoopTiming.Update);
            }
        }

        private IEnumerator InputDelayRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            _canAcceptInput = true;
        }

        private void Update()
        {
            if (_currentPhase == Phase.Completed || _isAnimating || !_canAcceptInput) return;

            KeyCode pressed = GetCurrentValidKeyDown();
            if (pressed != KeyCode.None)
            {
                int category = pressed - KeyCode.Alpha0;
                ProcessInput(category);
            }
        }

        private KeyCode GetCurrentValidKeyDown()
        {
            KeyCode[] keysToCheck = new KeyCode[] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };
            foreach (KeyCode key in keysToCheck)
            {
                if (Input.GetKeyDown(key)) return key;
            }
            return KeyCode.None;
        }

        private void OnCardRecognized(string uid, int category)
        {
            if (_currentPhase == Phase.Completed || _isAnimating || !_canAcceptInput || category == 0) return;
            ProcessInput(category);
        }

        private void ProcessInput(int category)
        {
            if (_selectedIndex == category) return; 

            _selectedIndex = category;

            if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);

            if (_currentPhase == Phase.CountingDown)
            {
                _sequenceCoroutine = StartCoroutine(InterruptedCountdownRoutine(category));
            }
            else
            {   
                _sequenceCoroutine = StartCoroutine(SelectionSequenceRoutine(category));
            }
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

                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / fadeDuration;

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
                        if (rt) rt.anchoredPosition = _targetPosition;
                    }
                }

                elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / fadeDuration;

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

                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / fadeDuration;

                    for (int i = 0; i < 5; i++)
                    {
                        if (cgs[i]) cgs[i].alpha = Mathf.Lerp(startAlphas[i], 0f, t);
                    }
                    yield return null;
                }

                for (int i = 0; i < 5; i++)
                {
                    if (cgs[i]) cgs[i].alpha = 0f;
                }

                elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / fadeDuration;

                    if (targetCg) targetCg.alpha = Mathf.Lerp(0f, 1f, t);
                    yield return null;
                }

                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("레고_3");
            }

            _isAnimating = false; 

            yield return CoroutineData.GetWaitForSeconds(holdDuration);

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

            while (fadeOutElapsed < fadeDuration)
            {
                fadeOutElapsed += Time.deltaTime;
                float t = fadeOutElapsed / fadeDuration;

                if (targetCg) targetCg.alpha = Mathf.Lerp(targetFinalStart, 0f, t);
                yield return null;
            }
            if (targetCg) targetCg.alpha = 0f;

            float fadeInElapsed = 0f;
            while (fadeInElapsed < fadeDuration)
            {
                fadeInElapsed += Time.deltaTime;
                float t = fadeInElapsed / fadeDuration;

                if (legoArrowCg) legoArrowCg.alpha = Mathf.Lerp(0f, 1f, t);
                yield return null;
            }
            if (legoArrowCg) legoArrowCg.alpha = 1f;
            
            _isAnimating = false;

            float totalFadeTime = fadeDuration * 2f;
            if (totalFadeTime < 1.0f)
            {
                yield return CoroutineData.GetWaitForSeconds(1.0f - totalFadeTime);
            }

            for (int i = 4; i >= 1; i--)
            {
                if (textQuestion) textQuestion.text = i.ToString();
                if (i <= 1) _canAcceptInput = false;
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            _currentPhase = Phase.Completed;
            CompletePage();
        }

        private IEnumerator InterruptedCountdownRoutine(int index)
        {
            _isAnimating = true;
            _currentPhase = Phase.Holding; 
            
            yield return CoroutineData.GetWaitForSeconds(1.0f); 

            GameObject[] cgObjects = new GameObject[] { cgA, cgB, cgC, cgD, cgE };
            CanvasGroup[] cgs = new CanvasGroup[5];
            for (int i = 0; i < 5; i++) cgs[i] = GetOrAddCanvasGroup(cgObjects[i]);
            
            for (int i = 0; i < 5; i++)
            {
                if (cgs[i]) cgs[i].alpha = 0f;
            }

            if (cgObjects[index - 1])
            {
                RectTransform rt = cgObjects[index - 1].GetComponent<RectTransform>();
                if (rt) rt.anchoredPosition = _targetPosition;
            }

            if (legoArrowCg) legoArrowCg.alpha = 1f;

            if (textQuestion && countdownFont) textQuestion.font = countdownFont;

            if (SoundManager.Instance)
            {
                SoundManager.Instance.StopSFX();
                SoundManager.Instance.PlaySFX("공통_10_5초");
            }

            _isAnimating = false;
            _currentPhase = Phase.CountingDown; 

            for (int i = 5; i >= 1; i--)
            {
                if (textQuestion) textQuestion.text = i.ToString();
                if (i <= 1) _canAcceptInput = false;
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            _currentPhase = Phase.Completed;
            CompletePage();
        }

        private void CompletePage()
        {
            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
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