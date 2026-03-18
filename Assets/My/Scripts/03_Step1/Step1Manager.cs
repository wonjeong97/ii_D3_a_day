using System;
using System.Collections.Generic; 
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Core.Pages; 
using My.Scripts.Data; 
using UnityEngine;
using Wonjeong.Data;
using Wonjeong.Utils;

namespace My.Scripts._03_Step1
{
    [Serializable]
    public class Step1Setting
    {
        public CommonBackgroundData background; 
        public CommonIntroData introPage;      
        public CommonOutroData outroPage;
        public CommonQuestionUI commonQuestionUI;
        public CommonResultUI commonResultUI;     
        
        public List<QuestionSetItem> questionSets; 
        public List<DynamicAnswerSet> q2DynamicAnswers;
    }
    
    [Serializable]
    public class DynamicAnswerSet
    {
        public TextSetting textAnswer1;
        public TextSetting textAnswer2;
        public TextSetting textAnswer3;
        public TextSetting textAnswer4;
        public TextSetting textAnswer5;
    }

    public class Step1Manager : BaseFlowManager
    {
        [Header("Background Setup")]
        [SerializeField] private Page_Background backgroundPage;

        private Page_Question _q1Page;
        private Page_Question _q2Page;
        private CommonQuestionPageData _q2Data;
        private List<DynamicAnswerSet> _q2DynamicAnswers;

        protected override void Start()
        {
            skipFirstPageFade = true;
            base.Start(); 
        }

        protected override void LoadSettings()
        {
            Step1Setting setting = JsonLoader.Load<Step1Setting>(GameConstants.Path.Step1);

            if (setting == null)
            {
                Debug.LogError("[Step1Manager] JSON 로드 실패.");
                return;
            }

            _q2DynamicAnswers = setting.q2DynamicAnswers;

            if (backgroundPage)
            {
                backgroundPage.SetupData(setting.background);
                backgroundPage.OnEnter();
            }

            if (pages.Count > 0 && pages[0])
            {
                Page_Intro intro = pages[0] as Page_Intro;
                if (intro)
                {
                    intro.SetSyncCommand("STEP1_INTRO_COMPLETE");
                }
                pages[0].SetupData(setting.introPage);
            }

            int pageIndex = 1; 

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
                            if (i == 0) _q1Page = qPage;
                            else if (i == 1) 
                            {
                                _q2Page = qPage;
                                _q2Data = qData;
                            }

                            qPage.SetSyncCommand($"STEP1_Q_{i}_COMPLETE");
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
                            rPage.SetSyncCommand($"STEP1_R_{i}_COMPLETE");
                        }
                        pages[pageIndex].SetupData(rData);
                    }
                    pageIndex++;
                }
            }

            if (pageIndex < pages.Count && pages[pageIndex])
            {
                Page_Outro outro = pages[pageIndex] as Page_Outro;
                if (outro)
                {
                    outro.SetSyncCommand("STEP1_OUTRO_COMPLETE");
                    outro.SetupData(setting.outroPage);
                }
            }
        }
        
        public override void TransitionToPage(int index)
        {
            if (pages != null && index >= 0 && index < pages.Count)
            {
                if (_q2Page && _q1Page && pages[index] == _q2Page)
                {
                    int answerIndex = _q1Page.SelectedIndex - 1; 
                    
                    if (_q2DynamicAnswers != null && answerIndex >= 0 && answerIndex < _q2DynamicAnswers.Count)
                    {
                        DynamicAnswerSet dynamicSet = _q2DynamicAnswers[answerIndex];
                        
                        if (dynamicSet != null)
                        {
                            if (_q2Data != null && _q2Data.questionSetting != null)
                            {
                                _q2Data.questionSetting.textAnswer1 = dynamicSet.textAnswer1;
                                _q2Data.questionSetting.textAnswer2 = dynamicSet.textAnswer2;
                                _q2Data.questionSetting.textAnswer3 = dynamicSet.textAnswer3;
                                _q2Data.questionSetting.textAnswer4 = dynamicSet.textAnswer4;
                                _q2Data.questionSetting.textAnswer5 = dynamicSet.textAnswer5;
                                
                                _q2Page.SetupData(_q2Data);
                            }
                        }
                        else
                        {
                            Debug.LogWarning("[Step1Manager] 매칭되는 DynamicAnswerSet 객체가 null입니다.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[Step1Manager] 매칭되는 dynamicAnswerSets 인덱스가 없습니다.");
                    }
                }
            }
            
            base.TransitionToPage(index);
        }

        protected override void OnAllFinished()
        {
            Debug.Log("[Step1Manager] 내 PC Step1 완료. Step2로 즉시 이동합니다.");

            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.Step2); 
            }
        }
    }
}