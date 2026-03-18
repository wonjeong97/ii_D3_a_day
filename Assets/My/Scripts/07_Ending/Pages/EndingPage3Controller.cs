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
    /// 엔딩 페이지 3의 JSON 데이터를 담는 모델.
    /// 일반 엔딩과 특별 엔딩 각각의 텍스트 데이터를 가집니다.
    /// </summary>
    [Serializable]
    public class EndingPage3Data
    {
        public TextSetting normalEndingText;
        public TextSetting specialEndingText;
    }

    /// <summary>
    /// 엔딩의 세 번째 페이지 컨트롤러 (엔딩 분기 페이지).
    /// Why: 50% 확률로 일반/특별 엔딩을 결정하고, 특별 엔딩 시 추가 연출(빨간 선)을 재생합니다.
    /// </summary>
    public class EndingPage3Controller : GamePage<EndingPage3Data>
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup mainCg;
        [SerializeField] private Text endingTextUI;
        
        [Tooltip("특별 엔딩 시 나타날 빨간 선 이미지 (Image Type: Filled 권장)")]
        [SerializeField] private Image redLineImage; 

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float redLineFillDuration = 2.0f; 
        [SerializeField] private float waitTime = 4.0f;

        private EndingPage3Data _cachedData;
        private Coroutine _sequenceCoroutine;
        private bool _isCompleted = false;
        private bool _isSpecialEnding = false;

        protected override void SetupData(EndingPage3Data data)
        {
            _cachedData = data;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            // 진입 시 빨간 선 이미지를 0으로 초기화
            if (redLineImage)
            {
                redLineImage.type = Image.Type.Filled;
                redLineImage.fillAmount = 0f;
            }

            // 0.0f ~ 1.0f 사이의 난수를 생성하여 0.5 이상일 경우 특별 엔딩으로 취급 (50% 확률)
            _isSpecialEnding = UnityEngine.Random.value >= 0.5f;

            Debug.Log($"[EndingPage3] 엔딩 분기 결정: {(_isSpecialEnding ? "특별 엔딩" : "일반 엔딩")}");

            ApplyDataToUI();

            if (mainCg) mainCg.alpha = 0f;

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

        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;

            // 결정된 확률 분기에 따라 보여줄 텍스트 데이터를 선택
            TextSetting targetText = _isSpecialEnding ? _cachedData.specialEndingText : _cachedData.normalEndingText;
            
            if (endingTextUI)
            {
                SetUIText(endingTextUI, targetText);
            }
        }

        private IEnumerator SequenceRoutine()
        {
            // 1. 부드러운 페이드 인 연출
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

            // 2. 특별 엔딩일 경우 빨간 선이 채워지는 애니메이션 추가
            if (_isSpecialEnding && redLineImage)
            {
                yield return StartCoroutine(FillImageRoutine(redLineImage, 0f, 1f, redLineFillDuration));
            }

            // 3. 지정된 시간 동안 엔딩 문구 대기
            yield return CoroutineData.GetWaitForSeconds(waitTime);

            // 4. 완료 처리 후 다음 페이지로 넘김
            CompletePage();
        }

        /// <summary>
        /// 이미지의 FillAmount를 시간에 따라 선형 보간하는 코루틴.
        /// </summary>
        private IEnumerator FillImageRoutine(Image t, float s, float e, float d)
        {
            if (!t) yield break;

            float time = 0f;
            t.fillAmount = s;
            while (time < d)
            {
                time += Time.deltaTime;
                t.fillAmount = Mathf.Lerp(s, e, time / d);
                yield return null;
            }

            t.fillAmount = e;
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