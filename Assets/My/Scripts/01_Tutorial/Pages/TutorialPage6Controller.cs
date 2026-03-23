using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.Utils;
using My.Scripts.Network;
using Wonjeong.UI; 
using My.Scripts.Global;

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
    /// 세션에 저장된 카트리지 데이터를 기반으로 애니메이션을 재생하고 다음 단계로 넘어감.
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
                    bool isServer = true;
                    
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
                        string nameA = SessionManager.Instance && !string.IsNullOrEmpty(SessionManager.Instance.PlayerAFirstName) ? SessionManager.Instance.PlayerAFirstName : "사용자A";
                        string nameB = SessionManager.Instance && !string.IsNullOrEmpty(SessionManager.Instance.PlayerBFirstName) ? SessionManager.Instance.PlayerBFirstName : "사용자B";
                        string cart = SessionManager.Instance && !string.IsNullOrEmpty(SessionManager.Instance.Cartridge) ? SessionManager.Instance.Cartridge : "A";

                        // Why: 세션 데이터를 읽어와 하드코딩되었던 텍스트 치환을 동적으로 처리함
                        string processedText = rawText
                            .Replace("{nameA}", nameA)
                            .Replace("{nameB}", nameB)
                            .Replace("{cartridge}", $"{cart} 카트리지"); 

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

        public override void OnExit()
        {
            StopAllAnimationCoroutines();

            if (_autoSelectCoroutine != null)
            {
                StopCoroutine(_autoSelectCoroutine);
                _autoSelectCoroutine = null;
            }
        }

        private IEnumerator AutoSelectRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(autoSelectDelay);
            
            if (!_isCompleted)
            {
                int targetCartIndex = 1; // 기본값 A

                // Why: SessionManager에 등록된 유저의 카트리지 데이터에 따라 연출 인덱스를 분기함
                if (SessionManager.Instance)
                {
                    string cart = SessionManager.Instance.Cartridge;
                    if (cart == "B") targetCartIndex = 2;
                    else if (cart == "C") targetCartIndex = 3;
                    // # TODO: D 카트리지 UI가 튜토리얼에 추가될 경우 targetCartIndex = 4 로직 대응 필요
                }

                SelectLegoCart(targetCartIndex);
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

        private void ManageCartAnimation(ref Coroutine currentRoutine, CanvasGroup cg, RectTransform rectTransform, bool isSelected)
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
            SoundManager.Instance?.PlaySFX("레고_2");
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