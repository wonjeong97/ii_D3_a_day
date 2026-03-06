using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.Utils; // CoroutineData 사용을 위해 추가

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
    /// 선택된 카트리지 연출 후 3초 뒤 페이지 전체를 페이드 아웃하여 부드러운 씬 전환을 유도함.
    /// </summary>
    public class TutorialPage6Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup mainGroupCanvas; 
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
        [SerializeField] private float finalHoldTime = 3.0f; // 씬 전환 전 대기 시간 추가

        private readonly Vector2 _selectedTargetPos = new Vector2(900f, -500f);
        private TutorialPage6Data _cachedData;

        private Coroutine _mainFadeCoroutine;
        private Coroutine _animationCoroutineA;
        private Coroutine _animationCoroutineB;
        private Coroutine _animationCoroutineC;
        
        private bool _isCompleted = false; // 중복 선택 및 코루틴 다중 실행 방지용 플래그

        public override void SetupData(object data)
        {
            TutorialPage6Data pageData = data as TutorialPage6Data;
            if (pageData != null)
            {
                _cachedData = pageData;
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            if (mainGroupCanvas) mainGroupCanvas.alpha = 0f;
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

                    string processedText = rawText
                        .Replace("{nameA}", "사용자A")
                        .Replace("{nameB}", "사용자B")
                        .Replace("{cartridge}", "A 카트리지"); 

                    descriptionUI.text = processedText;
                }

                if (cartAText && _cachedData.cartNameA != null) cartAText.text = _cachedData.cartNameA.text;
                if (cartBText && _cachedData.cartNameB != null) cartBText.text = _cachedData.cartNameB.text;
                if (cartCText && _cachedData.cartNameC != null) cartCText.text = _cachedData.cartNameC.text;
            }

            if (mainGroupCanvas)
            {
                _mainFadeCoroutine = StartCoroutine(FadeCanvasGroupRoutine(mainGroupCanvas, 0f, 1f, fadeDuration));
            }
        }

        public override void OnExit()
        {
            base.OnExit();
            StopAllAnimationCoroutines();
        }

        private void Update()
        {
            // 이미 선택되어 씬 전환 연출이 진행 중이라면 추가 입력을 무시함
            if (_isCompleted) return;

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SelectLegoCart(1);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SelectLegoCart(2);
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SelectLegoCart(3);
        }

        private void SelectLegoCart(int selectedIndex)
        {
            _isCompleted = true; // 이후의 키 입력 차단

            ManageCartAnimation(ref _animationCoroutineA, cartACanvas, cartATransform, selectedIndex == 1);
            ManageCartAnimation(ref _animationCoroutineB, cartBCanvas, cartBTransform, selectedIndex == 2);
            ManageCartAnimation(ref _animationCoroutineC, cartCCanvas, cartCTransform, selectedIndex == 3);

            // 이동 애니메이션과 동시에 최종 마무리 시퀀스 코루틴 실행
            StartCoroutine(FinalTransitionRoutine());
        }

        /// <summary>
        /// 3초 대기 후 페이지 내의 모든 UI 요소를 페이드 아웃시키고 매니저에 완료 신호를 보냄.
        /// </summary>
        private IEnumerator FinalTransitionRoutine()
        {
            // 1. 결과 연출을 볼 수 있도록 3초 대기
            yield return CoroutineData.GetWaitForSeconds(finalHoldTime);

            // 2. 화면에 남아있는 UI를 모두 투명하게 페이드 아웃 처리함
            // Why: GameManager가 암전(검은 화면) 없이 씬을 로드하므로, UI를 먼저 부드럽게 지워서 화면 전환의 이질감을 줄이기 위함
            float elapsed = 0f;
            
            float startMainAlpha = mainGroupCanvas ? mainGroupCanvas.alpha : 0f;
            float startCartAAlpha = cartACanvas ? cartACanvas.alpha : 0f;
            float startCartBAlpha = cartBCanvas ? cartBCanvas.alpha : 0f;
            float startCartCAlpha = cartCCanvas ? cartCCanvas.alpha : 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;

                if (mainGroupCanvas) mainGroupCanvas.alpha = Mathf.Lerp(startMainAlpha, 0f, t);
                if (cartACanvas) cartACanvas.alpha = Mathf.Lerp(startCartAAlpha, 0f, t);
                if (cartBCanvas) cartBCanvas.alpha = Mathf.Lerp(startCartBAlpha, 0f, t);
                if (cartCCanvas) cartCCanvas.alpha = Mathf.Lerp(startCartCAlpha, 0f, t);

                yield return null;
            }

            if (mainGroupCanvas) mainGroupCanvas.alpha = 0f;
            if (cartACanvas) cartACanvas.alpha = 0f;
            if (cartBCanvas) cartBCanvas.alpha = 0f;
            if (cartCCanvas) cartCCanvas.alpha = 0f;

            // 3. 페이지 완료 신호 전송 -> TutorialManager.OnAllFinished() -> PlayTutorial 씬 전환
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

        private void StopAllAnimationCoroutines()
        {
            if (_mainFadeCoroutine != null) StopCoroutine(_mainFadeCoroutine);
            if (_animationCoroutineA != null) StopCoroutine(_animationCoroutineA);
            if (_animationCoroutineB != null) StopCoroutine(_animationCoroutineB);
            if (_animationCoroutineC != null) StopCoroutine(_animationCoroutineC);

            _mainFadeCoroutine = null;
            _animationCoroutineA = null;
            _animationCoroutineB = null;
            _animationCoroutineC = null;
        }
    }
}