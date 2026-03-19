using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Global; // GameManager 접근을 위해 추가
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.Utils;
using My.Scripts.Network;
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
    /// 여섯 번째 튜토리얼 페이지 컨트롤러.
    /// Why: GameManager에 저장된 카트리지 키를 참조하여 동적으로 애니메이션 및 텍스트를 연출한 뒤 다음 단계로 넘어감.
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
                UnityEngine.Debug.LogWarning("[TutorialPage6Controller] SetupData: 전달된 데이터가 null입니다.");
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

                    // Why: 현재 할당된 카트리지 키를 가져와 텍스트의 {cartridge} 치환자에 적용함
                    string cartName = "A";
                    if (GameManager.Instance && !string.IsNullOrEmpty(GameManager.Instance.CartridgeKey))
                    {
                        cartName = GameManager.Instance.CartridgeKey;
                    }

                    if (!string.IsNullOrEmpty(rawText))
                    {
                        string processedText = rawText
                            .Replace("{nameA}", "사용자A")
                            .Replace("{nameB}", "사용자B")
                            .Replace("{cartridge}", $"{cartName} 카트리지"); 

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

        private void Update()
        {
        }

        private IEnumerator AutoSelectRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(autoSelectDelay);
            
            if (!_isCompleted)
            {
                int selectedIndex = 1; 

                // Why: 게임 매니저의 카트리지 키에 따라 활성화할 인덱스를 결정함
                if (GameManager.Instance)
                {
                    string key = GameManager.Instance.CartridgeKey;
                    if (key == "A") selectedIndex = 1;
                    else if (key == "B") selectedIndex = 2;
                    else if (key == "C") selectedIndex = 3;
                    else
                    {
                        UnityEngine.Debug.LogWarning($"[TutorialPage6] 알 수 없는 카트리지 키: {key}. 기본값 A(1)를 사용합니다.");
                        selectedIndex = 1;
                    }
                }

                SelectLegoCart(selectedIndex);
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
            if (isSelected && SoundManager.Instance) 
            {
                SoundManager.Instance.PlaySFX("레고_2");
            }
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