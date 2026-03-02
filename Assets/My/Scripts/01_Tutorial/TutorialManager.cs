using System;
using My.Scripts.Core;
using My.Scripts._01_Tutorial.Pages;
using UnityEngine;
using My.Scripts.Global;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial
{
    /// <summary>
    /// JSON 데이터 파싱을 위한 루트 설정 클래스.
    /// </summary>
    [Serializable]
    public class TutorialSetting
    {
        public TutorialPage1Data page1;
        public TutorialPage2Data page2;
        public TutorialPage3Data page3;
    }

    /// <summary>
    /// 튜토리얼 씬의 전체 진행 순서와 페이지 전환을 관리하는 매니저.
    /// P1, P2 페이지 세트를 제어하며 외부 JSON 데이터를 주입함.
    /// </summary>
    public class TutorialManager : BaseFlowManager
    {
        /// <summary>
        /// 튜토리얼에 필요한 외부 데이터를 로드하여 각 페이지 세트에 전달함.
        /// </summary>
        protected override void LoadSettings()
        {
            // 지정된 경로에서 전체 튜토리얼 설정 데이터 로드
            TutorialSetting setting = JsonLoader.Load<TutorialSetting>(GameConstants.Path.Tutorial);

            if (setting == null)
            {
                Debug.LogError("[TutorialManager] Tutorial.json 로드에 실패했습니다. 경로를 확인하세요.");
                return;
            }
            
            if (pageSets.Count > 0) // 첫 번째 페이지 세트(Page1) 데이터 주입
            {
                PageSet firstSet = pageSets[0];
                if (firstSet.pageP1) firstSet.pageP1.SetupData(setting.page1);
                if (firstSet.pageP2) firstSet.pageP2.SetupData(setting.page1);
            }
            if (pageSets.Count > 1) // 두 번째 페이지 세트(Page2) 데이터 주입
            {
                PageSet secondSet = pageSets[1];
                if (secondSet.pageP1) secondSet.pageP1.SetupData(setting.page2);
                if (secondSet.pageP2) secondSet.pageP2.SetupData(setting.page2);
            }
            if (pageSets.Count > 2) // 세 번째 페이지 세트(Page3) 데이터 주입
            {
                PageSet thirdSet = pageSets[2];
                if (thirdSet.pageP1) thirdSet.pageP1.SetupData(setting.page3);
                if (thirdSet.pageP2) thirdSet.pageP2.SetupData(setting.page3);
            }
        }

        /// <summary>
        /// 모든 튜토리얼 단계가 끝나면 실제 플레이 튜토리얼 씬으로 전환함.
        /// </summary>
        protected override void OnAllFinished()
        {
            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.PlayTutorial);
            }
            else
            {
                Debug.LogError("TutorialManager: GameManager가 존재하지 않아 씬을 전환할 수 없습니다.");
            }
        }

        private void Update()
        {
            // 페이지 전환 연출 중에는 추가적인 로직 실행을 차단하여 흐름 꼬임 방지
            if (isTransitioning)
            {
                return;
            }
        }
    }
}