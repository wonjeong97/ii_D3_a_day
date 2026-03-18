using System;
using System.Collections.Generic;
using My.Scripts.Core;
using My.Scripts.Core.Pages;
using My.Scripts.Data;
using My.Scripts.Global;
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
        
        // 추가됨: 아웃트로 전에 나올 로딩 화면 데이터
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
            skipFirstPageFade = true;
            base.Start();
        }

        protected override void LoadSettings()
        {
            Step3Setting setting = JsonLoader.Load<Step3Setting>(GameConstants.Path.Step3);

            if (setting == null)
            {
                Debug.LogWarning("[Step3Manager] JSON/Step3 로드 실패. 데이터를 확인할 수 없습니다.");
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
                        textWait = setting.commonQuestionUI.textWait
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
                    // 로딩 페이지는 스스로 5초 뒤에 다음으로 넘어가므로 별도의 동기화 커맨드가 필요 없습니다.
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

        protected override void OnAllFinished()
        {
            Debug.Log("[Step3Manager] 내 PC Step3 완료. Video 씬으로 즉시 이동합니다.");

            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.PlayVideo);
            }
        }
    }
}