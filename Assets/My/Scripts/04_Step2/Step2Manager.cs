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
using My.Scripts.Network;
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
    /// Step2 씬의 페이지 흐름을 제어하는 매니저.
    /// Why: 여러 질문과 카메라 촬영 페이지를 순환하며 배경 이미지를 동적으로 교체함.
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
            //  다음 페이지로 넘어가기 직전, 현재 페이지가 질문 페이지였다면 답변 데이터를 API로 전송함
            if (currentPageIndex >= 0 && currentPageIndex < pages.Count)
            {
                Page_Question qPage = pages[currentPageIndex] as Page_Question;
                if (qPage)
                {
                    // 질문 번호 계산 (Q1=1, Q2=2 ... Q15=15)
                    int qNo = (currentPageIndex - 1) / 2 + 1;
                    
                    // 답변 값 매핑 (ㄱ->5, ㄴ2->4, ㄷ->3, ㄹ->2, ㅁ->1)
                    int rawSelection = qPage.SelectedIndex;
                    if (rawSelection >= 1 && rawSelection <= 5)
                    {
                        int mappedValue = 6 - rawSelection;
                        
                        // 서버면 left, 클라이언트면 right
                        string side = (TcpManager.Instance && TcpManager.Instance.IsServer) ? "left" : "right";
                        
                        if (GameManager.Instance)
                        {
                            Debug.Log($"[Step2Manager] API 전송: Q{qNo}, Side: {side}, Value: {mappedValue}");
                            GameManager.Instance.SendValueUpdateAPI(qNo, side, mappedValue);
                        }
                    }
                }
            }

            // 기존 배경 처리 및 페이지 전환 로직 실행
            base.TransitionToPage(index);

            if (index > 0 && index < pages.Count)
            {
                int questionNum = (index - 1) / 2 + 1; 
                bool isCameraPage = pages[index] is Page_Camera; 

                ProcessBackgroundSequenceAsync(questionNum, isCameraPage).Forget();
            }
        }

        /// <summary>
        /// 배경 화면 전환 시퀀스를 비동기로 처리함.
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
            catch (OperationCanceledException) { }
        }

        private async UniTask UpdateSubCanvasBackgroundAsync(int questionNum)
        {
            if (!subCanvasBgImage) return;

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

            if (_bgHandle.IsValid()) Addressables.Release(_bgHandle);

            try
            {
                _bgHandle = Addressables.LoadAssetAsync<Sprite>(bgKey);
                Sprite nextBg = await _bgHandle;
                
                if (nextBg) subCanvasBgImage.sprite = nextBg;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[Step2Manager] 배경 로드 실패: {bgKey}, {e.Message}");
            }
        }

        /// <summary>
        /// 공통 JSON과 개별 JSON을 깊은 복사(Deep Merge) 방식으로 병합함.
        /// Why: C# 직렬화 과정에서 빈 객체로 인해 속성값이 누락되는 현상을 완벽히 방어하기 위함.
        /// </summary>
        protected override void LoadSettings()
        {
            if (!SessionManager.Instance)
            {
                UnityEngine.Debug.LogError("[Step2Manager] SessionManager가 없습니다.");
                return;
            }

            string typeStr = SessionManager.Instance.CurrentUserType.ToString();
            
            if (typeStr.Length < 2 || typeStr == "None")
            {
                UnityEngine.Debug.LogError($"[Step2Manager] 유효하지 않은 UserType입니다: {typeStr}");
                return;
            }

            string commonPath = "JSON/Step2/Common";
            string dynamicPath = $"JSON/Step2/{typeStr}";
            
            Step2Setting commonSetting = JsonLoader.Load<Step2Setting>(commonPath);
            Step2Setting specificSetting = JsonLoader.Load<Step2Setting>(dynamicPath);

            if (specificSetting == null)
            {
                UnityEngine.Debug.LogWarning($"[Step2Manager] {dynamicPath} 로드 실패. 데이터를 확인할 수 없습니다.");
                return;
            }

            // 일반 C# 객체이므로 명시적 null 검사 사용
            if (commonSetting != null)
            {
                if (specificSetting.background == null) specificSetting.background = commonSetting.background;
                if (specificSetting.introPage == null) specificSetting.introPage = commonSetting.introPage;
                if (specificSetting.outroPage == null) specificSetting.outroPage = commonSetting.outroPage;
                
                // 질문 페이지 공통 UI 병합
                if (specificSetting.commonQuestionUI == null) 
                {
                    specificSetting.commonQuestionUI = commonSetting.commonQuestionUI;
                }
                else if (commonSetting.commonQuestionUI != null)
                {
                    if (specificSetting.commonQuestionUI.textSelected == null) specificSetting.commonQuestionUI.textSelected = commonSetting.commonQuestionUI.textSelected;
                    if (specificSetting.commonQuestionUI.textDescription == null) specificSetting.commonQuestionUI.textDescription = commonSetting.commonQuestionUI.textDescription;
                    if (specificSetting.commonQuestionUI.textWait == null) specificSetting.commonQuestionUI.textWait = commonSetting.commonQuestionUI.textWait;
                    if (specificSetting.commonQuestionUI.textPopupWarning == null) specificSetting.commonQuestionUI.textPopupWarning = commonSetting.commonQuestionUI.textPopupWarning;
                    if (specificSetting.commonQuestionUI.textPopupTimeout == null) specificSetting.commonQuestionUI.textPopupTimeout = commonSetting.commonQuestionUI.textPopupTimeout;
                }

                // 결과 페이지 공통 UI 병합
                if (specificSetting.commonResultUI == null) 
                {
                    specificSetting.commonResultUI = commonSetting.commonResultUI;
                }
                else if (commonSetting.commonResultUI != null)
                {
                    if (specificSetting.commonResultUI.textAnswerComplete == null) specificSetting.commonResultUI.textAnswerComplete = commonSetting.commonResultUI.textAnswerComplete;
                    if (specificSetting.commonResultUI.textPhotoSaved == null) specificSetting.commonResultUI.textPhotoSaved = commonSetting.commonResultUI.textPhotoSaved;
                }
            }

            Step2Setting setting = specificSetting;

            // Unity 객체이므로 암시적 boolean 검사 사용
            if (backgroundPage)
            {
                backgroundPage.SetupData(setting.background);
                backgroundPage.OnEnter();
            }

            if (pages.Count > 0 && pages[0])
            {
                Page_Intro intro = pages[0] as Page_Intro;
                if (intro) intro.SetSyncCommand("STEP2_INTRO_COMPLETE");
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
                        textWait = setting.commonQuestionUI.textWait,
                        textPopupWarning = setting.commonQuestionUI.textPopupWarning,
                        textPopupTimeout = setting.commonQuestionUI.textPopupTimeout
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
                        if (rPage) rPage.SetSyncCommand($"STEP2_R_{i}_COMPLETE");
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
            if (_bgHandle.IsValid()) Addressables.Release(_bgHandle);
            
            if (_fadeCts != null)
            {
                _fadeCts.Cancel();
                _fadeCts.Dispose();
            }
        }
    }
}