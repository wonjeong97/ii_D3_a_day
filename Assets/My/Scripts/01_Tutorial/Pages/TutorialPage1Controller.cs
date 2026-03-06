using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;

namespace My.Scripts._01_Tutorial.Pages
{   
    /// <summary>
    /// JSON에서 로드되는 튜토리얼 1페이지 데이터 구조체.
    /// </summary>
    [Serializable]
    public class TutorialPage1Data
    {
        public TextSetting descriptionText; 
    }
    
    /// <summary>
    /// 첫 번째 튜토리얼 페이지를 제어하는 컨트롤러.
    /// 페이지 자체는 즉시 나타나고 텍스트만 단독으로 페이드인 됨.
    /// </summary>
    public class TutorialPage1Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup textCanvasGroup;
        [SerializeField] private Text descriptionText;

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private string _cachedMessage = string.Empty;
        private bool _isPageActive;
        private Coroutine _fadeCoroutine;

        /// <summary>
        /// 매 프레임 엔터 키 입력을 확인하여 페이지 완료 여부를 결정함.
        /// </summary>
        private void Update()
        {   
            if (!_isPageActive) return;
            
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OnConfirmInput();
            }
        }

        /// <summary>
        /// TutorialManager로부터 전달받은 JSON 데이터를 텍스트 변수에 저장함.
        /// </summary>
        public override void SetupData(object data)
        {
            TutorialPage1Data pageData = data as TutorialPage1Data;
            
            if (pageData != null && pageData.descriptionText != null)
            {
                _cachedMessage = pageData.descriptionText.text;
            }
            else
            {   
                _cachedMessage = string.Empty;
                Debug.LogError("[TutorialPage1Controller] 전달된 데이터가 비어있거나 형식이 잘못되었습니다.");
            }
        }

        /// <summary>
        /// 페이지가 활성화될 때 텍스트용 캔버스 그룹을 페이드인 연출함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter(); // 루트 객체를 켜고 알파값을 1로 고정함

            _isPageActive = true;
            
            if (descriptionText)
            {
                descriptionText.text = !string.IsNullOrEmpty(_cachedMessage) 
                    ? _cachedMessage 
                    : "엔터 키를 눌러 진행하세요.";
            }

            // Why: 씬 진입 시 부자연스러운 화면 전체 페이드 대신, 텍스트만 자연스럽게 떠오르도록 연출
            if (textCanvasGroup)
            {
                textCanvasGroup.alpha = 0f;
                _fadeCoroutine = StartCoroutine(FadeTextRoutine(textCanvasGroup, 0f, 1f, fadeDuration));
            }
        }

        /// <summary>
        /// 확인 입력 발생 시 상위 매니저에게 현재 페이지 세트가 완료되었음을 알림.
        /// </summary>
        private void OnConfirmInput()
        {
            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }

        public override void OnExit()
        {
            base.OnExit();
            _isPageActive = false;
            
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
        }

        /// <summary>
        /// 텍스트를 감싸는 캔버스 그룹의 알파값을 지정된 시간 동안 조절함.
        /// </summary>
        private IEnumerator FadeTextRoutine(CanvasGroup target, float start, float end, float duration)
        {
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                
                if (target) 
                {
                    target.alpha = Mathf.Lerp(start, end, elapsed / duration);
                }
                
                yield return null;
            }

            if (target) 
            {
                target.alpha = end;
            }
        }
    }
}