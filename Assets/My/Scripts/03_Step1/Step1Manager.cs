using System;
using System.Collections.Generic; 
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Core.Pages; 
using My.Scripts.Data; 
using UnityEngine;
using UnityEngine.AddressableAssets; 
using UnityEngine.ResourceManagement.AsyncOperations; 
using Cysharp.Threading.Tasks; 
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

        [Header("Q2 Dynamic Theme Keys")]
        [Tooltip("Q1의 1~5번 선택지에 대응하는 테마 키워드.")]
        [SerializeField] private string[] q1Themes = new string[] { "Sea", "Mt", "River", "Sky", "Forest" };

        private Page_Question _q1Page;
        private Page_Question _q2Page;
        private CommonQuestionPageData _q2Data;
        private List<DynamicAnswerSet> _q2DynamicAnswers;

        private List<AsyncOperationHandle<Sprite>> _q2SpriteHandles = new List<AsyncOperationHandle<Sprite>>();

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
                UnityEngine.Debug.LogError("[Step1Manager] JSON 로드 실패.");
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
                if (intro) intro.SetSyncCommand("STEP1_INTRO_COMPLETE");
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
                    TransitionToQ2Async(index).Forget();
                    return; 
                }
            }
            
            base.TransitionToPage(index);
        }

        private async UniTaskVoid TransitionToQ2Async(int targetIndex)
        {
            int answerIndex = _q1Page.SelectedIndex - 1; 

            if (_q2DynamicAnswers != null && answerIndex >= 0 && answerIndex < _q2DynamicAnswers.Count)
            {
                DynamicAnswerSet dynamicSet = _q2DynamicAnswers[answerIndex];
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

            ReleaseQ2Sprites(); 
            Sprite[] loadedSprites = new Sprite[5];

            if (q1Themes != null && answerIndex >= 0 && answerIndex < q1Themes.Length)
            {
                string selectedTheme = q1Themes[answerIndex];

                // Why: Q1의 선택 결과를 바탕으로 메인 테마 키를 저장함
                if (GameManager.Instance)
                {
                    GameManager.Instance.Step2MainThemeKey = selectedTheme;
                }
                
                List<UniTask<Sprite>> loadTasks = new List<UniTask<Sprite>>();
                for (int i = 1; i <= 5; i++)
                {
                    string key = $"BG_Step2_{selectedTheme}_{i}_1";
                    loadTasks.Add(LoadSpriteAsync(key));
                }

                loadedSprites = await UniTask.WhenAll(loadTasks);
                _q2Page.ChangeAnswerImages(loadedSprites);
            }
            else
            {
                UnityEngine.Debug.LogWarning("[Step1Manager] 인스펙터에 설정된 q1Themes 배열과 매칭되는 값이 없습니다.");
            }

            base.TransitionToPage(targetIndex);
        }

        private async UniTask<Sprite> LoadSpriteAsync(string key)
        {
            try
            {
                AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(key);
                _q2SpriteHandles.Add(handle);
                return await handle.ToUniTask();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[Step1Manager] Q2 이미지 로드 실패 (Key: {key}): {e.Message}");
                return null;
            }
        }

        private void ReleaseQ2Sprites()
        {
            foreach (AsyncOperationHandle<Sprite> handle in _q2SpriteHandles)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
            _q2SpriteHandles.Clear();
        }

        /// <summary>
        /// 모든 페이지 연출이 종료되었을 때 호출됩니다.
        /// Why: Step1의 마지막 질문(Q2)에 대한 답변을 SubThemeKey로 저장하고 다음 씬으로 이동합니다.
        /// </summary>
        protected override void OnAllFinished()
        {
            UnityEngine.Debug.Log("[Step1Manager] 내 PC Step1 완료. Step2로 이동합니다.");

            if (GameManager.Instance)
            {
                // Q2의 선택 결과(1~5)를 SubThemeKey로 저장함
                if (_q2Page)
                {
                    GameManager.Instance.Step2SubThemeKey = _q2Page.SelectedIndex;
                }

                // 페이드 연출을 포함하여 Step 2 씬으로 전환함
                GameManager.Instance.ChangeScene(GameConstants.Scene.Step2, true); 
            }
        }

        private void OnDestroy()
        {
            ReleaseQ2Sprites(); 
        }
    }
}