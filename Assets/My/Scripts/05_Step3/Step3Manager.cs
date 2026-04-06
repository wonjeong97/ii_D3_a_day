using System;
using System.Collections;
using System.Collections.Generic;
using My.Scripts.Core;
using My.Scripts.Core.Pages;
using My.Scripts.Global;
using My.Scripts._06_PlayVideo;
using My.Scripts.Core.Data; 
using UnityEngine;
using Wonjeong.Data;
using Wonjeong.Utils;

namespace My.Scripts._05_Step3
{
    /// <summary>
    /// JSON에서 로드되는 Step3 씬의 전체 데이터 구조체.
    /// </summary>
    [Serializable]
    public class Step3Setting
    {
        public CommonBackgroundData background;
        public CommonIntroData introPage;
        public CommonLoadingData loadingPage; 
        public CommonOutroData outroPage;
        
        public CommonQuestionUI commonQuestionUI;
        public CommonResultUI commonResultUI;
        
        public List<QuestionSetItem> questionSets;
    }

    /// <summary>
    /// Step3 씬의 전체 페이지 흐름을 제어하는 매니저.
    /// 인트로, 질문 반복, 로딩, 아웃트로 순서로 씬 시퀀스를 진행함.
    /// </summary>
    public class Step3Manager : BaseFlowManager
    {
        [Header("Background Setup")]
        [SerializeField] private Page_Background backgroundPage;
        
        [Header("Sub Canvas UI")]
        [Tooltip("모니터 2(서브 캔버스)를 페이드 아웃 시킬 검은색 패널의 CanvasGroup")]
        [SerializeField] private CanvasGroup subCanvasFadeCg;

        /// <summary>
        /// 씬 진입 시 배경 비디오 인코딩을 백그라운드에서 시작함.
        /// 다음 씬(PlayVideo)에서 재생할 영상을 미리 준비하여 로딩 지연을 방지하기 위함.
        /// </summary>
        protected override void Start()
        {
            StillcutManager.GenerateVideoInBackground();

            skipFirstPageFade = true;
            base.Start();
        }

        /// <summary>
        /// 외부 JSON 파일에서 데이터를 로드하여 각 페이지 컴포넌트에 분배함.
        /// 반복되는 공통 UI 데이터와 개별 페이지 데이터를 결합하여 메모리에 할당함.
        /// </summary>
        protected override void LoadSettings()
        {
            Step3Setting setting = JsonLoader.Load<Step3Setting>(GameConstants.Path.Step3);

            if (setting == null)
            {
                Debug.LogWarning("[Step3Manager] JSON/Step3 로드 실패. 데이터를 확인할 수 없습니다.");
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
                    intro.SetSyncCommand("STEP3_INTRO_COMPLETE");
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
                        textWait = setting.commonQuestionUI.textWait,
                        textPopupWarning = setting.commonQuestionUI.textPopupWarning,
                        textPopupTimeout = setting.commonQuestionUI.textPopupTimeout
                    };

                    if (pageIndex < pages.Count && pages[pageIndex])
                    {
                        Page_Question qPage = pages[pageIndex] as Page_Question;
                        if (qPage)
                        {
                            qPage.SetSyncCommand($"STEP3_Q_{i}_COMPLETE");
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
                            rPage.SetSyncCommand($"STEP3_R_{i}_COMPLETE");
                            
                            // 각 카메라 페이지에 본인의 문항 번호를 주입하여 마지막 문항(D3) 여부를 판단하게 함.
                            string qId = (i == totalQuestions - 1) ? "D3" : $"Q{i + 1}";
                            rPage.SetQuestionId(qId);
                        }
                        pages[pageIndex].SetupData(rData);
                    }
                    pageIndex++;
                }
            }

            if (pageIndex < pages.Count && pages[pageIndex])
            {
                Page_Loading loading = pages[pageIndex] as Page_Loading;
                if (loading)
                {
                    loading.SetSyncCommands("STEP3_LOADING_READY", "STEP3_LOADING_COMPLETE");
                }
                pages[pageIndex].SetupData(setting.loadingPage);
                pageIndex++;
            }

