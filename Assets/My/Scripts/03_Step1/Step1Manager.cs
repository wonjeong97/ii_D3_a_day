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
    /// <summary>
    /// JSON에서 로드되는 Step1 씬의 전체 데이터 구조체.
    /// </summary>
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
    
    /// <summary>
    /// 첫 번째 질문의 결과에 따라 동적으로 변하는 두 번째 질문의 답변 데이터 구조체.
    /// </summary>
    [Serializable]
    public class DynamicAnswerSet
    {
        public TextSetting textAnswer1;
        public TextSetting textAnswer2;
        public TextSetting textAnswer3;
        public TextSetting textAnswer4;
        public TextSetting textAnswer5;
    }

    /// <summary>
    /// Step1 씬의 전체 페이지 흐름을 제어하는 매니저.
    /// 첫 번째와 두 번째 질문의 연계 논리 및 세션 저장을 담당함.
    /// </summary>
    public class Step1Manager : BaseFlowManager
    {
        [Header("Background Setup")]
        [SerializeField] private Page_Background backgroundPage;

        private Page_Question _q1Page;
        private Page_Question _q2Page;
        private CommonQuestionPageData _q2Data;
        private List<DynamicAnswerSet> _q2DynamicAnswers;
        
        // 첫 번째 질문의 응답 인덱스를 배경 테마 키워드로 변환하는 매핑 배열. 예: 인덱스 0 입력 시 "Sea" 반환.
        private readonly string[] _q1KeywordMap = new string[] { "Sea", "Mt", "River", "Sky", "Forest" };

        /// <summary>
        /// 매니저 초기화 시 부모 클래스의 로직을 호출함.
        /// 씬 진입 시 첫 페이지가 페이드 없이 즉시 노출되도록 플래그를 설정함.
        /// </summary>
        protected override void Start()
        {
            skipFirstPageFade = true;
            base.Start(); 
        }

        /// <summary>
        /// 외부 JSON 파일에서 데이터를 로드하여 각 페이지 컴포넌트에 분배함.
        /// 반복되는 공통 UI 데이터와 개별 페이지 데이터를 결합하여 메모리에 할당함.
        /// </summary>
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
                        textWait = settings.commonQuestionUI.textWait,
                        textPopupWarning = settings.commonQuestionUI.textPopupWarning,
                        textPopupTimeout = settings.commonQuestionUI.textPopupTimeout
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
                                _q2Page.SetDynamicImageMode(true);
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
        
        /// <summary>
        /// 특정 페이지로 전환하기 전 필요한 사전 작업을 수행함.
        /// Q1 답변이 완료된 시점에 즉시 Q2 이미지와 텍스트를 로드하여 유저가 페이지를 보기 전 준비를 마침.
        /// </summary>
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

        /// <summary>
        /// 첫 번째 질문의 결과에 따라 두 번째 질문의 답변 텍스트를 교체함.
        /// Q1의 응답 인덱스에 매칭되는 하위 답변 세트를 찾아 Q2의 UI 데이터에 덮어씌움.
        /// </summary>
        /// <param name="q1AnsIdx">첫 번째 질문에서 선택한 답변의 인덱스 (0부터 시작).</param>
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
        /// 첫 번째 질문의 결과에 따라 두 번째 질문의 답변 이미지를 교체함.
        /// 어드레서블 시스템을 사용하여 맞춤형 배경 이미지를 비동기로 불러옴.
        /// </summary>
        /// <param name="q1AnsIdx">첫 번째 질문에서 선택한 답변의 인덱스 (0부터 시작).</param>
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

        /// <summary>
        /// Step1의 모든 페이지 시퀀스가 끝났을 때 호출됨.
        /// 유저가 선택한 결과를 세션에 저장하여 Step2의 배경 테마로 활용하고 다음 씬으로 전환함.
        /// </summary>
        protected override void OnAllFinished()
        {
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