using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.Utils;
using My.Scripts.Network;
using My.Scripts.Global;
using Wonjeong.UI;

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
    /// 카트리지 선택 연출이 진행되는 튜토리얼 6페이지 컨트롤러.
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
            if (pageData != null) _cachedData = pageData;
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
                    bool isServer = false;
                    
                    // Why: 로컬 네트워크 상태를 확인하여 클라이언트일 경우 B 텍스트를 불러오도록 수정함
                    if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;
                    
                    if (isServer && _cachedData.descriptionTextA != null)
                        rawText = _cachedData.descriptionTextA.text;
                    else if (!isServer && _cachedData.descriptionTextB != null)
                        rawText = _cachedData.descriptionTextB.text;

                    if (!string.IsNullOrEmpty(rawText))
                    {
                        string cart = SessionManager.Instance && !string.IsNullOrEmpty(SessionManager.Instance.Cartridge) ? SessionManager.Instance.Cartridge : "A";
                        string processedText = My.Scripts.UI.UIUtils.ReplacePlayerNamePlaceholders(rawText)
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

                if (cg1CanvasGroup) cg1CanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }

            if (cg1CanvasGroup) cg1CanvasGroup.alpha = 0f;
            if (pageCanvasGroup) pageCanvasGroup.alpha = 1f;

            if (onStepComplete != null) onStepComplete.Invoke(0);
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
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("레고_2");
        }

        private void StopAllAnimationCoroutines()
        {
            if (_animationCoroutineA != null) StopCoroutine(_animationCoroutineA);
            if (_animationCoroutineB != null) StopCoroutine(_animationCoroutineB);
            if (_animationCoroutineC != null) StopCoroutine(_animationCoroutineC);
        }
    }
}