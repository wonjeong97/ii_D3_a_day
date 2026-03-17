using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Network;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;

namespace My.Scripts._03_Step1.Pages
{
    [Serializable]
    public class Step1Page2Data
    {
        public TextSetting textQuestion;
        public TextSetting textSelected;
        public TextSetting textDescription;
        public TextSetting textWait;
        public TextSetting textAnswer1;
        public TextSetting textAnswer2;
        public TextSetting textAnswer3;
        public TextSetting textAnswer4;
        public TextSetting textAnswer5;
    }

    /// <summary>
    /// Step1의 두 번째 본문 페이지 컨트롤러.
    /// 선택된 타겟 Cg가 먼저 사라지고 화살표가 이어서 나타나는 순차적 페이드 연출을 수행함.
    /// </summary>
    public class Step1Page2Controller : GamePage
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

        private Step1Page2Data _cachedData;
        private Phase _currentPhase = Phase.None;
        private int _selectedIndex = -1;
        private bool _isFirstSelectionDone = false;
        private Coroutine _sequenceCoroutine;
        private Font _originalQuestionFont;

        protected override void Awake()
        {
            base.Awake();
            
            if (textQuestion)
            {
                _originalQuestionFont = textQuestion.font;
            }
        }

        public override void SetupData(object data)
        {
            Step1Page2Data pageData = data as Step1Page2Data;
            
            // 일반 C# 객체이므로 명시적 null 검사 수행
            if (pageData != null)
            {
                _cachedData = pageData;
            }
            else
            {
                Debug.LogWarning("[Step1Page2Controller] SetupData: 전달된 데이터가 null입니다.");
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _currentPhase = Phase.None;
            _isFirstSelectionDone = false;
            _selectedIndex = -1;

            if (legoArrowCg) legoArrowCg.alpha = 0f;

            if (textQuestion && _originalQuestionFont)
            {
                textQuestion.font = _originalQuestionFont;
            }

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

            if (textQuestion && _cachedData.textQuestion != null) textQuestion.text = _cachedData.textQuestion.text;
            if (textDescription && _cachedData.textDescription != null) textDescription.text = _cachedData.textDescription.text;

            if (textAnswer1 && _cachedData.textAnswer1 != null) textAnswer1.text = _cachedData.textAnswer1.text;
            if (textAnswer2 && _cachedData.textAnswer2 != null) textAnswer2.text = _cachedData.textAnswer2.text;
            if (textAnswer3 && _cachedData.textAnswer3 != null) textAnswer3.text = _cachedData.textAnswer3.text;
            if (textAnswer4 && _cachedData.textAnswer4 != null) textAnswer4.text = _cachedData.textAnswer4.text;
            if (textAnswer5 && _cachedData.textAnswer5 != null) textAnswer5.text = _cachedData.textAnswer5.text;
        }

        private void Update()
        {
            if (_currentPhase == Phase.Completed) return;

            bool isServer = false;
            if (TcpManager.Instance)
            {
                isServer = TcpManager.Instance.IsServer;
            }

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

                if (textQuestion && _cachedData != null && _cachedData.textSelected != null)
                {
                    textQuestion.text = _cachedData.textSelected.text;
                }

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

            if (textDescription && _cachedData != null && _cachedData.textWait != null)
            {
                textDescription.text = _cachedData.textWait.text;
            }

            if (textQuestion)
            {
                if (countdownFont) textQuestion.font = countdownFont;
                textQuestion.text = "5";
            }

            // Why: 기존 타겟 Cg를 먼저 0.5초간 페이드 아웃시킴
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

            // Why: 타겟이 완전히 사라진 후 화살표를 이어서 0.5초간 페이드 인 시킴
            float fadeInElapsed = 0f;
            while (fadeInElapsed < fadeDuration)
            {
                fadeInElapsed += Time.deltaTime;
                float t = fadeInElapsed / fadeDuration;

                if (legoArrowCg) legoArrowCg.alpha = Mathf.Lerp(0f, 1f, t);
                yield return null;
            }
            if (legoArrowCg) legoArrowCg.alpha = 1f;

            // Why: 순차 페이드의 총 소요시간(fadeDuration * 2)이 1초가 안 될 경우 남은 1초 간격을 맞춰줌
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
                TcpManager.Instance.SendMessageToTarget("STEP1_PAGE2_COMPLETE", "");
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
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
            }
        }

        private void OnDisable()
        {
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
            }
        }

        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == "STEP1_PAGE2_COMPLETE" && _currentPhase != Phase.Completed)
            {
                _currentPhase = Phase.Completed;
                
                if (onStepComplete != null)
                {
                    onStepComplete.Invoke(0);
                }
            }
        }
    }
}