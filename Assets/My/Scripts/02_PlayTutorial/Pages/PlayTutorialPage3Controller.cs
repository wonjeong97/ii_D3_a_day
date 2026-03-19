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
    [Serializable]
    public class PlayTutorialPage3Data
    {
        public TextSetting descriptionText;
        public TextSetting waitText;
    }

    /// <summary>
    /// 상대 플레이어를 기다리는 마지막 대기 페이지 컨트롤러.
    /// 연출이 끝나면 스스로 단계 완료를 호출하여 매니저의 동기화 로직을 발동시킴.
    /// </summary>
    public class PlayTutorialPage3Controller : GamePage
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup mainGroupCanvas;
        [SerializeField] private Text descriptionUI;
        [SerializeField] private Text waitUI;

        [Header("Settings")]
        [SerializeField] private float localFadeDuration = 0.5f;

        private PlayTutorialPage3Data _cachedData;

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

        public override void OnEnter()
        {
            base.OnEnter();
            
            ApplyDataToUI();

            if (mainGroupCanvas) mainGroupCanvas.alpha = 0f;
            StartCoroutine(SequenceRoutine());
        }

        /// <summary>
        /// 페이지가 완료되고 매니저에 의해 전환 처리가 일어날 때 호출됨.
        /// Why: 부모 클래스(GamePage)의 OnExit에 있는 gameObject.SetActive(false)가 
        /// 실행되는 것을 막기 위해 의도적으로 base.OnExit() 호출을 생략함.
        /// </summary>
        public override void OnExit()
        {
            // 의도적으로 비워둠 (화면이 꺼지지 않고 유지됨)
        }

        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;

            // 수정됨: 텍스트 내용뿐만 아니라 JSON에 설정된 위치, 정렬, 폰트 등을 일괄 적용
            SetUIText(descriptionUI, _cachedData.descriptionText);
            SetUIText(waitUI, _cachedData.waitText);
        }

        private IEnumerator SequenceRoutine()
        {
            if (mainGroupCanvas)
            {
                float elapsed = 0f;
                while (elapsed < localFadeDuration)
                {
                    elapsed += Time.deltaTime;
                    mainGroupCanvas.alpha = elapsed / localFadeDuration;
                    yield return null;
                }

                mainGroupCanvas.alpha = 1f;
                SoundManager.Instance?.PlaySFX("레고_3");
            }

            yield return CoroutineData.GetWaitForSeconds(3f);

            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }
    }
}