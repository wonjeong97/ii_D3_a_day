using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Global;
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
    /// Why: 입장 후 3초 대기 후 완료 신호를 발송하여 동기화를 요청함.
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

            if (mainGroupCanvas) mainGroupCanvas.alpha = 1f;
            StartCoroutine(SequenceRoutine());
        }

        public override void OnExit()
        {
        }

        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;

            SetUIText(descriptionUI, _cachedData.descriptionText);
            SetUIText(waitUI, _cachedData.waitText);
        }

        private IEnumerator SequenceRoutine()
        {   
            if (SoundManager.Instance)
            {
                SoundManager.Instance.PlaySFX("레고_3");
            }
            
            // Why: 입장 후 3초 대기
            yield return CoroutineData.GetWaitForSeconds(3.0f);

            // Why: 3초 대기 후 매니저에게 완료 신호 발송
            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }
    }
}
