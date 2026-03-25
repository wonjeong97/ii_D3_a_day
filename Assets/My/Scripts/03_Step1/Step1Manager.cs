using System;
using System.Collections.Generic; 
using My.Scripts.Core;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Core.Pages;
using UnityEngine;
using Wonjeong.Data;
using Wonjeong.Utils;
using Cysharp.Threading.Tasks;

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
        
        private readonly string[] _q1KeywordMap = new string[] { "Sea", "Mt", "River", "Sky", "Forest" };

        protected override void Start()
        {
            skipFirstPageFade = true;
            base.Start(); 
        }

        protected override void LoadSettings()
        {
            Step1Setting settings = JsonLoader.Load<Step1Setting>(GameConstants.Path.Step1);

            if (settings == null)
            {
                Debug.LogError("[Step1Manager] JSON 로드 실패.");
                return;
            }

            _q2DynamicAnswers = settings.q2DynamicAnswers;

            if (backgroundPage)
            {
                backgroundPage.SetupData(settings.background);
                backgroundPage.OnEnter();
            }

            if (pages.Count > 0 && pages[0])
            {
                Page_Intro intro = pages[0] as Page_Intro;
                if (intro) intro.SetSyncCommand("STEP1_INTRO_COMPLETE");
                pages[0].SetupData(settings.introPage);
            }

            int pageIndex = 1; 

            if (settings.questionSets != null)
            {
                int totalQuestions = settings.questionSets.Count;

                for (int i = 0; i < totalQuestions; i++)
                {
                    string progressString = $"{i + 1}/{totalQuestions}";
                    
                    bool hasOverrideDesc = settings.questionSets[i].textDescription != null && 
                                           !string.IsNullOrEmpty(settings.questionSets[i].textDescription.text);

                    TextSetting targetDescription = hasOverrideDesc 
                        ? settings.questionSets[i].textDescription 
                        : settings.commonQuestionUI.textDescription;

                    CommonQuestionPageData qData = new CommonQuestionPageData 
                    {
                        questionSetting = settings.questionSets[i].questionSetting,
                        textSelected = settings.commonQuestionUI.textSelected,
                        textDescription = targetDescription,
                        textWait = settings.commonQuestionUI.textWait
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
                        textAnswerComplete = settings.commonResultUI.textAnswerComplete,
                        textMyScene = settings.questionSets[i].textMyScene,
                        textPhotoSaved = settings.commonResultUI.textPhotoSaved
                    };

                    if (pageIndex < pages.Count && pages[pageIndex])
                    {
                        Page_Camera rPage = pages[pageIndex] as Page_Camera;
                        if (rPage) rPage.SetSyncCommand($"STEP1_R_{i}_COMPLETE");
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
                    outro.SetupData(settings.outroPage);
                }
            }
        }
        
        public override void TransitionToPage(int index)
        {
            if (pages != null && index >= 0 && index < pages.Count)
            {
                if (_q2Page && _q1Page && pages[index] == _q2Page)
                {
                    int q1AnsIdx = _q1Page.SelectedIndex - 1; 
                    
                    ApplyDynamicQ2Text(q1AnsIdx);
                    ApplyDynamicQ2ImagesAsync(q1AnsIdx).Forget();
                }
            }
            
            base.TransitionToPage(index);
        }

        private void ApplyDynamicQ2Text(int q1AnsIdx)
        {
            if (_q2DynamicAnswers != null && q1AnsIdx >= 0 && q1AnsIdx < _q2DynamicAnswers.Count)
            {
                DynamicAnswerSet dynamicSet = _q2DynamicAnswers[q1AnsIdx];
                if (dynamicSet != null && _q2Data != null && _q2Data.questionSetting != null)
                {
                    _q2Data.questionSetting.textAnswer1 = dynamicSet.textAnswer1;
                    _q2Data.questionSetting.textAnswer2 = dynamicSet.textAnswer2;
                    _q2Data.questionSetting.textAnswer3 = dynamicSet.textAnswer3;
                    _q2Data.questionSetting.textAnswer4 = dynamicSet.textAnswer4;
                    _q2Data.questionSetting.textAnswer5 = dynamicSet.textAnswer5;
                    _q2Page.SetupData(_q2Data);
                }
            }
        }

        private async UniTaskVoid ApplyDynamicQ2ImagesAsync(int q1AnsIdx)
        {
            if (q1AnsIdx < 0 || q1AnsIdx >= _q1KeywordMap.Length) return;

            string keyword = _q1KeywordMap[q1AnsIdx];
            
            string[] specificKeys = new string[5];
            for (int i = 0; i < 5; i++)
            {
                specificKeys[i] = $"BG_Step2_{keyword}_{i + 1}_1";
            }
            
            await _q2Page.LoadAndSetSpecificImagesAsync(specificKeys);
        }

        protected override void OnAllFinished()
        {
            // Step1이 모두 끝나면 Q1과 Q2의 최종 답변을 가져와 세션 매니저의 인게임 진행 데이터로 확정함.
            if (_q1Page && _q2Page && SessionManager.Instance)
            {
                int q1Idx = _q1Page.SelectedIndex - 1;
                int q2Selection = _q2Page.SelectedIndex; 

                if (q1Idx >= 0 && q1Idx < _q1KeywordMap.Length)
                {
                    SessionManager.Instance.Step2MainTheme = _q1KeywordMap[q1Idx];
                }
                
                if (q2Selection >= 1 && q2Selection <= 5)
                {
                    SessionManager.Instance.Step2SubTheme = q2Selection;
                }
            }

            if (GameManager.Instance) GameManager.Instance.ChangeScene(GameConstants.Scene.Step2, true); 
        }
    }
}