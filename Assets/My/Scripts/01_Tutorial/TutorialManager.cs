using System;
using My.Scripts.Core;
using My.Scripts._01_Tutorial.Pages;
using UnityEngine;
using My.Scripts.Global;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial
{
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
    /// 튜토리얼 씬의 페이지 전환을 관리하는 매니저.
    /// 흐름: 모든 페이지 완료 시 다른 PC를 기다리지 않고 각자 PlayTutorial 씬으로 넘어갑니다.
    /// </summary>
    public class TutorialManager : BaseFlowManager
    {
        protected override void LoadSettings()
        {
            TutorialSetting setting = JsonLoader.Load<TutorialSetting>(GameConstants.Path.Tutorial);

            if (setting == null)
            {
                Debug.LogError("[TutorialManager] Tutorial.json 로드 실패.");
                return;
            }
            
            if (pages.Count > 0 && pages[0]) pages[0].SetupData(setting.page1);
            if (pages.Count > 1 && pages[1]) pages[1].SetupData(setting.page2);
            if (pages.Count > 2 && pages[2]) pages[2].SetupData(setting.page3);
            if (pages.Count > 3 && pages[3]) pages[3].SetupData(setting.page4);
            if (pages.Count > 4 && pages[4]) pages[4].SetupData(setting.page5);
            if (pages.Count > 5 && pages[5]) pages[5].SetupData(setting.page6);
        }

        protected override void OnAllFinished()
        {
            Debug.Log("[TutorialManager] 내 PC 튜토리얼 완료. PlayTutorial로 각자 이동합니다.");

            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.PlayTutorial, true);
            }
        }
    }
}