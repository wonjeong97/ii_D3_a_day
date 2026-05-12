using System;
using My.Scripts.Core;
using My.Scripts._01_Tutorial.Pages;
using UnityEngine;
using My.Scripts.Global;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial
{
    /// <summary>
    /// 튜토리얼 씬의 각 페이지별 JSON 데이터를 매핑하기 위한 데이터 구조체.
    /// </summary>
    [Serializable]
    public class TutorialSetting
    {
        public TutorialPage1Data page1;
        public TutorialPage2Data page2;
        public TutorialPage3Data page3;
        public TutorialPage4Data page4;
        public TutorialPage5Data page5;
        public TutorialPage6Data page6;
    }

    /// <summary>
    /// 튜토리얼 씬의 전반적인 페이지 전환 흐름을 제어하는 매니저.
    /// 모든 페이지 완료 시 다른 PC와의 동기화 대기 없이 즉각적으로 PlayTutorial 씬으로 이동함.
    /// </summary>
    public class TutorialManager : BaseFlowManager
    {
        /// <summary>
        /// 외부 JSON 파일에서 튜토리얼 데이터를 로드하여 각 페이지에 할당함.
        /// 씬 로드 시 하드코딩된 데이터 대신 최신 기획 데이터로 화면을 구성하기 위함.
        /// </summary>
        protected override void LoadSettings()
        {
            string lang = "ko";

            // 전역 세션에서 언어 정보를 조회하여 다국어 경로를 결정함
            if (SessionManager.Instance)
            {
                lang = SessionManager.Instance.CurrentLanguage;

                if (string.IsNullOrEmpty(lang))
                {
                    lang = "ko"; // 언어 설정 누락 시 기본값으로 복구함
                }
            }
            else
            {
                Debug.LogWarning("[TutorialManager] SessionManager 부재로 기본 언어(ko) 적용함.");
            }

            // 언어별 폴더 구조(JSON/{lang}/...)에서 데이터를 불러옴
            string path = $"{GameConstants.Path.ContentRoot}/{lang}/{GameConstants.Path.Tutorial}";
            TutorialSetting setting = JsonLoader.Load<TutorialSetting>(path);

            if (setting == null)
            {
                Debug.LogError($"[TutorialManager] {path} 로드 실패. 파일 유무 확인 필요함.");
                return;
            }
        
            // 각 페이지 컨트롤러의 유효성을 검사한 뒤 데이터를 할당함
            if (pages.Count > 0 && pages[0]) pages[0].SetupData(setting.page1);
            if (pages.Count > 1 && pages[1]) pages[1].SetupData(setting.page2);
            if (pages.Count > 2 && pages[2]) pages[2].SetupData(setting.page3);
            if (pages.Count > 3 && pages[3]) pages[3].SetupData(setting.page4);
            if (pages.Count > 4 && pages[4]) pages[4].SetupData(setting.page5);
            if (pages.Count > 5 && pages[5]) pages[5].SetupData(setting.page6);
        }

        /// <summary>
        /// 전체 페이지 시퀀스가 끝났을 때 호출되어 다음 씬으로 전환함.
        /// 마지막 페이지 UI가 화면에 남은 상태로 자연스럽게 넘어가도록 전역 페이드 아웃을 비활성화함.
        /// </summary>
        protected override void OnAllFinished()
        {
            Debug.Log("[TutorialManager] 내 PC 튜토리얼 완료. PlayTutorial로 각자 이동합니다.");

            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.PlayTutorial, false);
            }
        }
    }
}