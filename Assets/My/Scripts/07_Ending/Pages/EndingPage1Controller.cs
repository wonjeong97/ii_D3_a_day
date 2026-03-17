using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;

namespace My.Scripts._07_Ending.Pages
{
    /// <summary>
    /// 엔딩 페이지 1의 JSON 데이터를 담는 모델.
    /// </summary>
    [Serializable]
    public class EndingPage1Data
    {
        public TextSetting text1;
        public TextSetting text2;
    }

    /// <summary>
    /// 엔딩의 첫 번째 페이지 컨트롤러.
    /// Why: 하나의 텍스트 UI를 재활용하여 시간에 맞춰 두 개의 문장을 페이드 인/아웃으로 교체하여 보여주기 위함.
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
        private bool _isCompleted = false;

        public override void SetupData(object data)
        {
            EndingPage1Data pageData = data as EndingPage1Data;
            
            // 일반 C# 객체이므로 명시적 null 검사 수행
            if (pageData != null)
            {
                _cachedData = pageData;
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] SetupData: 전달된 데이터가 null이거나 형식이 잘못되었습니다.");
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            // 처음 시작할 때는 캔버스가 켜져있어야 하므로 알파값을 1로 초기화
            if (mainCg) mainCg.alpha = 1f;

            if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = StartCoroutine(SequenceRoutine());
        }

        public override void OnExit()
        {
            base.OnExit();
            
            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }
        }

        private IEnumerator SequenceRoutine()
        {
            // 1. 첫 번째 텍스트 세팅
            if (_cachedData != null && mainTextUI)
            {
                SetUIText(mainTextUI, _cachedData.text1);
            }

            // 2. 6초 대기
            yield return new WaitForSeconds(firstWaitTime);

            // 3. 0.5초 동안 페이드 아웃
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

            // 4. 두 번째 텍스트로 교체
            if (_cachedData != null && mainTextUI)
            {
                SetUIText(mainTextUI, _cachedData.text2);
            }

            // 5. 0.5초 동안 페이드 인
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

            // 6. 3초 대기
            yield return new WaitForSeconds(secondWaitTime);

            // 7. 페이지 완료 처리
            CompletePage();
        }

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