using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.Utils;

namespace My.Scripts._07_Ending.Pages
{
    /// <summary>
    /// JSON에서 로드되는 엔딩 페이지 3의 데이터 구조체.
    /// 일반 엔딩과 특별 엔딩 각각의 텍스트 데이터를 관리함.
    /// </summary>
    [Serializable]
    public class EndingPage3Data
    {
        public TextSetting normalEndingText;
        public TextSetting specialEndingText;
    }

    /// <summary>
    /// 엔딩의 세 번째 페이지 컨트롤러.
    /// 다른 카트리지의 클리어 여부에 따라 일반 또는 특별 엔딩 문구를 분기하여 출력함.
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
        private bool _isCompleted;
        private bool _isSpecialEnding;

        /// <summary>
        /// 전달받은 엔딩 데이터를 메모리에 캐싱함.
        /// </summary>
        /// <param name="data">EndingPage3Data 타입의 데이터 객체.</param>
        protected override void SetupData(EndingPage3Data data)
        {
            _cachedData = data;
        }

        /// <summary>
        /// 페이지 진입 시 유저의 전체 클리어 상태를 확인하여 엔딩 분기를 결정함.
        /// 모든 카트리지 콘텐츠 클리어 여부에 따라 시각적 연출과 텍스트를 확정하기 위함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            if (redLineImage)
            {
                redLineImage.type = Image.Type.Filled;
                redLineImage.fillAmount = 0f;
            }

            _isSpecialEnding = false;
            if (SessionManager.Instance)
            {
                _isSpecialEnding = SessionManager.Instance.IsOtherCartridgeContentsCleared;
            }

            Debug.Log($"[EndingPage3] 엔딩 분기 결정: {(_isSpecialEnding ? "특별 엔딩" : "일반 엔딩")}");

            ApplyDataToUI();

            if (mainCg) mainCg.alpha = 0f;

            if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = StartCoroutine(SequenceRoutine());
        }

        /// <summary>
        /// 페이지 이탈 시 진행 중인 애니메이션 코루틴을 중단함.
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
        /// 결정된 엔딩 분기에 맞춰 UI 텍스트를 갱신함.
        /// </summary>
        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;

            TextSetting targetText = _isSpecialEnding ? _cachedData.specialEndingText : _cachedData.normalEndingText;
            
            if (endingTextUI)
            {
                SetUIText(endingTextUI, targetText);
            }
        }

        /// <summary>
        /// 화면 페이드 인 후 특별 엔딩일 경우 라인 드로잉 연출을 추가로 수행함.
        /// </summary>
        private IEnumerator SequenceRoutine()
        {
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

            if (_isSpecialEnding && redLineImage)
            {
                yield return StartCoroutine(FillImageRoutine(redLineImage, 0f, 1f, redLineFillDuration));
            }

            yield return CoroutineData.GetWaitForSeconds(waitTime);

            CompletePage();
        }

        /// <summary>
        /// 이미지의 FillAmount 속성을 시간에 따라 선형 보간하여 채워지는 효과를 연출함.
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

        /// <summary>
        /// 페이지 완료 플래그를 설정하고 매니저에게 시퀀스 종료를 알림.
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