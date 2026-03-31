using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace My.Scripts.Core
{
    /// <summary>
    /// CanvasGroup을 활용하여 단일 PC 환경의 페이지 흐름을 제어하는 베이스 클래스.
    /// 각 PC가 독립적으로 페이지 리스트를 관리하며 페이드 인/아웃 전환을 수행함.
    /// </summary>
    public abstract class BaseFlowManager : MonoBehaviour
    {
        [Header("Flow Settings")]
        [SerializeField] protected List<GamePage> pages = new List<GamePage>();
        [SerializeField] protected float fadeDuration;
        
        protected int currentPageIndex = -1;
        protected bool isTransitioning;
        protected bool isFinished;
        protected bool skipFirstPageFade;

        /// <summary>
        /// 초기 설정 로드 후 페이지 리스트를 비활성화하고 첫 페이지를 트리거함.
        /// 특정 씬에서 즉각적인 화면 노출이 필요한 경우 페이드 연출 없이 첫 페이지를 활성화함.
        /// </summary>
        protected virtual void Start()
        {
            LoadSettings();
            
            for (int i = 0; i < pages.Count; i++)
            {
                GamePage page = pages[i];
                if (page && page.gameObject.activeSelf)
                {
                    page.gameObject.SetActive(false);
                }
            }
            
            if (pages.Count > 0)
            {
                if (skipFirstPageFade)
                {
                    currentPageIndex = 0;
                    GamePage firstPage = pages[0];
                    if (firstPage)
                    {
                        firstPage.onStepComplete = (trigger) => TransitionToNext();
                        firstPage.OnEnter();
                        firstPage.SetAlpha(1f);
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
            
            if (index < 0 || index >= pages.Count)
            {
                isFinished = true;
                
                if (currentPageIndex >= 0 && currentPageIndex < pages.Count)
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
        /// 페이지 전환 중 상태 변화를 막기 위해 트랜지션 플래그를 제어함.
        /// </summary>
        private IEnumerator PageTransitionRoutine(int index)
        {
            isTransitioning = true;

            if (currentPageIndex >= 0 && currentPageIndex < pages.Count)
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
            GamePage nextPage = pages[currentPageIndex];

            if (nextPage)
            {
                nextPage.onStepComplete = (trigger) => TransitionToNext();
                nextPage.OnEnter();
                nextPage.SetAlpha(0f);
                yield return StartCoroutine(FadePage(nextPage, 0f, 1f));
            }

            isTransitioning = false;
        }

        /// <summary>
        /// CanvasGroup의 알파값을 시간에 따라 선형 보간하여 투명도를 조절함.
        /// </summary>
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