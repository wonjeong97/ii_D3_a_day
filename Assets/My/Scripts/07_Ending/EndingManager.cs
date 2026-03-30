using System;
using My.Scripts._07_Ending.Pages;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;
using Wonjeong.Utils;

namespace My.Scripts._07_Ending
{
    [Serializable]
    public class EndingSetting
    {
        public EndingPage1Data page1;
        public EndingPage2Data page2;
        public EndingPage3Data page3;
    }

    /// <summary>
    /// 엔딩 씬의 전체 흐름을 제어하는 매니저.
    /// 흐름: Page1 -> Page2 -> Page3 -> (세션 초기화) -> 독립적으로 타이틀 씬 복귀
    /// </summary>
    public class EndingManager : BaseFlowManager
    {
        protected override void Start()
        {
            skipFirstPageFade = true;
            base.Start();
        }

        protected override void LoadSettings()
        {
            EndingSetting setting = JsonLoader.Load<EndingSetting>(GameConstants.Path.Ending);

            if (setting == null)
            {
                Debug.LogError("[EndingManager] JSON/Ending 로드 실패. 데이터를 확인할 수 없습니다.");
                return;
            }

            if (pages.Count > 0 && pages[0]) pages[0].SetupData(setting.page1);
            if (pages.Count > 1 && pages[1]) pages[1].SetupData(setting.page2);
            if (pages.Count > 2 && pages[2]) pages[2].SetupData(setting.page3);
        }

        protected override void OnAllFinished()
        {
            Debug.Log("[EndingManager] 내 PC 엔딩 완료. 세션을 초기화하고 타이틀 씬으로 각자 복귀합니다.");

            if (GameManager.Instance)
            {
                GameManager.Instance.ReturnToTitle(true); 
            }
        }
    }
}