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
    /// <summary>
    /// JSON에서 로드되는 튜토리얼 5페이지 데이터 구조체.
    /// </summary>
    [Serializable]
    public class TutorialPage5Data
    {
        public TextSetting descriptionText;
        public TextSetting descriptionText2;
    }

    /// <summary>
    /// 다섯 번째 튜토리얼 페이지 컨트롤러.
    /// 기기 조작 방식을 시각적으로 안내하기 위해 3단계 레고 이미지를 크로스 페이드 연출함.
    /// </summary>
    public class TutorialPage5Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup mainGroupCanvas; 
        [SerializeField] private CanvasGroup descriptionCanvasGroup; 
        [SerializeField] private Text descriptionUI;
        [SerializeField] private CanvasGroup cameraImageCanvas; 

        [Header("Lego Images")]
        [SerializeField] private RectTransform legoTransform; 
        [SerializeField] private CanvasGroup lego1ImageCanvas; 
        [SerializeField] private CanvasGroup lego2ImageCanvas; 
        [SerializeField] private CanvasGroup lego3ImageCanvas; 

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 1.0f;
        [SerializeField] private float moveDuration = 1.0f;
        [SerializeField] private float waitBetweenSteps = 1.0f;
        [SerializeField] private float finalHoldTime = 3.0f;
        
        [Header("Line Images")]
        [SerializeField] private CanvasGroup cgLine1;
        [SerializeField] private CanvasGroup cgLine2;
        [SerializeField] private CanvasGroup cgLine3;

        private Vector2 _legoStartPos;
        private Vector2 _legoEndPos;
        private readonly Quaternion _legoStartRot = Quaternion.Euler(0, 0, -30f);
        private readonly Quaternion _legoEndRot = Quaternion.Euler(0, 0, 0);

        private TutorialPage5Data _cachedData;
        private Coroutine _animationCoroutine;

        /// <summary>
        /// 매니저로부터 전달받은 페이지 데이터를 메모리에 캐싱함.
        /// </summary>
        /// <param name="data">TutorialPage5Data 타입의 데이터 객체.</param>
        public override void SetupData(object data)
        {
            TutorialPage5Data pageData = data as TutorialPage5Data;
            if (pageData != null)
            {
                _cachedData = pageData;
            }
        }

        /// <summary>
        /// 페이지 진입 시 연출 요소들을 초기화하고 시퀀스를 시작함.
        /// 화면에 노출되기 전 모든 UI의 투명도와 위치를 시작 상태로 리셋하기 위함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            CalculateDynamicPositions();

            if (mainGroupCanvas) mainGroupCanvas.alpha = 0f;
            if (cameraImageCanvas) cameraImageCanvas.alpha = 0f;
            if (lego1ImageCanvas) lego1ImageCanvas.alpha = 0f;
            if (lego2ImageCanvas) lego2ImageCanvas.alpha = 0f;
            if (lego3ImageCanvas) lego3ImageCanvas.alpha = 0f;
            
            if (cgLine1) cgLine1.alpha = 0f;
            if (cgLine2) cgLine2.alpha = 0f;
            if (cgLine3) cgLine3.alpha = 0f;

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

        /// <summary>
        /// 페이지 이탈 시 실행 중인 애니메이션 코루틴을 중단함.
        /// 메모리 누수 및 백그라운드 연산 낭비를 방지하기 위함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }
        }

        /// <summary>
        /// 화면 해상도 비율에 맞춰 레고 애니메이션 좌표를 동적으로 계산함.
        /// 1920x1080 기준으로 하드코딩된 좌표를 다양한 종횡비 환경에서도 동일한 비율로 렌더링하기 위함.
        /// </summary>
        private void CalculateDynamicPositions()
        {
            RectTransform rt = transform as RectTransform;
            if (rt && rt.rect.width > 0 && rt.rect.height > 0)
            {
                float scaleX = rt.rect.width / 1920f;
                float scaleY = rt.rect.height / 1080f;

                _legoStartPos = new Vector2(1150f * scaleX, -345f * scaleY);
                _legoEndPos = new Vector2(905f * scaleX, -530f * scaleY);
            }
            else
            {
                _legoStartPos = new Vector2(1150f, -345f);
                _legoEndPos = new Vector2(905f, -530f);
            }
        }

        /// <summary>
        /// 텍스트, 이미지 이동, 크로스 페이드 연출을 순차적으로 제어함.
        /// 기획된 가이드 흐름에 맞춰 단계별 시각적 피드백을 제공하기 위함.
        /// </summary>
        private IEnumerator PageSequenceRoutine()
        {
            if (mainGroupCanvas) yield return StartCoroutine(FadeCanvasGroupRoutine(mainGroupCanvas, 0f, 1f, fadeDuration));
            yield return CoroutineData.GetWaitForSeconds(waitBetweenSteps);

            // 1단계 레고 등장
            if (lego1ImageCanvas) yield return StartCoroutine(FadeCanvasGroupRoutine(lego1ImageCanvas, 0f, 1f, fadeDuration));
            
            // 1. 1단계 레고 이동 완료 후 2초 대기
            if (legoTransform) yield return StartCoroutine(LegoMoveRoutine());
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            
            // 2. CgLine1 연출 (0.5초 페이드인 > 1초 대기 > 0.5초 페이드아웃 > 1초 대기)
            if (cgLine1) yield return StartCoroutine(FadeCanvasGroupRoutine(cgLine1, 0f, 1f, 0.5f));
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            if (cgLine1) yield return StartCoroutine(FadeCanvasGroupRoutine(cgLine1, 1f, 0f, 0.5f));
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            // 3. 2단계 레고 노출 및 CgLine2 연출
            StartCoroutine(CrossFadeRoutine(lego1ImageCanvas, lego2ImageCanvas, 0.5f));
            
            if (cgLine2) yield return StartCoroutine(FadeCanvasGroupRoutine(cgLine2, 0f, 1f, 0.5f));
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            if (cgLine2) yield return StartCoroutine(FadeCanvasGroupRoutine(cgLine2, 1f, 0f, 0.5f));

            // 4. 3단계 레고 노출 + 효과음 및 CgLine3 연출
            StartCoroutine(CrossFadeRoutine(lego2ImageCanvas, lego3ImageCanvas, 0.5f));
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("레고_1");
            
            if (cgLine3) yield return StartCoroutine(FadeCanvasGroupRoutine(cgLine3, 0f, 1f, 0.5f));
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            if (cgLine3) yield return StartCoroutine(FadeCanvasGroupRoutine(cgLine3, 1f, 0f, 0.5f));

            // 5. 2초 대기
            yield return CoroutineData.GetWaitForSeconds(2.0f);

            // 6. 설명 텍스트 교체 및 카메라 UI 페이드인
            if (descriptionCanvasGroup) yield return StartCoroutine(FadeCanvasGroupRoutine(descriptionCanvasGroup, 1f, 0f, 0.5f));

            if (_cachedData != null && _cachedData.descriptionText2 != null && descriptionUI)
            {
                descriptionUI.text = _cachedData.descriptionText2.text;
            }

            if (descriptionCanvasGroup) StartCoroutine(FadeCanvasGroupRoutine(descriptionCanvasGroup, 0f, 1f, 0.5f));
            if (cameraImageCanvas) StartCoroutine(FadeCanvasGroupRoutine(cameraImageCanvas, 0f, 1f, 0.5f));
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_11");
            
            yield return CoroutineData.GetWaitForSeconds(0.5f);

            yield return CoroutineData.GetWaitForSeconds(finalHoldTime);

            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }

        /// <summary>
        /// 레고 UI 객체의 위치와 회전값을 목표 지점까지 선형 보간함.
        /// </summary>
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

        /// <summary>
        /// 두 캔버스 그룹의 알파값을 교차하여 화면 전환을 연출함.
        /// </summary>
        /// <param name="fadeOutTarget">사라질 캔버스 그룹.</param>
        /// <param name="fadeInTarget">나타날 캔버스 그룹.</param>
        /// <param name="duration">전환에 걸리는 시간.</param>
        private IEnumerator CrossFadeRoutine(CanvasGroup fadeOutTarget, CanvasGroup fadeInTarget, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 하나의 코루틴 안에서 두 대상의 알파를 동시에 조절하여 동기화된 크로스 페이드를 구현함.
                if (fadeOutTarget) fadeOutTarget.alpha = Mathf.Lerp(1f, 0f, t);
                if (fadeInTarget) fadeInTarget.alpha = Mathf.Lerp(0f, 1f, t);

                yield return null;
            }

            if (fadeOutTarget) fadeOutTarget.alpha = 0f;
            if (fadeInTarget) fadeInTarget.alpha = 1f;
        }

        /// <summary>
        /// 단일 캔버스 그룹의 투명도를 목표값까지 변경함.
        /// </summary>
        /// <param name="target">알파값을 변경할 캔버스 그룹.</param>
        /// <param name="start">시작 알파값.</param>
        /// <param name="end">목표 알파값.</param>
        /// <param name="duration">변경에 걸리는 시간.</param>
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