using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using Wonjeong.Utils;

namespace My.Scripts._02_Play_Tutorial.Pages
{
    /// <summary>
    /// PlayTutorial 1페이지 데이터 구조체.
    /// 데이터 연동이 필요할 시 필드 추가 예정.
    /// </summary>
    [Serializable]
    public class PlayTutorialPage1Data
    {
        // # TODO: 제이슨 구조 확정 시 텍스트 및 이미지 세팅 필드 추가
    }

    /// <summary>
    /// 플레이 튜토리얼의 첫 번째 페이지 컨트롤러.
    /// P1과 P2가 독립적인 진행 상황을 가지며, 3개의 UI 그룹을 순차적으로 나타냄.
    /// </summary>
    public class PlayTutorialPage1Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup firstGroupCanvas;
        [SerializeField] private CanvasGroup secondGroupCanvas;
        [SerializeField] private CanvasGroup thirdGroupCanvas;

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float waitBetweenFades = 0.5f;

        private PlayTutorialPage1Data _cachedData;
        private Coroutine _animationCoroutine;

        /// <summary>
        /// 전달된 페이지 데이터를 캐싱함.
        /// </summary>
        /// <param name="data">PlayTutorialPage1Data 타입의 데이터.</param>
        public override void SetupData(object data)
        {
            PlayTutorialPage1Data pageData = data as PlayTutorialPage1Data;
            
            // 일반 C# 객체이므로 일반적인 null 검사 진행
            if (pageData != null)
            {
                _cachedData = pageData;
            }
            else
            {
                Debug.LogError("[PlayTutorialPage1Controller] 데이터 바인딩 실패: 전달된 데이터가 null입니다.");
            }
        }

        /// <summary>
        /// 페이지 진입 시 모든 UI를 투명하게 초기화하고 순차 페이드인 연출을 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            if (firstGroupCanvas) firstGroupCanvas.alpha = 0f;
            if (secondGroupCanvas) secondGroupCanvas.alpha = 0f;
            if (thirdGroupCanvas) thirdGroupCanvas.alpha = 0f;

            if (_cachedData == null)
            {
                Debug.LogError("[PlayTutorialPage1Controller] OnEnter: 캐싱된 데이터가 없습니다.");
            }

            _animationCoroutine = StartCoroutine(SequenceFadeRoutine());
        }

        /// <summary>
        /// 페이지 퇴장 시 실행 중인 연출 코루틴을 안전하게 중단함.
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
        /// 3개의 캔버스 그룹을 정해진 대기 시간에 맞춰 순서대로 페이드인 시킴.
        /// </summary>
        private IEnumerator SequenceFadeRoutine()
        {
            // Why: 시각적 정보량을 조절하여 사용자가 단계별로 인지하도록 유도함
            if (firstGroupCanvas) yield return StartCoroutine(FadeCanvasGroupRoutine(firstGroupCanvas, 0f, 1f, fadeDuration));
            yield return CoroutineData.GetWaitForSeconds(waitBetweenFades);

            if (secondGroupCanvas) yield return StartCoroutine(FadeCanvasGroupRoutine(secondGroupCanvas, 0f, 1f, fadeDuration));
            yield return CoroutineData.GetWaitForSeconds(waitBetweenFades);

            if (thirdGroupCanvas) yield return StartCoroutine(FadeCanvasGroupRoutine(thirdGroupCanvas, 0f, 1f, fadeDuration));

            // # TODO: 3개의 UI가 모두 나타난 후 다음 페이지로 넘어갈 트리거(버튼 입력, 자동 대기 등) 구현 필요
        }

        /// <summary>
        /// 캔버스 그룹의 알파값을 지정된 시간 동안 부드럽게 변경함.
        /// </summary>
        private IEnumerator FadeCanvasGroupRoutine(CanvasGroup target, float start, float end, float duration)
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