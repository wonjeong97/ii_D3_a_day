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
    /// JSON 데이터를 UI에 적용하고 페이드 연출 후 매니저에게 완료 신호를 보냄.
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
            // Why: 일반 C# 클래스 객체이므로 명시적 null 비교를 수행함
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
            
            // Why: 화면이 페이드인 되기 전에 JSON 데이터를 UI 텍스트에 미리 적용함
            ApplyDataToUI();

            if (mainGroupCanvas) mainGroupCanvas.alpha = 0f;
            StartCoroutine(SequenceRoutine());
        }

        /// <summary>
        /// 캐싱된 데이터를 UI 텍스트 컴포넌트에 할당함.
        /// </summary>
        private void ApplyDataToUI()
        {
            if (_cachedData == null) 
            {
                Debug.LogWarning($"[{gameObject.name}] 표시할 캐시 데이터가 없습니다.");
                return;
            }

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

            PlayTutorialManager.Instance.PlayTutorialFinished();
        }
    }
}