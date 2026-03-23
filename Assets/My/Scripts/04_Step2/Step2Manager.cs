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

    /// <summary>
    /// Step2 씬의 페이지 전환 흐름을 제어하는 매니저.
    /// Why: 질문 번호에 맞춰 SubCanvas 배경을 교체하고, 단일 폴더 구조의 JSON 파일을 동적으로 로드함.
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

        /// <summary>
        /// 페이드 연출과 배경 교체의 순서를 제어하는 비동기 함수.
        /// </summary>
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

        /// <summary>
        /// SubCanvas 배경 이미지를 서서히 나타나거나 사라지게 함.
        /// </summary>
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

        /// <summary>
        /// 테마와 질문 번호에 맞는 배경 이미지를 Addressables로 로드함.
        /// </summary>
        private async UniTask UpdateSubCanvasBackgroundAsync(int questionNum)
        {
            if (!subCanvasBgImage) return;

            if (_currentBgQuestionNum == questionNum) return;
            _currentBgQuestionNum = questionNum;

            string theme = "Sea_1";
            if (GameManager.Instance)
            {
                theme = GameManager.Instance.Step2ThemeKey;
            }

            string bgKey = $"BG_Step2_{theme}_{questionNum}";

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

        /// <summary>
        /// 세션에 저장된 타입과 동일한 이름의 단일 JSON 파일을 동적으로 로드함.
        /// Why: Enum 이름을 그대로 파일명에 매핑하여 하위 폴더 접근이나 문자열 자르기 연산을 제거함.
        /// </summary>
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

            // 예시 입력값: typeStr이 A1일 때 -> "JSON/Step2/A1"
            string dynamicPath = $"JSON/Step2/{typeStr}";
            
            Step2Setting setting = JsonLoader.Load<Step2Setting>(dynamicPath);

            if (setting == null)
            {
                UnityEngine.Debug.LogWarning($"[Step2Manager] {dynamicPath} 로드 실패. 데이터를 확인할 수 없습니다.");
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