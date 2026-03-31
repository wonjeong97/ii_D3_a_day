using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace My.Scripts.Core
{
    /// <summary>
    /// CanvasGroup을 활용하여 단일 PC 환경의 페이지 흐름을 제어하는 베이스 클래스.
    /// TCP 통신 도입으로 P1/P2 구분이 사라지고, 각 PC가 자신의 페이지 리스트만 독립적으로 관리함.
    /// </summary>
    public abstract class BaseFlowManager : MonoBehaviour
    {
        [Header("Flow Settings")]
        [SerializeField] protected List<GamePage> pages = new List<GamePage>();
        [SerializeField] protected float fadeDuration = 0.5f;

        protected int currentPageIndex = -1;
        protected bool isTransitioning = false;
        protected bool isFinished = false;
        
        // Why: 특정 씬(Step1 등)에서 시작 즉시 화면이 페이드 없이 노출되어야 할 때 사용하는 플래그
        protected bool skipFirstPageFade = false;

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
                    // Why: 페이드 연출을 무시하고 첫 페이지를 알파값 1로 즉시 활성화함
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

        protected abstract void LoadSettings();

        public void TransitionToNext()
        {
            TransitionToPage(currentPageIndex + 1);
        }

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

        protected abstract void OnAllFinished();
    }
}