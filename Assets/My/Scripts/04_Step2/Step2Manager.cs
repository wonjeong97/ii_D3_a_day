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
    /// <summary>
    /// JSON에서 로드되는 Step2 씬의 전체 데이터 구조체.
    /// </summary>
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
    /// Step2 씬의 전체 페이지 흐름을 제어하는 매니저.
    /// 여러 질문과 카메라 촬영 페이지를 순환하며 배경 이미지를 동적으로 교체함.
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

        /// <summary>
        /// 씬 진입 시 배경 캔버스를 투명하게 초기화하고 첫 페이지 페이드 인 연출을 생략함.
        /// </summary>
        protected override void Start()
        {
            skipFirstPageFade = true;
            
            if (subCanvasBgCg)
            {
                subCanvasBgCg.alpha = 0f;
            }
            base.Start();
        }

        /// <summary>
        /// 특정 페이지로 전환하기 전 이전 페이지의 응답 데이터를 전송하고 배경 전환을 수행함.
        /// 질문 결과값을 역순으로 매핑하여 서버에 동기화함. 예: 입력 5 -> 전송 1
        /// </summary>
        /// <param name="index">전환할 페이지의 인덱스 번호.</param>
        public override void TransitionToPage(int index)
        {
            if (currentPageIndex >= 0 && currentPageIndex < pages.Count)
            {
                Page_Question qPage = pages[currentPageIndex] as Page_Question;
                if (qPage)
                {
                    int qNo = (currentPageIndex - 1) / 2 + 1;
                    int rawSelection = qPage.SelectedIndex;
                    
                    if (rawSelection >= 1 && rawSelection <= 5)
                    {
                        int mappedValue = 6 - rawSelection;
                        string side = (TcpManager.Instance && TcpManager.Instance.IsServer) ? "left" : "right";
                        
                        if (GameManager.Instance)
                        {
                            Debug.Log($"[Step2Manager] API 전송: Q{qNo}, Side: {side}, Value: {mappedValue}");
                            GameManager.Instance.SendValueUpdateAPI(qNo, side, mappedValue);
                        }
                    }
                }
            }

            base.TransitionToPage(index);

            if (index > 0 && index < pages.Count)
            {
                int questionNum = (index - 1) / 2 + 1; 
                bool isCameraPage = pages[index] is Page_Camera; 

                ProcessBackgroundSequenceAsync(questionNum, isCameraPage).Forget();
            }
        }

        /// <summary>
        /// 대상 페이지 유형에 맞춰 배경 이미지 갱신과 페이드 연출 순서를 다르게 적용함.
        /// 카메라 촬영 화면에서는 촬영 대상 확보를 위해 이미지를 먼저 교체함.
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
        /// 배경 캔버스 그룹의 알파값을 조절하여 부드러운 전환을 연출함.
        /// 기존 진행 중인 연출을 취소하여 애니메이션 충돌을 방지함.
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
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// 이전 스텝에서 확정된 테마와 현재 질문 번호에 맞는 배경 이미지를 로드함.
        /// 어드레서블 자원을 비동기로 호출하여 메인 스레드 부하를 줄임.
        /// </summary>
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
                Debug.LogError($"[Step2Manager] 배경 로드 실패: {bgKey}, {e.Message}");
            }
        }

        /// <summary>
        /// 공통 설정과 개별 설정을 깊은 복사 방식으로 병합하여 페이지 데이터를 구성함.
        /// 직렬화 과정에서 발생하는 빈 객체 누락 현상을 방어하기 위함.
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

            string commonPath = "JSON/Step2/Common";
            string dynamicPath = $"JSON/Step2/{typeStr}";
            
            Step2Setting commonSetting = JsonLoader.Load<Step2Setting>(commonPath);
            Step2Setting specificSetting = JsonLoader.Load<Step2Setting>(dynamicPath);

            if (specificSetting == null)
            {
                Debug.LogWarning($"[Step2Manager] {dynamicPath} 로드 실패. 데이터를 확인할 수 없습니다.");
                return;
            }

            if (commonSetting != null)
            {
                if (specificSetting.background == null) specificSetting.background = commonSetting.background;
                if (specificSetting.introPage == null) specificSetting.introPage = commonSetting.introPage;
                if (specificSetting.outroPage == null) specificSetting.outroPage = commonSetting.outroPage;
                
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

        /// <summary>
        /// 모든 질문 및 촬영 절차가 종료되면 다음 씬으로 전환함.
        /// </summary>
        protected override void OnAllFinished()
        {
            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.Step3, true);
            }
        }

        /// <summary>
        /// 매니저 파괴 시 할당된 어드레서블 핸들과 비동기 토큰을 해제함.
        /// 씬 전환 시 메모리 누수를 방어하기 위함.
        /// </summary>
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