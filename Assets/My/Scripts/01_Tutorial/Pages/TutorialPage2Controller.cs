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
    /// JSON에서 로드되는 튜토리얼 2페이지 데이터 구조체.
    /// </summary>
    [Serializable]
    public class TutorialPage2Data
    {
        public TextSetting descriptionText; // JSON의 page2 > descriptionText 필드와 매핑
    }

    /// <summary>
    /// 두 번째 튜토리얼 페이지를 제어하는 컨트롤러.
    /// 진입 후 5초가 지나면 자동으로 다음 단계로 전환됨.
    /// </summary>
    public class TutorialPage2Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] 
        private Text descriptionUI;

        [Header("Settings")]
        [SerializeField] 
        private float autoTransitionDelay = 5.0f;

        private string cachedMessage = string.Empty;
        private Coroutine transitionCoroutine;

        /// <summary>
        /// TutorialManager로부터 전달받은 2페이지용 데이터를 캐싱함.
        /// </summary>
        /// <param name="data">TutorialPage2Data 타입의 객체.</param>
        public override void SetupData(object data)
        {
            TutorialPage2Data pageData = data as TutorialPage2Data;
            
            if (pageData != null && pageData.descriptionText != null)
            {
                cachedMessage = pageData.descriptionText.text;
            }
            else
            {
                Debug.LogError("[TutorialPage2Controller] 데이터 바인딩 실패: Page2 데이터가 누락되었습니다.");
            }
        }

        /// <summary>
        /// 페이지 진입 시 텍스트를 갱신하고 자동 전환 타이머를 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            if (descriptionUI)
            {
                descriptionUI.text = !string.IsNullOrEmpty(cachedMessage) ? cachedMessage : "잠시만 기다려 주세요...";
            }
            SoundManager.Instance?.PlaySFX("공통_6");
            // 지정된 시간 후 자동으로 다음 페이지로 넘어가기 위한 코루틴 실행
            transitionCoroutine = StartCoroutine(AutoTransitionRoutine());
        }

        /// <summary>
        /// 페이지를 나갈 때 실행 중인 타이머 코루틴을 중단함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }
        }

        /// <summary>
        /// 설정된 지연 시간만큼 대기 후 상위 매니저에게 단계 완료를 알림.
        /// </summary>
        private IEnumerator AutoTransitionRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(autoTransitionDelay);
            
            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }
    }
}