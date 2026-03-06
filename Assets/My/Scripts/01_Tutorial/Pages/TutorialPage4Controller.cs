using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary> JSON에서 로드되는 튜토리얼 4페이지 데이터 구조체. </summary>
    [Serializable]
    public class TutorialPage4Data
    {
        public TextSetting descriptionText;
    }

    /// <summary>
    /// 네 번째 튜토리얼 페이지 컨트롤러.
    /// 텍스트와 이미지를 순차적으로 페이드인시킨 후 자동으로 다음 단계로 이동함.
    /// </summary>
    public class TutorialPage4Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup textCanvasGroup;
        [SerializeField] private Text descriptionUI;
        [SerializeField] private CanvasGroup legoImageCanvasGroup;

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 1.0f;
        [SerializeField] private float waitBetweenFade = 1.0f;
        [SerializeField] private float finalHoldTime = 3.0f;

        private TutorialPage4Data _cachedData;
        private Coroutine _animationCoroutine;

        /// <summary> TutorialManager로부터 전달받은 4페이지 데이터를 캐싱함. </summary>
        public override void SetupData(object data)
        {
            TutorialPage4Data pageData = data as TutorialPage4Data;
            if (pageData != null)
            {
                _cachedData = pageData;
            }
            else
            {
                Debug.LogError("[TutorialPage4Controller] 데이터 바인딩 실패");
            }
        }

        /// <summary> 페이지 진입 시 초기 알파값을 설정하고 연출 시퀀스를 시작함. </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            // 연출 시작 전 요소들을 투명하게 초기화
            if (textCanvasGroup) textCanvasGroup.alpha = 0f;
            if (legoImageCanvasGroup) legoImageCanvasGroup.alpha = 0f;

            if (_cachedData != null && descriptionUI && _cachedData.descriptionText != null)
            {
                descriptionUI.text = _cachedData.descriptionText.text;
            }

            // 시퀀스 연출 코루틴 실행
            _animationCoroutine = StartCoroutine(PageSequenceRoutine());
        }

        /// <summary> 페이지 퇴장 시 진행 중인 연출 코루틴을 중단함. </summary>
        public override void OnExit()
        {
            base.OnExit();

            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }
        }

        /// <summary> 텍스트 페이드인 -> 대기 -> 이미지 페이드인 -> 최종 대기 순으로 진행되는 연출 루틴. </summary>
        private IEnumerator PageSequenceRoutine()
        {
            // 1. 텍스트 페이드인
            if (textCanvasGroup)
            {
                yield return StartCoroutine(FadeRoutine(textCanvasGroup, 0f, 1f));
            }

            // 2. 단계 사이 대기 (GC 최적화를 위해 캐싱된 객체 사용)
            yield return CoroutineData.GetWaitForSeconds(waitBetweenFade);

            // 3. 레고 이미지 페이드인
            if (legoImageCanvasGroup)
            {
                yield return StartCoroutine(FadeRoutine(legoImageCanvasGroup, 0f, 1f));
            }

            // 4. 최종 대기 후 다음 페이지 전환
            yield return CoroutineData.GetWaitForSeconds(finalHoldTime);

            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }

        /// <summary> CanvasGroup의 알파를 일정 시간에 걸쳐 변화시키는 범용 페이드 루틴. </summary>
        private IEnumerator FadeRoutine(CanvasGroup target, float start, float end)
        {
            float elapsed = 0f;
            
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float currentAlpha = Mathf.Lerp(start, end, elapsed / fadeDuration);
                
                if (target)
                {
                    target.alpha = currentAlpha;
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