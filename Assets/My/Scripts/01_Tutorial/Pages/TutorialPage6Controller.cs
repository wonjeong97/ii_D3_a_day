using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.Utils;
using My.Scripts.Network;
using My.Scripts.Global;
using My.Scripts.UI;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary>
    /// JSON에서 로드되는 튜토리얼 6페이지 데이터 구조체.
    /// </summary>
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
    /// 카트리지 선택 연출이 진행되는 튜토리얼 6페이지 컨트롤러.
    /// 현재 세션의 카트리지 정보에 맞춰 시각적인 선택 애니메이션을 제공하기 위함.
    /// </summary>
    public class TutorialPage6Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup pageCanvasGroup;
        [SerializeField] private CanvasGroup cg1CanvasGroup;
        [SerializeField] private Text descriptionUI;

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
        [SerializeField] private float autoSelectDelay = 1.5f;
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float finalHoldTime = 3.0f;

        private Vector2 _selectedTargetPos;
        private TutorialPage6Data _cachedData;

        private Coroutine _autoSelectCoroutine;
        private Coroutine _animationCoroutineA;
        private Coroutine _animationCoroutineB;
        private Coroutine _animationCoroutineC;

        private bool _isCompleted;

        /// <summary>
        /// 매니저로부터 전달받은 페이지 데이터를 메모리에 캐싱함.
        /// </summary>
        public override void SetupData(object data)
        {
            TutorialPage6Data pageData = data as TutorialPage6Data;
            if (pageData != null) _cachedData = pageData;
        }

        /// <summary>
        /// 페이지 진입 시 UI를 초기화하고 텍스트 플레이스홀더를 치환함.
        /// 네트워크 역할에 맞는 텍스트를 출력하고 애니메이션을 준비하기 위함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            CalculateDynamicPositions();

            if (cg1CanvasGroup) cg1CanvasGroup.alpha = 1f;
            if (cartACanvas) cartACanvas.alpha = 1f;
            if (cartBCanvas) cartBCanvas.alpha = 1f;
            if (cartCCanvas) cartCCanvas.alpha = 1f;

            if (_cachedData != null)
            {
                if (descriptionUI)
                {
                    string rawText = string.Empty;
                    bool isServer = false;

                    if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

                    if (isServer && _cachedData.descriptionTextA != null)
                        rawText = _cachedData.descriptionTextA.text;
                    else if (!isServer && _cachedData.descriptionTextB != null)
                        rawText = _cachedData.descriptionTextB.text;

                    if (!string.IsNullOrEmpty(rawText))
                    {
                        string cart =
                            SessionManager.Instance && !string.IsNullOrEmpty(SessionManager.Instance.Cartridge) ? SessionManager.Instance.Cartridge : "A";
                        string processedText = UIUtils.ReplacePlayerNamePlaceholders(rawText).Replace("{cartridge}", $"{cart} 카트리지");

                        descriptionUI.text = processedText;
                    }
                }

                if (cartAText && _cachedData.cartNameA != null) cartAText.text = _cachedData.cartNameA.text;
                if (cartBText && _cachedData.cartNameB != null) cartBText.text = _cachedData.cartNameB.text;
                if (cartCText && _cachedData.cartNameC != null) cartCText.text = _cachedData.cartNameC.text;
            }

            if (_autoSelectCoroutine != null) StopCoroutine(_autoSelectCoroutine);
            _autoSelectCoroutine = StartCoroutine(AutoSelectRoutine());
        }

        /// <summary>
        /// 페이지 이탈 시 실행 중인 애니메이션 코루틴을 중단함.
        /// 마지막 튜토리얼 페이지이므로 씬 전환 시 화면에 UI를 유지하기 위해 base.OnExit()를 고의로 생략함.
        /// </summary>
        public override void OnExit()
        {
            StopAllAnimationCoroutines();

            if (_autoSelectCoroutine != null)
            {
                StopCoroutine(_autoSelectCoroutine);
                _autoSelectCoroutine = null;
            }
        }

        /// <summary>
        /// 화면 해상도 비율에 맞춰 애니메이션 목표 좌표를 동적으로 계산함.
        /// 1920x1080 기준으로 하드코딩된 좌표를 다양한 종횡비 환경에서도 동일한 비율로 렌더링하기 위함.
        /// </summary>
        private void CalculateDynamicPositions()
        {
            RectTransform rt = transform as RectTransform;
            if (rt && rt.rect.width > 0 && rt.rect.height > 0)
            {
                float scaleX = rt.rect.width / 1920f;
                float scaleY = rt.rect.height / 1080f;

                _selectedTargetPos = new Vector2(900f * scaleX, -500f * scaleY);
            }
            else
            {
                _selectedTargetPos = new Vector2(900f, -500f);
            }
        }

        /// <summary>
        /// 일정 시간 대기 후 세션에 저장된 카트리지 정보를 기반으로 선택 연출을 시작함.
        /// </summary>
        private IEnumerator AutoSelectRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(autoSelectDelay);

            if (!_isCompleted)
            {
                int targetCartIndex = 1;

                if (SessionManager.Instance)
                {
                    string cart = SessionManager.Instance.Cartridge;
                    if (!string.IsNullOrEmpty(cart))
                    {
                        switch (cart.ToUpper())
                        {
                            case "A": targetCartIndex = 1; break;
                            case "B": targetCartIndex = 2; break;
                            case "C": targetCartIndex = 3; break;
                            case "D": targetCartIndex = 4; break;
                            default: targetCartIndex = 1; break;
                        }
                    }
                }

                SelectLegoCart(targetCartIndex);
            }
        }

        /// <summary>
        /// 선택된 카트리지에 따라 개별 UI 애니메이션을 할당하고 실행함.
        /// </summary>
        private void SelectLegoCart(int selectedIndex)
        {
            _isCompleted = true;

            ManageCartAnimation(ref _animationCoroutineA, cartACanvas, cartATransform, selectedIndex == 1);
            ManageCartAnimation(ref _animationCoroutineB, cartBCanvas, cartBTransform, selectedIndex == 2);
            ManageCartAnimation(ref _animationCoroutineC, cartCCanvas, cartCTransform, selectedIndex == 3);

            StartCoroutine(FinalTransitionRoutine());
        }

        /// <summary>
        /// 애니메이션 종료 후 화면을 페이드 아웃하고 다음 단계로 전환함.
        /// </summary>
        private IEnumerator FinalTransitionRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(finalHoldTime);

            float elapsed = 0f;
            float startAlpha = cg1CanvasGroup ? cg1CanvasGroup.alpha : 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;

                if (cg1CanvasGroup) cg1CanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }

            if (cg1CanvasGroup) cg1CanvasGroup.alpha = 0f;
            if (pageCanvasGroup) pageCanvasGroup.alpha = 1f;

            if (onStepComplete != null) onStepComplete.Invoke(0);
        }

        /// <summary>
        /// 기존 애니메이션을 중단하고 새로운 카트리지 애니메이션 코루틴을 할당함.
        /// </summary>
        private void ManageCartAnimation(ref Coroutine currentRoutine, CanvasGroup cg, RectTransform rectTransform,
            bool isSelected)
        {
            if (currentRoutine != null)
            {
                StopCoroutine(currentRoutine);
                currentRoutine = null;
            }

            if (cg && rectTransform)
            {
                currentRoutine = StartCoroutine(CartAnimationRoutine(cg, rectTransform, isSelected));
            }
        }

        /// <summary>
        /// 선택 여부에 따라 카트리지의 알파값과 위치를 선형 보간함.
        /// </summary>
        private IEnumerator CartAnimationRoutine(CanvasGroup cg, RectTransform rectTransform, bool isSelected)
        {
            float elapsed = 0f;
            float startAlpha = cg.alpha;
            float targetAlpha = isSelected ? 1f : 0f;
            Vector2 startPos = rectTransform.anchoredPosition;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;

                if (cg) cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

                if (isSelected && rectTransform)
                {
                    rectTransform.anchoredPosition = Vector2.Lerp(startPos, _selectedTargetPos, t);
                }

                yield return null;
            }

            if (cg) cg.alpha = targetAlpha;
            if (isSelected && rectTransform) rectTransform.anchoredPosition = _selectedTargetPos;

            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("레고_2");
        }

        /// <summary>
        /// 모든 카트리지 애니메이션 코루틴을 강제 중단함.
        /// </summary>
        private void StopAllAnimationCoroutines()
        {
            if (_animationCoroutineA != null) StopCoroutine(_animationCoroutineA);
            if (_animationCoroutineB != null) StopCoroutine(_animationCoroutineB);
            if (_animationCoroutineC != null) StopCoroutine(_animationCoroutineC);
        }
    }
}