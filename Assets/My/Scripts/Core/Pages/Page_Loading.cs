using System.Collections;
using My.Scripts.Data;
using UnityEngine;
using UnityEngine.UI;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 로딩 또는 대기 화면을 담당하는 페이지 컨트롤러.
    /// Why: 씬 전환 전후나 특정 단계에서 5초간 대기하며 안내 텍스트를 띄워주기 위함.
    /// </summary>
    public class Page_Loading : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup mainCg;
        [SerializeField] private Text text1UI;
        [SerializeField] private Text text2UI;

        [Header("Settings")]
        [SerializeField] private float waitTime = 10.0f;
        [SerializeField] private float fadeDuration = 0.5f;

        private CommonLoadingData _cachedData;
        private Coroutine _loadingCoroutine;
        private bool _isCompleted = false;

        /// <summary>
        /// 페이지에 필요한 JSON 데이터를 캐싱함.
        /// Why: OnEnter에서 텍스트 서식을 동적으로 적용하기 위해 데이터를 미리 들고 있어야 함.
        /// </summary>
        /// <param name="data">매니저에서 넘겨주는 CommonLoadingData 객체</param>
        public override void SetupData(object data)
        {
            CommonLoadingData pageData = data as CommonLoadingData;
            
            if (pageData != null)
            {
                _cachedData = pageData;
            }
            else
            {
                Debug.LogWarning("[Page_Loading] SetupData: 전달된 데이터가 null이거나 형식이 잘못되었습니다.");
            }
        }

        /// <summary>
        /// 페이지 활성화 시 호출되는 진입점.
        /// Why: UI 서식을 세팅하고 페이드 인 및 카운트다운(5초) 코루틴을 시작하기 위함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            ApplyDataToUI();

            if (mainCg)
            {
                mainCg.alpha = 0f;
            }

            if (_loadingCoroutine != null)
            {
                StopCoroutine(_loadingCoroutine);
            }
            _loadingCoroutine = StartCoroutine(LoadingRoutine());
        }

        /// <summary>
        /// 페이지 비활성화 시 호출되는 종료점.
        /// Why: 페이지가 강제로 꺼질 때 실행 중인 코루틴을 안전하게 멈춰 메모리 누수를 막기 위함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();

            if (_loadingCoroutine != null)
            {
                StopCoroutine(_loadingCoroutine);
                _loadingCoroutine = null;
            }
        }

        /// <summary>
        /// 캐싱된 제이슨 데이터를 기반으로 UI 텍스트의 서식을 일괄 적용함.
        /// Why: 위치, 크기, 폰트 등을 하드코딩하지 않고 제이슨 설정값에 따르도록 만들기 위함.
        /// </summary>
        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;

            if (text1UI) SetUIText(text1UI, _cachedData.text1);
            if (text2UI) SetUIText(text2UI, _cachedData.text2);
        }

        /// <summary>
        /// 페이드 인 연출 후 설정된 시간(5초) 동안 대기하는 코루틴.
        /// Why: 사용자에게 로딩 텍스트를 부드럽게 보여준 뒤 지정된 시간이 지나면 자동으로 다음 단계로 넘기기 위함.
        /// </summary>
        private IEnumerator LoadingRoutine()
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

            // 설정한 시간 대기
            yield return new WaitForSeconds(waitTime);

            CompletePage();
        }

        /// <summary>
        /// 로딩 페이지의 모든 연출 및 대기가 끝났음을 상위 매니저에 알림.
        /// Why: 상위 매니저(BaseFlowManager)가 완료 이벤트를 감지하여 다음 페이지나 씬으로 전환하도록 유도함.
        /// </summary>
        private void CompletePage()
        {
            if (_isCompleted) return;
            _isCompleted = true;

            onStepComplete?.Invoke(0);
        }
    }
}