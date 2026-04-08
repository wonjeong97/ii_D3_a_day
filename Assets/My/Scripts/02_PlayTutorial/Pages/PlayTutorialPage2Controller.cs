using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Network; 
using My.Scripts.Hardware; 
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._02_PlayTutorial.Pages
{
    /// <summary>
    /// 조작 튜토리얼 2페이지 데이터 모델.
    /// </summary>
    [Serializable]
    public class PlayTutorialPage2Data
    {
    }

    /// <summary>
    /// 조작 튜토리얼의 두 번째 페이지 컨트롤러.
    /// Why: 카드를 5초 동안 떼지 않고 유지하는 조작을 학습하기 위함.
    /// </summary>
    public class PlayTutorialPage2Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup mainGroupCanvas;
        [SerializeField] private Text countdownUI;

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private PlayTutorialPage2Data _cachedData;
        private Coroutine _fadeCoroutine;
        private Coroutine _countdownCoroutine;
        
        private bool _isCompleted;

        /// <summary>
        /// 이전 페이지(Page1)에서 확정된 응답 인덱스를 초기값으로 설정함.
        /// (현재는 카운트다운 중 추가 입력을 받지 않으므로 유지용으로만 둠)
        /// </summary>
        /// <param name="answerIndex">1~5 사이의 응답 인덱스.</param>
        public void SetInitialAnswer(int answerIndex)
        {
            // 사용하지 않음
        }

        public override void SetupData(object data)
        {
            PlayTutorialPage2Data pageData = data as PlayTutorialPage2Data;
            
            if (pageData != null) 
            {
                _cachedData = pageData;
            }
            else 
            {
                Debug.LogWarning("[PlayTutorialPage2Controller] SetupData: 전달된 데이터가 null입니다.");
            }
        }

        /// <summary>
        /// 페이지 진입 시 연출을 수행하고 즉시 카운트다운을 시작함.
        /// (이전 페이지에서 입력을 확정했으므로 추가 RFID 폴링은 하지 않음)
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            _isCompleted = false;

            if (countdownUI) countdownUI.text = "5";
            if (mainGroupCanvas) mainGroupCanvas.alpha = 0f;

            _fadeCoroutine = StartCoroutine(FadeCanvasGroupRoutine(mainGroupCanvas, 0f, 1f, fadeDuration));

            _countdownCoroutine = StartCoroutine(CountdownRoutine());
        }

        /// <summary>
        /// 페이지 이탈 시 코루틴을 중단함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
        }

        /// <summary>
        /// 5초 카운트다운을 수행함.
        /// </summary>
        private IEnumerator CountdownRoutine()
        {
            if (SoundManager.Instance)
            {   
                SoundManager.Instance.StopSFX();
                SoundManager.Instance.PlaySFX("공통_10_5초");
            }

            for (int i = 5; i >= 1; i--)
            {
                if (countdownUI) countdownUI.text = i.ToString();
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            _isCompleted = true;

            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }

        /// <summary>
        /// 캔버스 그룹 알파값 선형 보간 애니메이션.
        /// </summary>
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