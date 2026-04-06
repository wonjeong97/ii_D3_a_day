using System;
using System.Collections;
using My.Scripts.Core.Data;
using My.Scripts.Network;
using My.Scripts.UI;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 각 스텝의 시작을 알리는 인트로 페이지 컨트롤러.
    /// 설정된 시간 이후 자동으로 다음 페이지로 전환됨.
    /// </summary>
    public class Page_Intro : GamePage
    {
        [Header("Dynamic UI Components (Main)")]
        [SerializeField] private Text textName;
        [SerializeField] private Text textDate;

        [Header("Dynamic UI Components (Sub Canvas)")]
        [SerializeField] private CanvasGroup subCanvasGroup;
        [SerializeField] private Text subTextName;
        [SerializeField] private Text subTextDate;

        [Header("Settings")]
        [SerializeField] private float autoTransitionDelay = 3.0f;

        private CommonIntroData _cachedData;
        private bool _isCompleted;
        private Coroutine _autoTransitionCoroutine;

        /// <summary>
        /// 동기화 명령어 설정.
        /// Why: 이전 코드 호환성을 위해 인터페이스만 남겨둠.
        /// </summary>
        /// <param name="command">동기화 명령어.</param>
        public void SetSyncCommand(string command) { }

        /// <summary>
        /// 페이지 데이터를 캐싱.
        /// </summary>
        /// <param name="data">초기화 데이터.</param>
        public override void SetupData(object data)
        {
            CommonIntroData pageData = data as CommonIntroData;
            if (pageData != null) _cachedData = pageData;
        }
        
        /// <summary>
        /// 메인 캔버스의 투명도(페이드 연출)를 서브 캔버스에도 동일하게 동기화함.
        /// BaseFlowManager가 메인 CanvasGroup의 알파만 조절하더라도 같이 페이드 아웃 되도록 하기 위함.
        /// </summary>
        private void Update()
        {
            if (subCanvasGroup && canvasGroup)
            {
                subCanvasGroup.alpha = canvasGroup.alpha;
            }
        }

        /// <summary>
        /// 페이지 활성화 시 초기화 및 전환 타이머 시작.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            if (subCanvasGroup)
            {
                subCanvasGroup.gameObject.SetActive(true);
                subCanvasGroup.alpha = 1f;
            }

            ApplyDataToUI();
            ApplyCurrentDate();

            if (_autoTransitionCoroutine != null) StopCoroutine(_autoTransitionCoroutine);
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_13");
            _autoTransitionCoroutine = StartCoroutine(AutoTransitionRoutine());
        }

        /// <summary>
        /// 페이지 비활성화 시 코루틴 정리.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();

            if (subCanvasGroup)
            {
                subCanvasGroup.alpha = 0f;
                subCanvasGroup.gameObject.SetActive(false);
            }

            if (_autoTransitionCoroutine != null)
            {
                StopCoroutine(_autoTransitionCoroutine);
                _autoTransitionCoroutine = null;
            }
        }

        /// <summary>
        /// 이름 등 플레이어 데이터를 양쪽 UI에 모두 적용.
        /// </summary>
        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;

            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

            TextSetting targetSetting = isServer ? _cachedData.nameA : _cachedData.nameB;

            if (textName)
            {
                SetUIText(textName, targetSetting);
                string rawText = textName.text;
                if (isServer && rawText.Contains("{nameB}")) rawText = rawText.Replace("{nameB}", "{nameA}");
                if (!isServer && rawText.Contains("{nameA}")) rawText = rawText.Replace("{nameA}", "{nameB}");
                textName.text = UIUtils.ReplacePlayerNamePlaceholders(rawText);
            }

            if (subTextName)
            {
                SetUIText(subTextName, targetSetting);
                string rawText = subTextName.text;
                if (isServer && rawText.Contains("{nameB}")) rawText = rawText.Replace("{nameB}", "{nameA}");
                if (!isServer && rawText.Contains("{nameA}")) rawText = rawText.Replace("{nameA}", "{nameB}");
                subTextName.text = UIUtils.ReplacePlayerNamePlaceholders(rawText);
            }
        }

        /// <summary>
        /// 현재 시스템 날짜를 양쪽 UI에 적용.
        /// </summary>
        private void ApplyCurrentDate()
        {
            string dateStr = DateTime.Now.ToString("yyyy.MM.dd");
            if (textDate) textDate.text = dateStr;
            if (subTextDate) subTextDate.text = dateStr;
        }

        /// <summary>
        /// 일정 시간 대기 후 다음 페이지로 넘김.
        /// </summary>
        private IEnumerator AutoTransitionRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(autoTransitionDelay);
            if (!_isCompleted) CompletePage();
        }

        /// <summary>
        /// 상위 매니저에 완료 이벤트를 전달.
        /// </summary>
        private void CompletePage()
        {
            if (_isCompleted) return;
            _isCompleted = true;

            if (onStepComplete != null) onStepComplete.Invoke(0);
        }
    }
}