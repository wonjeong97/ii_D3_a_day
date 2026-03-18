using System.Collections;
using My.Scripts.Data;
using My.Scripts.Network;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Answer Objects (Cg)")]
        [SerializeField] private GameObject cgA;
        [SerializeField] private GameObject cgB;
        [SerializeField] private GameObject cgC;
        [SerializeField] private GameObject cgD;
        [SerializeField] private GameObject cgE;
        
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
        
        public int SelectedIndex
        {
            get { return _selectedIndex; }
        }

        public void SetSyncCommand(string command)
        {
            _syncCommand = command;
        }

        public override void SetupData(object data)
        {
            CommonQuestionPageData pageData = data as CommonQuestionPageData;
            
            if (pageData != null)
            {
                _cachedData = pageData;
            }
            else
            {
                Debug.LogWarning("[Page_Question] SetupData: 전달된 데이터가 null이거나 형식이 잘못되었습니다.");
            }
        }
        
        public void SetProgressInfo(Page_Background bg, string progress)
        {
            _background = bg;
            _progressText = progress;
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

            if (legoArrowCg) legoArrowCg.alpha = 0f;

            // Why: ApplyDataToUI에서 SetUIText가 호출되며 JSON에 있는 폰트와 서식으로 다시 세팅해줌
            ApplyDataToUI();
        }

        public override void OnExit()
        {
            base.OnExit();

            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
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
            else
            {
                Debug.LogWarning("[Page_Question] ApplyDataToUI: questionSetting 데이터가 없습니다.");
            }

            SetUIText(textDescription, _cachedData.textDescription);
        }

        private void Update()
        {
            if (_currentPhase == Phase.Completed) return;

            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

            bool canSkip = isServer;

#if UNITY_EDITOR
            canSkip = true;
#endif

            if (canSkip)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SelectAnswer(1);
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SelectAnswer(2);
                else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SelectAnswer(3);
                else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) SelectAnswer(4);
                else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) SelectAnswer(5);
            }
        }

        private void SelectAnswer(int index)
        {
            if (_selectedIndex == index) return;
            _selectedIndex = index;

            if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);

            if (_currentPhase == Phase.CountingDown)
            {
                _sequenceCoroutine = StartCoroutine(InterruptedCountdownRoutine(index));
            }
            else
            {
                _sequenceCoroutine = StartCoroutine(SelectionSequenceRoutine(index));
            }
        }

        private IEnumerator SelectionSequenceRoutine(int index)
        {
            _currentPhase = Phase.Holding;
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

                // 텍스트 뿐만 아니라 선택 후의 서식 정보도 일괄 덮어씌움
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
            }

            yield return new WaitForSeconds(holdDuration);

            _currentPhase = Phase.CountingDown;

            // 대기 텍스트 서식 적용
            SetUIText(textDescription, _cachedData.textWait);

            if (textQuestion)
            {
                if (countdownFont) textQuestion.font = countdownFont;
                textQuestion.text = "5";
            }

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

            float totalFadeTime = fadeDuration * 2f;
            if (totalFadeTime < 1.0f)
            {
                yield return new WaitForSeconds(1.0f - totalFadeTime);
            }

            for (int i = 4; i >= 1; i--)
            {
                if (textQuestion) textQuestion.text = i.ToString();
                yield return new WaitForSeconds(1.0f);
            }

            _currentPhase = Phase.Completed;
            CompletePage();
        }

        private IEnumerator InterruptedCountdownRoutine(int index)
        {
            yield return new WaitForSeconds(1.0f);

            GameObject[] cgObjects = new GameObject[] { cgA, cgB, cgC, cgD, cgE };
            for (int i = 0; i < 5; i++)
            {
                CanvasGroup cg = GetOrAddCanvasGroup(cgObjects[i]);
                if (cg) cg.alpha = 0f;
            }
            if (legoArrowCg) legoArrowCg.alpha = 1f;

            if (textQuestion && countdownFont) textQuestion.font = countdownFont;

            for (int i = 5; i >= 1; i--)
            {
                if (textQuestion) textQuestion.text = i.ToString();
                yield return new WaitForSeconds(1.0f);
            }

            _currentPhase = Phase.Completed;
            CompletePage();
        }

        private void CompletePage()
        {
            if (TcpManager.Instance && TcpManager.Instance.IsServer)
            {
                TcpManager.Instance.SendMessageToTarget(_syncCommand, "");
            }

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