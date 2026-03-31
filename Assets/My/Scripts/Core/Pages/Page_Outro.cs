using System;
using System.Collections;
using My.Scripts.Core.Data;
using My.Scripts.Network;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 각 스텝의 끝을 알리는 아웃트로 페이지 컨트롤러.
    /// 연출 후 설정된 시간이 지나면 자동으로 다음 단계로 전환됨.
    /// </summary>
    public class Page_Outro : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup mainCg;
        [SerializeField] private Text textOutroUI;
        [SerializeField] private Text textOutro2UI;

        [Header("Settings")]
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float autoTransitionDelay = 3.0f;

        private CommonOutroData _cachedData;
        private bool _isCompleted = false;
        private Coroutine _sequenceCoroutine;

        /// <summary>
        /// 동기화 명령어 설정.
        /// </summary>
        /// <param name="command">동기화 명령어.</param>
        public void SetSyncCommand(string command) { }

        /// <summary>
        /// 페이지 데이터를 캐싱.
        /// </summary>
        /// <param name="data">초기화 데이터.</param>
        public override void SetupData(object data)
        {
            CommonOutroData pageData = data as CommonOutroData;
            if (pageData != null) _cachedData = pageData;
        }

        /// <summary>
        /// 페이지 활성화 시 초기화 및 연출 시퀀스 시작.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            ApplyDataToUI();

            if (mainCg) mainCg.alpha = 0f;

            if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = StartCoroutine(SequenceRoutine());
        }

        /// <summary>
        /// 페이지 비활성화 시 코루틴 정리.
        /// </summary>
        public override void OnExit()
        {
            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }
        }

        /// <summary>
        /// 아웃트로 텍스트 데이터를 UI에 적용.
        /// </summary>
        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;
            SetUIText(textOutroUI, _cachedData.textOutro);
            SetUIText(textOutro2UI, _cachedData.textOutro2);
        }

        /// <summary>
        /// 페이드 인 연출 후 대기 및 자동 완료 처리.
        /// </summary>
        private IEnumerator SequenceRoutine()
        {
            if (mainCg) yield return StartCoroutine(FadeCanvasGroupRoutine(mainCg, 0f, 1f, fadeDuration));
            yield return CoroutineData.GetWaitForSeconds(autoTransitionDelay);
            
            if (!_isCompleted) CompletePage();
        }

        /// <summary>
        /// 캔버스 그룹의 알파값을 지정된 시간에 걸쳐 변경.
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

        /// <summary>
        /// 상위 매니저에 완료 이벤트를 전달.
        /// </summary>
        private void CompletePage()
        {
            if (_isCompleted) return;
            _isCompleted = true;

            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }

            if (onStepComplete != null) onStepComplete.Invoke(0);
        }
    }
}