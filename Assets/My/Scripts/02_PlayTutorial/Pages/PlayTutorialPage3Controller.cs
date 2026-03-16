using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;

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

        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;

            if (descriptionUI && _cachedData.descriptionText != null)
            {
                descriptionUI.text = _cachedData.descriptionText.text;
            }

            if (waitUI && _cachedData.waitText != null)
            {
                waitUI.text = _cachedData.waitText.text;
            }
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
            }

            // Why: 상위 매니저의 isTransitioning 플래그가 안전하게 해제될 수 있도록 0.5초 대기 후 완료 신호를 쏨
            yield return new WaitForSeconds(0.5f);

            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }
    }
}