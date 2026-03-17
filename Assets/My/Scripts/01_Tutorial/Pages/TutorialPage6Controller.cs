using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.Utils;
using My.Scripts.Network; // TCP 통신 매니저 접근

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage6Data
    {
        public TextSetting descriptionTextA;
        public TextSetting descriptionTextB;
        public TextSetting cartNameA;
        public TextSetting cartNameB;
        public TextSetting cartNameC;
    }

    /// <summary>
    /// 여섯 번째 튜토리얼 페이지 컨트롤러.
    /// 진입 후 자동으로 1번 카트리지를 선택하며, 내부 UI 페이드 아웃 연출 후 다음 단계로 넘어감.
    /// </summary>
    public class TutorialPage6Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup pageCanvasGroup; 
        [SerializeField] private CanvasGroup cg1CanvasGroup;  
        [SerializeField] private Text descriptionUI; 

        // Why: TCP 네트워크 상태에 따라 동적으로 판단하므로 isPlayer1 변수는 삭제함

        [Header("Cart A")]
        [SerializeField] private CanvasGroup cartACanvas; 
        [SerializeField] private RectTransform cartATransform;
        [SerializeField] private Text cartAText; 

        [Header("Cart B")]
        [SerializeField] private CanvasGroup cartBCanvas; 
        [SerializeField] private RectTransform cartBTransform;
        [SerializeField] private Text cartBText; 

        [Header("Cart C")]
        [SerializeField] private CanvasGroup cartCCanvas; 
        [SerializeField] private RectTransform cartCTransform;
        [SerializeField] private Text cartCText; 

        [Header("Animation Settings")]
        [SerializeField] private float autoSelectDelay = 1.5f; // 자동 선택 대기 시간
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float finalHoldTime = 3.0f;

        private readonly Vector2 _selectedTargetPos = new Vector2(900f, -500f);
        private TutorialPage6Data _cachedData;

        private Coroutine _autoSelectCoroutine;
        private Coroutine _animationCoroutineA;
        private Coroutine _animationCoroutineB;
        private Coroutine _animationCoroutineC;
        
        private bool _isCompleted = false; 

        public override void SetupData(object data)
        {
            TutorialPage6Data pageData = data as TutorialPage6Data;
            if (pageData != null)
            {
                _cachedData = pageData;
            }
            else
            {
                Debug.LogWarning("[TutorialPage6Controller] SetupData: 전달된 데이터가 null입니다.");
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            if (cg1CanvasGroup) cg1CanvasGroup.alpha = 1f;
            if (cartACanvas) cartACanvas.alpha = 1f;
            if (cartBCanvas) cartBCanvas.alpha = 1f;
            if (cartCCanvas) cartCCanvas.alpha = 1f;

            if (_cachedData != null)
            {
                if (descriptionUI)
                {
                    string rawText = string.Empty;
                    bool isServer = TcpManager.Instance && TcpManager.Instance.IsServer;

                    // Why: 현재 PC가 서버(P1)이면 A 텍스트, 클라이언트(P2)이면 B 텍스트를 출력함
                    if (isServer && _cachedData.descriptionTextA != null)
                    {
                        rawText = _cachedData.descriptionTextA.text;
                    }
                    else if (!isServer && _cachedData.descriptionTextB != null)
                    {
                        rawText = _cachedData.descriptionTextB.text;
                    }
                    else
                    {
                        Debug.LogWarning("[TutorialPage6Controller] descriptionText 데이터가 null입니다.");
                    }

                    if (!string.IsNullOrEmpty(rawText))
                    {
                        string processedText = rawText
                            .Replace("{nameA}", "사용자A")
                            .Replace("{nameB}", "사용자B")
                            .Replace("{cartridge}", "A 카트리지"); 

                        descriptionUI.text = processedText;
                    }
                }

                if (cartAText && _cachedData.cartNameA != null) cartAText.text = _cachedData.cartNameA.text;
                if (cartBText && _cachedData.cartNameB != null) cartBText.text = _cachedData.cartNameB.text;
                if (cartCText && _cachedData.cartNameC != null) cartCText.text = _cachedData.cartNameC.text;
            }

            // Why: 외부 연동 전 임시로 일정 시간 대기 후 1번 카트리지를 자동 선택함
            if (_autoSelectCoroutine != null) StopCoroutine(_autoSelectCoroutine);
            _autoSelectCoroutine = StartCoroutine(AutoSelectRoutine());
        }

        public override void OnExit()
        {
            // Why: 씬 전환 중 화면 암전 방지를 위해 base.OnExit() 생략
            StopAllAnimationCoroutines();

            if (_autoSelectCoroutine != null)
            {
                StopCoroutine(_autoSelectCoroutine);
                _autoSelectCoroutine = null;
            }
        }

        private void Update()
        {
            // Why: 자동으로 넘어가도록 수정되었으므로 기존의 키보드 입력 체크 로직은 비워둠
        }

        private IEnumerator AutoSelectRoutine()
        {
            // 화면에 요소들이 나타난 뒤 사용자가 인지할 수 있도록 짧게 대기함
            yield return new WaitForSeconds(autoSelectDelay);
            
            if (!_isCompleted)
            {
                SelectLegoCart(1);
            }
        }

        private void SelectLegoCart(int selectedIndex)
        {
            _isCompleted = true; 

            ManageCartAnimation(ref _animationCoroutineA, cartACanvas, cartATransform, selectedIndex == 1);
            ManageCartAnimation(ref _animationCoroutineB, cartBCanvas, cartBTransform, selectedIndex == 2);
            ManageCartAnimation(ref _animationCoroutineC, cartCCanvas, cartCTransform, selectedIndex == 3);

            StartCoroutine(FinalTransitionRoutine());
        }

        private IEnumerator FinalTransitionRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(finalHoldTime);

            float elapsed = 0f;
            float startAlpha = cg1CanvasGroup ? cg1CanvasGroup.alpha : 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;

                if (cg1CanvasGroup) 
                {
                    cg1CanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                }

                yield return null;
            }

            if (cg1CanvasGroup) 
            {
                cg1CanvasGroup.alpha = 0f;
            }

            if (pageCanvasGroup)
            {
                pageCanvasGroup.alpha = 1f;
            }

            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }

        private void ManageCartAnimation(ref Coroutine currentRoutine, CanvasGroup canvasGroup, RectTransform rectTransform, bool isSelected)
        {
            if (currentRoutine != null)
            {
                StopCoroutine(currentRoutine);
                currentRoutine = null;
            }

            if (canvasGroup && rectTransform)
            {
                currentRoutine = StartCoroutine(CartAnimationRoutine(canvasGroup, rectTransform, isSelected));
            }
        }

        private IEnumerator CartAnimationRoutine(CanvasGroup canvasGroup, RectTransform rectTransform, bool isSelected)
        {
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;
            float targetAlpha = isSelected ? 1f : 0f;
            Vector2 startPos = rectTransform.anchoredPosition;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;

                if (canvasGroup) canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

                if (isSelected && rectTransform)
                {
                    rectTransform.anchoredPosition = Vector2.Lerp(startPos, _selectedTargetPos, t);
                }

                yield return null;
            }

            if (canvasGroup) canvasGroup.alpha = targetAlpha;
            if (isSelected && rectTransform) rectTransform.anchoredPosition = _selectedTargetPos;
        }

        private void StopAllAnimationCoroutines()
        {
            if (_animationCoroutineA != null) StopCoroutine(_animationCoroutineA);
            if (_animationCoroutineB != null) StopCoroutine(_animationCoroutineB);
            if (_animationCoroutineC != null) StopCoroutine(_animationCoroutineC);

            _animationCoroutineA = null;
            _animationCoroutineB = null;
            _animationCoroutineC = null;
        }
    }
}