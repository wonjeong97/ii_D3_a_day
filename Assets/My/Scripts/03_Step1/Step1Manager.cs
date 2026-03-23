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
        
        // Q1의 답변 인덱스(0~4)에 대응하는 Q2 이미지 조합용 키워드 배열.
        // Why: 어드레서블 그룹에 BG_Step2_Sea_1_1 형식으로 저장되어 있으므로 Sea, Mt 등의 키워드가 필요함.
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
                // Q2 페이지로 전환되는 시점인지 확인
                if (_q2Page && _q1Page && pages[index] == _q2Page)
                {
                    // Q1의 선택 결과를 가져옴 (1~5 값을 0~4 인덱스로 변환)
                    int q1AnsIdx = _q1Page.SelectedIndex - 1; 
                    
                    // 1. Q2 답변 텍스트를 Q1 결과에 맞춰 동적으로 변경 (기존 로직 유지)
                    ApplyDynamicQ2Text(q1AnsIdx);

                    // 2. Q2 답변 보기를 Q1 결과에 맞춰 비동기로 로드 및 교체 (요청된 예외 로직)
                    ApplyDynamicQ2ImagesAsync(q1AnsIdx).Forget();
                }
            }
            
            base.TransitionToPage(index);
        }

        /// <summary>
        /// Q1 답변 인덱스를 기반으로 Q2 답변 텍스트를 동적으로 적용함.
        /// </summary>
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

        /// <summary>
        /// Q1 답변 인덱스를 기반으로 어드레서블 키를 조합하여 Q2 이미지를 비동기로 로드하도록 지시함.
        /// Why: 요청하신 "Q1의 답에 따라 Q2의 답변 보기 이미지가 변경되어야 해" 로직을 수행함.
        /// </summary>
        private async UniTaskVoid ApplyDynamicQ2ImagesAsync(int q1AnsIdx)
        {
            if (q1AnsIdx < 0 || q1AnsIdx >= _q1KeywordMap.Length) return;

            // Q1 답변에 해당하는 키워드 (예: Sea)를 가져옴
            string keyword = _q1KeywordMap[q1AnsIdx];
            
            // 5개의 어드레서블 키 배열을 조합함. 예: BG_Step2_Sea_1_1
            string[] specificKeys = new string[5];
            for (int i = 0; i < 5; i++)
            {
                specificKeys[i] = $"BG_Step2_{keyword}_{i + 1}_1";
            }
            
            // Q2 페이지 객체에게 조합된 키 배열을 넘겨 비동기 로드 및 세팅을 지시함.
            // Q2Page는 이 호출을 받으면 OnEnter에서의 기본 카트리지 로드를 건너뛰게 됨.
            await _q2Page.LoadAndSetSpecificImagesAsync(specificKeys);
        }

        protected override void OnAllFinished()
        {
            Debug.Log("[Step1Manager] 내 PC Step1 완료. Step2로 즉시 이동합니다.");
            if (GameManager.Instance) GameManager.Instance.ChangeScene(GameConstants.Scene.Step2, true); 
        }
    }
}