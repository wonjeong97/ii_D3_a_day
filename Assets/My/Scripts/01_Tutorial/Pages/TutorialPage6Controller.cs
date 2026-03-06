using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.Utils; 

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
    /// 진입 시 페이드 효과는 상위 매니저(BaseFlowManager)에 위임하고, 
    /// 퇴장 시에만 메인 배경은 유지한 채 내부 UI 묶음(Cg1)을 페이드 아웃시킴.
    /// </summary>
    public class TutorialPage6Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup pageCanvasGroup; 
        [SerializeField] private CanvasGroup cg1CanvasGroup;  
        [SerializeField] private Text descriptionUI; 
        
        [Header("Display Settings")]
        [SerializeField] private bool isPlayer1; 

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
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float finalHoldTime = 3.0f;

        private readonly Vector2 _selectedTargetPos = new Vector2(900f, -500f);
        private TutorialPage6Data _cachedData;

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

                    if (isPlayer1 && _cachedData.descriptionTextA != null)
                    {
                        rawText = _cachedData.descriptionTextA.text;
                    }
                    else if (!isPlayer1 && _cachedData.descriptionTextB != null)
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
        }

        public override void OnExit()
        {
            // Why: 씬 전환 중 마지막 페이지의 게임 오브젝트가 꺼져버려 화면이 깜빡이는 현상(암전)을 막기 위해 base.OnExit() 호출을 생략함
            StopAllAnimationCoroutines();
        }

        private void Update()
        {
            if (_isCompleted) return;

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SelectLegoCart(1);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SelectLegoCart(2);
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SelectLegoCart(3);
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