using System;
using System.Collections.Generic;
using System.Threading;
using My.Scripts.Core;
using My.Scripts.Core.Pages;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using My.Scripts.Core.Data;
using Wonjeong.Data;
using Wonjeong.Utils;

namespace My.Scripts._04_Step2
{
    [Serializable]
    public class Step2Setting
    {
        public CommonBackgroundData background;
        public CommonIntroData introPage;
        public CommonOutroData outroPage;
        
        public CommonQuestionUI commonQuestionUI;
        public CommonResultUI commonResultUI;
        
        public List<QuestionSetItem> questionSets;
    }

    public class Step2Manager : BaseFlowManager
    {
        [Header("Background Setup")]
        [SerializeField] private Page_Background backgroundPage;

        [Header("Dynamic SubCanvas Background")]
        [SerializeField] private Image subCanvasBgImage; 
        [SerializeField] private CanvasGroup subCanvasBgCg;
        [SerializeField] private float bgFadeDuration = 0.5f;

        private AsyncOperationHandle<Sprite> _bgHandle;
        private CancellationTokenSource _fadeCts;
        
        private int _currentBgQuestionNum = -1; 

        protected override void Start()
        {
            skipFirstPageFade = true;
            
            if (subCanvasBgCg)
            {
                subCanvasBgCg.alpha = 0f;
            }
            base.Start();
        }

        public override void TransitionToPage(int index)
        {
            base.TransitionToPage(index);

            if (index > 0 && index < pages.Count)
            {
                int questionNum = (index - 1) / 2 + 1; 
                bool isCameraPage = pages[index] is Page_Camera; 

                ProcessBackgroundSequenceAsync(questionNum, isCameraPage).Forget();
            }
        }

        private async UniTaskVoid ProcessBackgroundSequenceAsync(int questionNum, bool isCameraPage)
        {
            if (isCameraPage)
            {
                await UpdateSubCanvasBackgroundAsync(questionNum);
                await FadeSubCanvasBackgroundAsync(true);
            }
            else
            {
                await FadeSubCanvasBackgroundAsync(false);
                await UpdateSubCanvasBackgroundAsync(questionNum);
            }
        }

        private async UniTask FadeSubCanvasBackgroundAsync(bool fadeIn)
        {
            if (!subCanvasBgCg) return;

            if (_fadeCts != null)
            {
                _fadeCts.Cancel();
                _fadeCts.Dispose();
            }
            
            _fadeCts = new CancellationTokenSource();
            CancellationToken token = _fadeCts.Token;

            float startAlpha = subCanvasBgCg.alpha;
            float endAlpha = fadeIn ? 1f : 0f;
            float elapsed = 0f;

            try
            {
                while (elapsed < bgFadeDuration)
                {
                    if (token.IsCancellationRequested) return;

                    elapsed += Time.deltaTime;
                    subCanvasBgCg.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / bgFadeDuration);
                    
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                if (!token.IsCancellationRequested)
                {
                    subCanvasBgCg.alpha = endAlpha;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTask UpdateSubCanvasBackgroundAsync(int questionNum)
        {
            if (!subCanvasBgImage) return;

            // Why: 질문 개수(15개)를 초과하는 인덱스(예: Outro 진입 시 계산되는 16)에 대한 배경 로드 시도를 차단하여 에러를 방지함.
            if (questionNum > 15) return;

            if (_currentBgQuestionNum == questionNum) return;
            _currentBgQuestionNum = questionNum;

            string mainTheme = "Sea";
            int subTheme = 1;

            if (SessionManager.Instance)
            {
                mainTheme = SessionManager.Instance.Step2MainTheme;
                subTheme = SessionManager.Instance.Step2SubTheme;
            }

            string bgKey = $"BG_Step2_{mainTheme}_{subTheme}_{questionNum}";

            if (_bgHandle.IsValid())
            {
                Addressables.Release(_bgHandle);
            }

            try
            {
                _bgHandle = Addressables.LoadAssetAsync<Sprite>(bgKey);
                Sprite nextBg = await _bgHandle;
                
                if (nextBg) 
                {
                    subCanvasBgImage.sprite = nextBg;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Step2Manager] 배경 로드 실패: {bgKey}, {e.Message}");
            }
        }

        protected override void LoadSettings()
        {
            if (!SessionManager.Instance)
            {
                Debug.LogError("[Step2Manager] SessionManager가 없습니다.");
                return;
            }

            string typeStr = SessionManager.Instance.CurrentUserType.ToString();
            
            if (typeStr.Length < 2 || typeStr == "None")
            {
                Debug.LogError($"[Step2Manager] 유효하지 않은 UserType입니다: {typeStr}");
                return;
            }

            string dynamicPath = $"JSON/Step2/{typeStr}";
            
            Step2Setting setting = JsonLoader.Load<Step2Setting>(dynamicPath);

            if (setting == null)
            {
                Debug.LogWarning($"[Step2Manager] {dynamicPath} 로드 실패. 데이터를 확인할 수 없습니다.");
                return;
            }

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
                    intro.SetSyncCommand("STEP2_INTRO_COMPLETE");
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
                            qPage.SetSyncCommand($"STEP2_Q_{i}_COMPLETE");
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
                            rPage.SetSyncCommand($"STEP2_R_{i}_COMPLETE");
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
                    outro.SetSyncCommand("STEP2_OUTRO_COMPLETE");
                    outro.SetupData(setting.outroPage);
                }
            }
        }

        protected override void OnAllFinished()
        {
            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.Step3, true);
            }
        }

        private void OnDestroy()
        {
            if (_bgHandle.IsValid())
            {
                Addressables.Release(_bgHandle);
            }
            
            if (_fadeCts != null)
            {
                _fadeCts.Cancel();
                _fadeCts.Dispose();
            }
        }
    }
}