            if (pageIndex < pages.Count && pages[pageIndex])
            {
                Page_Outro outro = pages[pageIndex] as Page_Outro;
                if (outro)
                {
                    outro.SetSyncCommand("STEP3_OUTRO_COMPLETE");
                }
                pages[pageIndex].SetupData(setting.outroPage);
            }
        }

        /// <summary>
        /// 지정된 인덱스의 페이지로 전환함.
        /// 전환 직전 이전 질문의 응답 데이터를 합성에 활용할 수 있도록 동기화함.
        /// </summary>
        /// <param name="index">전환할 페이지의 인덱스 번호.</param>
        public override void TransitionToPage(int index)
        {
            if (isTransitioning || isFinished) return;
            
            if (currentPageIndex >= 0 && currentPageIndex < pages.Count && index >= 0 && index < pages.Count)
            {
                Page_Question prevQ = pages[currentPageIndex] as Page_Question;
                Page_Camera nextR = pages[index] as Page_Camera;
                
                // 질문(Page_Question)에서 카메라/합성(Page_Camera)으로 넘어갈 때 유저가 고른 답변 번호를 전달함.
                if (prevQ && nextR)
                {
                    nextR.SetAnswerIndex(prevQ.SelectedIndex);
                }

                GamePage prevPage = pages[currentPageIndex];
                GamePage nextPage = pages[index];

                // 로딩 페이지에서 아웃트로 페이지로 넘어갈 때 배경이 검게 보이지 않도록 크로스 페이드 연출을 적용함.
                if (prevPage is Page_Loading && nextPage is Page_Outro)
                {
                    StartCoroutine(CrossFadeTransitionRoutine(prevPage, nextPage, index));
                    return;
                }
            }

            base.TransitionToPage(index);
        }

        /// <summary>
        /// 두 페이지 간의 알파값을 동시에 교차하여 부드러운 화면 전환을 연출함.
        /// 중간에 화면이 까맣게 비는 현상을 방지하기 위함.
        /// </summary>
        private IEnumerator CrossFadeTransitionRoutine(GamePage prevPage, GamePage nextPage, int nextIndex)
        {
            isTransitioning = true;
            currentPageIndex = nextIndex;

            if (nextPage)
            {
                nextPage.onStepComplete = (trigger) => TransitionToNext();
                nextPage.OnEnter();
                nextPage.SetAlpha(0f);
            }

            float elapsed = 0f;
            CanvasGroup prevCg = prevPage ? prevPage.GetComponent<CanvasGroup>() : null;
            CanvasGroup nextCg = nextPage ? nextPage.GetComponent<CanvasGroup>() : null;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;

                if (prevCg) prevCg.alpha = Mathf.Lerp(1f, 0f, t);
                if (nextCg) nextCg.alpha = Mathf.Lerp(0f, 1f, t);

                yield return null;
            }

            if (prevCg) prevCg.alpha = 0f;
            if (nextCg) nextCg.alpha = 1f;

            if (prevPage)
            {
                prevPage.onStepComplete = null;
                prevPage.OnExit();
            }

            isTransitioning = false;
        }

        /// <summary>
        /// 모든 시퀀스가 종료되면 PlayVideo 씬으로 전환함.
        /// 메인 디스플레이가 페이드 아웃될 때 서브 캔버스도 동시에 페이드 아웃되도록 처리함.
        /// </summary>
        protected override void OnAllFinished()
        {
            Debug.Log("[Step3Manager] 내 PC Step3 완료. Video 씬으로 즉시 이동합니다.");

            if (subCanvasFadeCg)
            {
                StartCoroutine(SubCanvasFadeOutRoutine());
            }

            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.PlayVideo, true);
            }
        }

        /// <summary>
        /// 서브 캔버스의 검은색 패널 알파값을 올려 메인 카메라 페이드아웃과 타이밍을 맞춤.
        /// GameManager의 기본 페이드아웃 시간(약 1초)과 동기화됨.
        /// </summary>
        private IEnumerator SubCanvasFadeOutRoutine()
        {
            float elapsed = 0f;
            float duration = 1.0f; 

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (subCanvasFadeCg) subCanvasFadeCg.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
                yield return null;
            }
            
            if (subCanvasFadeCg) subCanvasFadeCg.alpha = 1f;
        }
    }
}