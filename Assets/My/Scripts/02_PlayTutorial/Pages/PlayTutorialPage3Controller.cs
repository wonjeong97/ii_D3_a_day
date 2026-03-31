using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._02_PlayTutorial.Pages
{
    /// <summary>
    /// JSON에서 로드되는 조작 튜토리얼 3페이지 데이터 구조체.
    /// </summary>
    [Serializable]
    public class PlayTutorialPage3Data
    {
        public TextSetting descriptionText;
        public TextSetting waitText;
    }

    /// <summary>
    /// 상대 플레이어를 기다리는 마지막 대기 페이지 컨트롤러.
    /// 양방향 동기화를 위해 대기 화면을 띄우고 완료 신호를 발송함.
    /// </summary>
    public class PlayTutorialPage3Controller : GamePage
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup mainGroupCanvas;
        [SerializeField] private Text descriptionUI;
        [SerializeField] private Text waitUI;

        private PlayTutorialPage3Data _cachedData;

        /// <summary>
        /// 전달된 UI 세팅 데이터를 캐싱하여 페이지 활성화 시 렌더링에 활용함.
        /// </summary>
        /// <param name="data">JSON에서 역직렬화된 데이터 객체.</param>
        public override void SetupData(object data)
        {
            PlayTutorialPage3Data pageData = data as PlayTutorialPage3Data;

            if (pageData != null)
            {
                _cachedData = pageData;
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] SetupData: 데이터가 유효하지 않습니다.");
            }
        }

        /// <summary>
        /// 페이지 진입 시 UI 텍스트를 갱신하고 대기 시퀀스를 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            ApplyDataToUI();

            if (mainGroupCanvas) mainGroupCanvas.alpha = 1f;
            StartCoroutine(SequenceRoutine());
        }

        /// <summary>
        /// 페이지 이탈 시 실행되는 로직.
        /// 마지막 튜토리얼 페이지이므로 씬 전환 시 화면에 UI를 유지하기 위해 base.OnExit() 호출을 생략함.
        /// </summary>
        public override void OnExit()
        {
        }

        /// <summary>
        /// 캐싱된 텍스트 데이터를 UI 컴포넌트에 적용함.
        /// </summary>
        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;

            SetUIText(descriptionUI, _cachedData.descriptionText);
            SetUIText(waitUI, _cachedData.waitText);
        }

        /// <summary>
        /// 입장 사운드 재생 후 지정된 시간 동안 대기하고 완료 이벤트를 트리거함.
        /// 최소 대기 시간을 보장하여 네트워크 동기화 전 시각적 안정감을 주기 위함.
        /// </summary>
        private IEnumerator SequenceRoutine()
        {   
            if (SoundManager.Instance)
            {
                SoundManager.Instance.PlaySFX("레고_3");
            }
            
            yield return CoroutineData.GetWaitForSeconds(3.0f);

            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }
    }
}