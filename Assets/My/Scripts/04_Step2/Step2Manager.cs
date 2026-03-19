using System;
using System.Collections.Generic;
using System.Threading;
using My.Scripts.Core;
using My.Scripts.Core.Pages;
using My.Scripts.Data;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
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

    /// <summary>
    /// Step2 씬의 페이지 전환 흐름을 제어하는 매니저.
    /// Why: 질문 번호에 맞춰 SubCanvas 배경을 교체하고, 카메라 페이지 진입/퇴장 시 페이드 연출을 수행함.
    /// </summary>
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
        
        // 중복 이미지 로드를 방지하기 위한 캐싱 변수
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

            // Intro(0) 페이지 이후부터 처리함.
            if (index > 0 && index < pages.Count)
            {
                bool isQuestionPage = pages[index] is Page_Question;
                bool isCameraPage = pages[index] is Page_Camera;
                bool isOutroPage = pages[index] is Page_Outro;

                // Why: 질문이나 카메라 페이지인 경우에만 실제 배경 데이터 로드가 필요함.
                if (isQuestionPage || isCameraPage)
                {
                    int questionNum = (index - 1) / 2 + 1; 
                    ProcessBackgroundSequenceAsync(questionNum, isCameraPage).Forget();
                }
                else if (isOutroPage)
                {
                    FadeSubCanvasBackgroundAsync(false).Forget();
                }
            }
        }

        /// <summary>
        /// 페이드 연출과 배경 교체의 순서를 제어하는 비동기 함수.
        /// Why: 캔버스 알파가 0일 때 이미지를 교체해야 화면이 튀는 현상을 방지할 수 있음.
        /// </summary>
        private async UniTaskVoid ProcessBackgroundSequenceAsync(int questionNum, bool isCameraPage)
        {
            if (isCameraPage)
            {
                // 1. 카메라 페이지: 알파 0 상태에서 이미지 교체를 먼저 완료한 후 화면을 켬
                await UpdateSubCanvasBackgroundAsync(questionNum);
                await FadeSubCanvasBackgroundAsync(true);
            }
            else
            {
                // 2. 질문 페이지 등: 화면을 먼저 완전히 끈(페이드 아웃) 뒤, 보이지 않는 상태에서 다음 이미지를 로드함
                await FadeSubCanvasBackgroundAsync(false);
                await UpdateSubCanvasBackgroundAsync(questionNum);
            }
        }

        /// <summary>
        /// SubCanvas 배경 이미지를 서서히 나타나거나 사라지게 함.
        /// </summary>
        private async UniTask FadeSubCanvasBackgroundAsync(bool fadeIn)
        {
            if (!subCanvasBgCg) return;

            _fadeCts?.Cancel();
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
                // 페이드 전환 취소 시 무시
            }
        }

        /// <summary>
        /// 테마와 질문 번호에 맞는 배경 이미지를 Addressables로 로드함.
        /// </summary>
        private async UniTask UpdateSubCanvasBackgroundAsync(int questionNum)
        {
            if (!subCanvasBgImage) return;

            // 질문 페이지와 카메라 페이지가 연속될 때 같은 이미지를 중복 로드하는 것을 방지함
            if (_currentBgQuestionNum == questionNum) return;
            _currentBgQuestionNum = questionNum;

            string mainTheme = string.Empty;
            int subTheme = 0;
            if (GameManager.Instance)
            {
                mainTheme = GameManager.Instance.Step2MainThemeKey;
                subTheme = GameManager.Instance.Step2SubThemeKey;
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
            Step2Setting setting = JsonLoader.Load<Step2Setting>(GameConstants.Path.Step2);

            if (setting == null)
            {
                Debug.LogWarning("[Step2Manager] JSON/Step2 로드 실패. 데이터를 확인할 수 없습니다.");
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
            Debug.Log("[Step2Manager] 내 PC Step2 완료. Step3로 즉시 이동합니다.");

            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.Step3);
            }
        }

        private void OnDestroy()
        {
            if (_bgHandle.IsValid())
            {
                Addressables.Release(_bgHandle);
            }
            
            _fadeCts?.Cancel();
            _fadeCts?.Dispose();
        }
    }
}