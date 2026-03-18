using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage5Data
    {
        public TextSetting descriptionText;
        public TextSetting descriptionText2;
    }

    /// <summary>
    /// 다섯 번째 튜토리얼 페이지 컨트롤러.
    /// 세 개의 레고 이미지를 순차적으로 크로스 페이드하며 가이드 연출을 수행함.
    /// </summary>
    public class TutorialPage5Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup mainGroupCanvas; 
        [SerializeField] private CanvasGroup descriptionCanvasGroup; 
        [SerializeField] private Text descriptionUI;
        [SerializeField] private CanvasGroup cameraImageCanvas; 

        [Header("Lego Images")]
        [SerializeField] private RectTransform legoTransform; // 레고 1 이동/회전용
        [SerializeField] private CanvasGroup lego1ImageCanvas; // 첫 번째 레고 이미지
        [SerializeField] private CanvasGroup lego2ImageCanvas; // 두 번째 레고 이미지
        [SerializeField] private CanvasGroup lego3ImageCanvas; // 세 번째 레고 이미지

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 1.0f;
        [SerializeField] private float moveDuration = 1.0f;
        [SerializeField] private float waitBetweenSteps = 1.0f;
        [SerializeField] private float finalHoldTime = 3.0f;

        // 레고 시작/종료 트랜스폼 데이터
        private readonly Vector2 _legoStartPos = new Vector2(1150f, -345f);
        private readonly Quaternion _legoStartRot = Quaternion.Euler(0, 0, -30f);
        private readonly Vector2 _legoEndPos = new Vector2(905f, -530f);
        private readonly Quaternion _legoEndRot = Quaternion.Euler(0, 0, 0);

        private TutorialPage5Data _cachedData;
        private Coroutine _animationCoroutine;

        public override void SetupData(object data)
        {
            TutorialPage5Data pageData = data as TutorialPage5Data;
            if (pageData != null)
            {
                _cachedData = pageData;
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();

            // 모든 연출 요소를 투명하게 초기화함
            if (mainGroupCanvas) mainGroupCanvas.alpha = 0f;
            if (cameraImageCanvas) cameraImageCanvas.alpha = 0f;
            if (lego1ImageCanvas) lego1ImageCanvas.alpha = 0f;
            if (lego2ImageCanvas) lego2ImageCanvas.alpha = 0f;
            if (lego3ImageCanvas) lego3ImageCanvas.alpha = 0f;

            if (legoTransform)
            {
                legoTransform.anchoredPosition = _legoStartPos;
                legoTransform.localRotation = _legoStartRot;
            }

            if (_cachedData != null && descriptionUI && _cachedData.descriptionText != null)
            {
                if (descriptionUI.supportRichText == false)
                {
                    descriptionUI.supportRichText = true;
                }
                descriptionUI.text = _cachedData.descriptionText.text;
            }

            _animationCoroutine = StartCoroutine(PageSequenceRoutine());
        }

        public override void OnExit()
        {
            base.OnExit();
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }
        }

        private IEnumerator PageSequenceRoutine()
        {
            // 1. 메인 그룹 페이드인 (1.0초) -> 대기 (1.0초)
            if (mainGroupCanvas) yield return StartCoroutine(FadeCanvasGroupRoutine(mainGroupCanvas, 0f, 1f, fadeDuration));
            yield return CoroutineData.GetWaitForSeconds(waitBetweenSteps);

            // 2. 첫 번째 레고 페이드인 (1.0초) -> 레고 이동 및 안착 (1.0초)
            if (lego1ImageCanvas) yield return StartCoroutine(FadeCanvasGroupRoutine(lego1ImageCanvas, 0f, 1f, fadeDuration));
            if (legoTransform) yield return StartCoroutine(LegoMoveRoutine());
            yield return CoroutineData.GetWaitForSeconds(5.0f); // 화면 전환 5초 대기

            // 3. 기존 설명 텍스트 단독 페이드 아웃 (0.5초)
            if (descriptionCanvasGroup) yield return StartCoroutine(FadeCanvasGroupRoutine(descriptionCanvasGroup, 1f, 0f, 0.5f));

            // 4. 두 번째 텍스트로 내용 변경
            if (_cachedData != null && _cachedData.descriptionText2 != null && descriptionUI)
            {
                descriptionUI.text = _cachedData.descriptionText2.text;
            }

            // 5. 새로운 설명 텍스트 페이드 인 (0.5초) -> 대기 (1.0초)
            if (descriptionCanvasGroup) yield return StartCoroutine(FadeCanvasGroupRoutine(descriptionCanvasGroup, 0f, 1f, 0.5f));
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            // 6. ImageCamera 페이드 인 (0.5초) -> 대기 (1.0초)
            if (cameraImageCanvas) yield return StartCoroutine(FadeCanvasGroupRoutine(cameraImageCanvas, 0f, 1f, 0.5f));
            SoundManager.Instance?.PlaySFX("공통_11");
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            // 7. 레고 1 -> 레고 2 크로스 페이드
            yield return StartCoroutine(CrossFadeRoutine(lego1ImageCanvas, lego2ImageCanvas, 0.3f));

            // 8. 두 번째 이미지를 사용자가 인식할 수 있도록 잠시 대기
            yield return CoroutineData.GetWaitForSeconds(0.5f);

            // 9. 레고 2 -> 레고 3 크로스 페이드
            yield return StartCoroutine(CrossFadeRoutine(lego2ImageCanvas, lego3ImageCanvas, 0.3f));
            SoundManager.Instance?.PlaySFX("레고_1");

            // 10. 연출 종료 대기
            yield return CoroutineData.GetWaitForSeconds(finalHoldTime);

            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }

        private IEnumerator LegoMoveRoutine()
        {
            float elapsed = 0f;

            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / moveDuration;

                if (legoTransform)
                {
                    legoTransform.anchoredPosition = Vector2.Lerp(_legoStartPos, _legoEndPos, t);
                    legoTransform.localRotation = Quaternion.Slerp(_legoStartRot, _legoEndRot, t);
                }

                yield return null;
            }

            if (legoTransform)
            {
                legoTransform.anchoredPosition = _legoEndPos;
                legoTransform.localRotation = _legoEndRot;
            }
        }

        /// <summary> 두 캔버스 그룹의 알파값을 동시에 교차시켜 부드러운 화면 전환을 연출함. </summary>
        private IEnumerator CrossFadeRoutine(CanvasGroup fadeOutTarget, CanvasGroup fadeInTarget, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Why: 하나의 코루틴 안에서 두 대상의 알파를 동시에 조절하여 완벽히 동기화된 크로스 페이드를 구현함
                if (fadeOutTarget) fadeOutTarget.alpha = Mathf.Lerp(1f, 0f, t);
                if (fadeInTarget) fadeInTarget.alpha = Mathf.Lerp(0f, 1f, t);

                yield return null;
            }

            if (fadeOutTarget) fadeOutTarget.alpha = 0f;
            if (fadeInTarget) fadeInTarget.alpha = 1f;
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
    }
}