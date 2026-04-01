using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace My.Scripts.Core
{
    /// <summary>
    /// 단일 PC 환경의 페이지 흐름을 제어하는 베이스 클래스.
    /// CanvasGroup을 활용하여 각 페이지의 페이드 인/아웃 전환을 수행함.
    /// </summary>
    public abstract class BaseFlowManager : MonoBehaviour
    {
        [Header("Flow Settings")]
        [SerializeField] protected List<GamePage> pages;
        [SerializeField] protected float fadeDuration;
        
        protected int currentPageIndex;
        protected bool isTransitioning;
        protected bool isFinished;
        protected bool skipFirstPageFade;

        /// <summary>
        /// 컴포넌트 활성화 시 인덱스를 초기화함.
        /// 필드 선언부의 명시적 초기화를 피하기 위함.
        /// </summary>
        protected virtual void Awake()
        {
            currentPageIndex = -1;
        }

        /// <summary>
        /// 초기 설정 로드 후 페이지 리스트를 비활성화하고 첫 페이지를 트리거함.
        /// 씬 진입 시 화면 노출을 제어하기 위함.
        /// </summary>
        protected virtual void Start()
        {
            LoadSettings();
            
            if (pages != null)
            {
                for (int i = 0; i < pages.Count; i++)
                {
                    GamePage page = pages[i];
                    if (page && page.gameObject.activeSelf)
                    {
                        page.gameObject.SetActive(false);
                    }
                }
            }
            
            if (pages != null && pages.Count > 0)
            {
                if (skipFirstPageFade)
                {
                    currentPageIndex = 0;
                    GamePage firstPage = pages[0];
                    if (firstPage)
                    {
                        StartCoroutine(StartFirstPageRoutine(firstPage));
                    }
                }
                else
                {
                    TransitionToPage(0);
                }
            }
            else
            {
                OnAllFinished();
            }
        }

        /// <summary>
        /// 첫 페이지 진입 시 비동기 로딩이 있다면 완료될 때까지 대기한 후 노출함.
        /// 에셋이 로드되기 전 화면이 깜빡이는 현상을 방지하기 위함.
        /// </summary>
        /// <param name="firstPage">처음으로 렌더링할 대상 페이지 객체.</param>
        private IEnumerator StartFirstPageRoutine(GamePage firstPage)
        {
            firstPage.onStepComplete = (trigger) => TransitionToNext();
            firstPage.OnEnter();
            firstPage.SetAlpha(0f);
            
            while (!firstPage.IsReady)
            {
                yield return null;
            }
            
            firstPage.SetAlpha(1f);
        }

        /// <summary>
        /// 씬별로 필요한 JSON 데이터 또는 리소스 설정을 로드함.
        /// </summary>
        protected abstract void LoadSettings();

        /// <summary>
        /// 현재 인덱스의 다음 순서 페이지로 전환을 요청함.
        /// </summary>
        public void TransitionToNext()
        {
            TransitionToPage(currentPageIndex + 1);
        }

        /// <summary>
        /// 지정된 인덱스의 페이지로 전환하며 범위를 벗어날 경우 종료 시퀀스를 실행함.
        /// 중복 전환이나 종료 후 실행을 방지하기 위해 상태 플래그를 검사함.
        /// </summary>
        /// <param name="index">전환할 대상 페이지 인덱스.</param>
        public virtual void TransitionToPage(int index)
        {
            if (isTransitioning || isFinished) return;
            
            if (pages == null || index < 0 || index >= pages.Count)
            {
                isFinished = true;
                
                if (pages != null && currentPageIndex >= 0 && currentPageIndex < pages.Count)
                {
                    GamePage prevPage = pages[currentPageIndex];
                    if (prevPage)
                    {
                        prevPage.onStepComplete = null;
                        prevPage.OnExit();
                    }
                }
                
                OnAllFinished();
                return;
            }

            StartCoroutine(PageTransitionRoutine(index));
        }

        /// <summary>
        /// 이전 페이지를 페이드 아웃하고 새 페이지를 페이드 인 하는 시퀀스 루틴.
        /// 페이지가 시각적으로 준비될 때까지 기다렸다가 화면에 렌더링을 시작함.
        /// </summary>
        /// <param name="index">전환할 대상 페이지 인덱스.</param>
        private IEnumerator PageTransitionRoutine(int index)
        {
            isTransitioning = true;

            if (pages != null && currentPageIndex >= 0 && currentPageIndex < pages.Count)
            {
                GamePage prevPage = pages[currentPageIndex];
                if (prevPage)
                {
                    yield return StartCoroutine(FadePage(prevPage, 1f, 0f));
                    prevPage.onStepComplete = null;
                    prevPage.OnExit();
                }
            }

            currentPageIndex = index;
            if (pages != null)
            {
                GamePage nextPage = pages[currentPageIndex];

                if (nextPage)
                {
                    nextPage.onStepComplete = (trigger) => TransitionToNext();
                    nextPage.OnEnter();
                    nextPage.SetAlpha(0f);
                
                    while (!nextPage.IsReady)
                    {
                        yield return null;
                    }

                    yield return StartCoroutine(FadePage(nextPage, 0f, 1f));
                }
            }

            isTransitioning = false;
        }

        /// <summary>
        /// CanvasGroup의 알파값을 시간에 따라 선형 보간하여 투명도를 조절함.
        /// </summary>
        /// <param name="page">대상 페이지.</param>
        /// <param name="startAlpha">시작 알파값.</param>
        /// <param name="endAlpha">목표 알파값.</param>
        private IEnumerator FadePage(GamePage page, float startAlpha, float endAlpha)
        {
            if (!page) yield break;

            CanvasGroup cg = page.GetComponent<CanvasGroup>();
            if (!cg) yield break;

            float elapsed = 0f;
            cg.alpha = startAlpha;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeDuration);
                yield return null;
            }

            cg.alpha = endAlpha;
        }

        /// <summary>
        /// 모든 페이지 시퀀스가 완료되었을 때 실행되는 추상 메서드.
        /// </summary>
        protected abstract void OnAllFinished();
    }
}