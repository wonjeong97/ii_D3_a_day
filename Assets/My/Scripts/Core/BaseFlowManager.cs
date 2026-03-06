using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace My.Scripts.Core
{
    /// <summary>
    /// 각 단계별로 출력될 P1(Main), P2(Sub) 페이지 쌍을 정의함.
    /// </summary>
    [Serializable]
    public struct PageSet
    {
        public GamePage pageP1;
        public GamePage pageP2;
    }

    /// <summary>
    /// CanvasGroup을 활용하여 페이지 세트 간 페이드 연출 및 흐름을 제어하는 베이스 클래스.
    /// </summary>
    public abstract class BaseFlowManager : MonoBehaviour
    {
        [Header("Flow Settings")]
        [SerializeField] protected List<PageSet> pageSets = new List<PageSet>();
        [SerializeField] private float fadeDuration = 0.5f;

        protected int currentPageIndex = -1;
        protected bool isTransitioning = false;
        private bool isFlowFinished = false;

        protected virtual void Start()
        {
            LoadSettings();
            
            // 첫 시작 시 모든 페이지 비활성화
            foreach (PageSet page in pageSets)
            {
                if (page.pageP1 && page.pageP1.gameObject.activeSelf)
                {
                    page.pageP1.gameObject.SetActive(false);
                }

                if (page.pageP2 && page.pageP2.gameObject.activeSelf)
                {
                    page.pageP2.gameObject.SetActive(false);
                }
            }
            
            // Why: 씬 진입 시 첫 페이지의 루트 캔버스는 즉시 노출(알파 1)하고, 
            // 세부적인 요소(텍스트 등)의 페이드인은 각 페이지 컨트롤러에 위임함
            if (pageSets.Count > 0)
            {
                currentPageIndex = 0;
                PageSet firstSet = pageSets[0];

                if (firstSet.pageP1)
                {
                    firstSet.pageP1.onStepComplete = TransitionToNext;
                    firstSet.pageP1.OnEnter();
                    firstSet.pageP1.SetAlpha(1f);
                }

                if (firstSet.pageP2)
                {
                    firstSet.pageP2.onStepComplete = TransitionToNext;
                    firstSet.pageP2.OnEnter();
                    firstSet.pageP2.SetAlpha(1f);
                }
            }
        }

        /// <summary>
        /// 외부 JSON 데이터 등을 로드하여 페이지에 주입하기 위한 추상 메서드.
        /// (자식 클래스에서 반드시 구현해야 함)
        /// </summary>
        protected abstract void LoadSettings();

        /// <summary>
        /// 특정 인덱스의 페이지 세트로 전환함 (페이드 아웃 -> 페이드 인 시퀀스).
        /// </summary>
        public virtual void TransitionToPage(int index)
        {
            if (isTransitioning || isFlowFinished) return;
            
            if (index < 0 || index >= pageSets.Count)
            {
                isFlowFinished = true;
                if (currentPageIndex >= 0 && currentPageIndex < pageSets.Count)
                {
                    PageSet currentSet = pageSets[currentPageIndex];
                    if (currentSet.pageP1)
                    {
                        currentSet.pageP1.onStepComplete = null;
                        currentSet.pageP1.OnExit();
                    }
                    if (currentSet.pageP2)
                    {
                        currentSet.pageP2.onStepComplete = null;
                        currentSet.pageP2.OnExit();
                    }
                }
                OnAllFinished();
                return;
            }

            StartCoroutine(PageTransitionRoutine(index));
        }

        /// <summary>
        /// 이전 페이지를 페이드 아웃시키고 새 페이지를 페이드 인시키는 루틴.
        /// </summary>
        private IEnumerator PageTransitionRoutine(int index)
        {
            isTransitioning = true;

            // 1. 이전 페이지 세트 페이드 아웃
            if (currentPageIndex >= 0)
            {
                yield return StartCoroutine(FadePageSet(pageSets[currentPageIndex], 1f, 0f));
                
                PageSet prevSet = pageSets[currentPageIndex];
                if (prevSet.pageP1)
                {
                    prevSet.pageP1.onStepComplete = null;
                    prevSet.pageP1.OnExit();
                }
                if (prevSet.pageP2)
                {
                    prevSet.pageP2.onStepComplete = null;
                    prevSet.pageP2.OnExit();
                }
            }

            currentPageIndex = index;
            PageSet nextSet = pageSets[currentPageIndex];

            // 2. 새 페이지 진입 및 초기화 (알파 0에서 시작)
            if (nextSet.pageP1)
            {
                nextSet.pageP1.onStepComplete = TransitionToNext;
                nextSet.pageP1.OnEnter();
            }
            if (nextSet.pageP2)
            {
                nextSet.pageP2.onStepComplete = TransitionToNext;
                nextSet.pageP2.OnEnter();
            }

            // 3. 새 페이지 세트 페이드 인
            yield return StartCoroutine(FadePageSet(nextSet, 0f, 1f));

            isTransitioning = false;
        }

        /// <summary>
        /// 지정된 페이지 세트의 CanvasGroup 알파값을 조절하여 페이드 효과를 줌.
        /// </summary>
        private IEnumerator FadePageSet(PageSet set, float startAlpha, float endAlpha)
        {
            float elapsed = 0f;
            CanvasGroup cg1 = set.pageP1 ? set.pageP1.GetComponent<CanvasGroup>() : null;
            CanvasGroup cg2 = set.pageP2 ? set.pageP2.GetComponent<CanvasGroup>() : null;

            if (cg1) cg1.alpha = startAlpha;
            if (cg2) cg2.alpha = startAlpha;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float currentAlpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeDuration);

                if (cg1) cg1.alpha = currentAlpha;
                if (cg2) cg2.alpha = currentAlpha;

                yield return null;
            }

            if (cg1) cg1.alpha = endAlpha;
            if (cg2) cg2.alpha = endAlpha;
        }

        protected void TransitionToNext(int trigger)
        {
            TransitionToPage(currentPageIndex + 1);
        }

        protected abstract void OnAllFinished();
    }
}