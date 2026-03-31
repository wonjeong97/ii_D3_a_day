using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.Utils;

namespace My.Scripts._07_Ending.Pages
{
    /// <summary>
    /// 엔딩 페이지 1의 JSON 데이터를 매핑하기 위한 데이터 구조체.
    /// </summary>
    [Serializable]
    public class EndingPage1Data
    {
        public TextSetting text1;
        public TextSetting text2;
    }

    /// <summary>
    /// 엔딩의 첫 번째 페이지 컨트롤러.
    /// 하나의 텍스트 UI를 재활용하여 두 개의 문장을 순차적으로 페이드 전환하며 보여주기 위함.
    /// </summary>
    public class EndingPage1Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup mainCg;
        [SerializeField] private Text mainTextUI;

        [Header("Animation Settings")]
        [SerializeField] private float firstWaitTime = 6.0f;
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float secondWaitTime = 3.0f;

        private EndingPage1Data _cachedData;
        private Coroutine _sequenceCoroutine;
        private bool _isCompleted;

        /// <summary>
        /// 외부로부터 전달받은 페이지 데이터를 메모리에 캐싱함.
        /// </summary>
        /// <param name="data">EndingPage1Data 타입의 데이터 객체.</param>
        public override void SetupData(object data)
        {
            EndingPage1Data pageData = data as EndingPage1Data;
            
            if (pageData != null)
            {
                _cachedData = pageData;
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] SetupData: 전달된 데이터가 null이거나 형식이 잘못되었습니다.");
            }
        }

        /// <summary>
        /// 페이지 진입 시 초기화 및 텍스트 교체 시퀀스를 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            if (mainCg) mainCg.alpha = 1f;

            if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = StartCoroutine(SequenceRoutine());
        }

        /// <summary>
        /// 페이지 이탈 시 진행 중인 코루틴을 중단함.
        /// 백그라운드 연산 낭비 및 널 참조 에러를 방지하기 위함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            
            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }
        }

        /// <summary>
        /// 첫 번째 텍스트 노출, 페이드 아웃, 텍스트 교체, 페이드 인 과정을 순차적으로 제어함.
        /// 정해진 타임라인에 따라 엔딩 문구를 연출하기 위함.
        /// </summary>
        private IEnumerator SequenceRoutine()
        {
            if (_cachedData != null && mainTextUI)
            {
                SetUIText(mainTextUI, _cachedData.text1);
            }

            yield return CoroutineData.GetWaitForSeconds(firstWaitTime);

            if (mainCg)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    mainCg.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                    yield return null;
                }
                mainCg.alpha = 0f;
            }

            if (_cachedData != null && mainTextUI)
            {
                SetUIText(mainTextUI, _cachedData.text2);
            }

            if (mainCg)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    mainCg.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
                    yield return null;
                }
                mainCg.alpha = 1f;
            }

            yield return CoroutineData.GetWaitForSeconds(secondWaitTime);

            CompletePage();
        }

        /// <summary>
        /// 시퀀스 완료 플래그를 세우고 매니저에게 페이지 완료 이벤트를 알림.
        /// </summary>
        private void CompletePage()
        {
            if (_isCompleted) return;
            _isCompleted = true;

            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }
    }
}