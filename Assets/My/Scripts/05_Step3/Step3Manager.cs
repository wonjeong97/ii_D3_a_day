using System;
using System.Collections;
using System.Collections.Generic;
using My.Scripts.Core;
using My.Scripts.Core.Pages;
using My.Scripts.Global;
using My.Scripts._06_PlayVideo;
using My.Scripts.Core.Data; 
using UnityEngine;
using Wonjeong.Data;
using Wonjeong.Utils;

namespace My.Scripts._05_Step3
{
    [Serializable]
    public class Step3Setting
    {
        public CommonBackgroundData background;
        public CommonIntroData introPage;
        
        public CommonLoadingData loadingPage; 
        
        public CommonOutroData outroPage;
        
        public CommonQuestionUI commonQuestionUI;
        public CommonResultUI commonResultUI;
        
        public List<QuestionSetItem> questionSets;
    }

    /// <summary>
    /// Step3 씬의 페이지 전환 흐름을 제어하는 매니저.
    /// 흐름: Intro -> (Question -> Camera 반복) -> Loading -> Outro
    /// </summary>
    public class Step3Manager : BaseFlowManager
    {
        [Header("Background Setup")]
        [SerializeField] private Page_Background backgroundPage;

        protected override void Start()
        {
            // 1. 씬 진입 시점에 Step2 사진들을 영상으로 인코딩 시작
            StillcutManager.GenerateVideoInBackground();

            skipFirstPageFade = true;
            base.Start();
        }

        protected override void LoadSettings()
        {
            Step3Setting setting = JsonLoader.Load<Step3Setting>(GameConstants.Path.Step3);

            if (setting == null)
            {
                UnityEngine.Debug.LogWarning("[Step3Manager] JSON/Step3 로드 실패. 데이터를 확인할 수 없습니다.");
                return;
            }

            if (backgroundPage)
            {
                backgroundPage.SetupData(setting.background);
                backgroundPage.OnEnter();
            }

            // 1. Intro 페이지 설정
            if (pages.Count > 0 && pages[0])
            {
                Page_Intro intro = pages[0] as Page_Intro;
                if (intro)
                {
                    intro.SetSyncCommand("STEP3_INTRO_COMPLETE");
                }
                pages[0].SetupData(setting.introPage);
            }

            int pageIndex = 1;

            // 2. Question -> Camera 반복 설정
            if (setting.questionSets != null)
            {
                int totalQuestions = setting.questionSets.Count;

                for (int i = 0; i < totalQuestions; i++)
                {
                    string progressString = $"{i + 1}/{totalQuestions}";

                    bool hasOverrideDesc = setting.questionSets[i].textDescription != null && 
                                           !string.IsNullOrEmpty(setting.questionSets[i].textDescription.text);

                    TextSetting targetDescription = hasOverrideDesc 
                        ? setting.questionSets[i].textDescription 
                        : setting.commonQuestionUI.textDescription;

                    CommonQuestionPageData qData = new CommonQuestionPageData 
                    {
                        questionSetting = setting.questionSets[i].questionSetting,
                        textSelected = setting.commonQuestionUI.textSelected,
                        textDescription = targetDescription,
                        textWait = setting.commonQuestionUI.textWait,
                        textPopupWarning = setting.commonQuestionUI.textPopupWarning,
                        textPopupTimeout = setting.commonQuestionUI.textPopupTimeout
                    };

                    if (pageIndex < pages.Count && pages[pageIndex])
                    {
                        Page_Question qPage = pages[pageIndex] as Page_Question;
                        if (qPage)
                        {
                            qPage.SetSyncCommand($"STEP3_Q_{i}_COMPLETE");
                            qPage.SetProgressInfo(backgroundPage, progressString);
                        }
                        pages[pageIndex].SetupData(qData);
                    }
                    pageIndex++;

                    CommonResultPageData rData = new CommonResultPageData 
                    {
                        textAnswerComplete = setting.commonResultUI.textAnswerComplete,
                        textMyScene = setting.questionSets[i].textMyScene,
                        textPhotoSaved = setting.commonResultUI.textPhotoSaved
                    };

                    if (pageIndex < pages.Count && pages[pageIndex])
                    {
                        Page_Camera rPage = pages[pageIndex] as Page_Camera;
                        if (rPage)
                        {
                            rPage.SetSyncCommand($"STEP3_R_{i}_COMPLETE");
                        }
                        pages[pageIndex].SetupData(rData);
                    }
                    pageIndex++;
                }
            }

            // 3. Loading 페이지 설정
            if (pageIndex < pages.Count && pages[pageIndex])
            {
                Page_Loading loading = pages[pageIndex] as Page_Loading;
                if (loading)
                {
                    // Why: 로딩 페이지에서 양쪽 PC가 모인 뒤 3초 후 다음 페이지로 넘어가도록 동기화 설정함
                    loading.SetSyncCommands("STEP3_LOADING_READY", "STEP3_LOADING_COMPLETE");
                }
                pages[pageIndex].SetupData(setting.loadingPage);
                pageIndex++;
            }

            // 4. Outro 페이지 설정
            if (pageIndex < pages.Count && pages[pageIndex])
            {
                Page_Outro outro = pages[pageIndex] as Page_Outro;
                if (outro)
                {
                    outro.SetSyncCommand("STEP3_OUTRO_COMPLETE");
                }
                pages[pageIndex].SetupData(setting.outroPage);
            }
        }
        
        public override void TransitionToPage(int index)
        {
            if (isTransitioning || isFinished) return;
            
            if (currentPageIndex >= 0 && currentPageIndex < pages.Count && index >= 0 && index < pages.Count)
            {
                GamePage prevPage = pages[currentPageIndex];
                GamePage nextPage = pages[index];

                // Why: 로딩 페이지에서 아웃트로 페이지로 넘어갈 때 배경이 검게 보이지 않도록 크로스 페이드 연출 적용
                if (prevPage is Page_Loading && nextPage is Page_Outro)
                {
                    StartCoroutine(CrossFadeTransitionRoutine(prevPage, nextPage, index));
                    return;
                }
            }

            base.TransitionToPage(index);
        }

        private IEnumerator CrossFadeTransitionRoutine(GamePage prevPage, GamePage nextPage, int nextIndex)
        {
            isTransitioning = true;
            currentPageIndex = nextIndex;

            if (nextPage)
            {
                nextPage.onStepComplete = (trigger) => TransitionToNext();
                nextPage.OnEnter();
                nextPage.SetAlpha(0f);
            }

            float elapsed = 0f;
            CanvasGroup prevCg = prevPage ? prevPage.GetComponent<CanvasGroup>() : null;
            CanvasGroup nextCg = nextPage ? nextPage.GetComponent<CanvasGroup>() : null;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;

                if (prevCg) prevCg.alpha = Mathf.Lerp(1f, 0f, t);
                if (nextCg) nextCg.alpha = Mathf.Lerp(0f, 1f, t);

                yield return null;
            }

            if (prevCg) prevCg.alpha = 0f;
            if (nextCg) nextCg.alpha = 1f;

            if (prevPage)
            {
                prevPage.onStepComplete = null;
                prevPage.OnExit();
            }

            isTransitioning = false;
        }

        protected override void OnAllFinished()
        {
            UnityEngine.Debug.Log("[Step3Manager] 내 PC Step3 완료. Video 씬으로 즉시 이동합니다.");

            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.PlayVideo, true);
            }
        }
    }
}