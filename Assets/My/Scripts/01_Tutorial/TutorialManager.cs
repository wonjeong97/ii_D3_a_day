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
    /// 튜토리얼 4, 5페이지 데이터를 추가로 매핑함.
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
    /// 튜토리얼 씬의 전체 진행 순서와 페이지 전환을 관리하는 매니저.
    /// P1, P2 페이지 세트를 제어하며 외부 JSON 데이터를 주입함.
    /// </summary>
    public class TutorialManager : BaseFlowManager
    {
        /// <summary> 튜토리얼에 필요한 외부 데이터를 로드하여 각 페이지 세트에 전달함. </summary>
        protected override void LoadSettings()
        {
            // 지정된 경로에서 전체 튜토리얼 설정 데이터 로드
            TutorialSetting setting = JsonLoader.Load<TutorialSetting>(GameConstants.Path.Tutorial);

            if (setting == null)
            {
                Debug.LogError("[TutorialManager] Tutorial.json 로드에 실패했습니다. 경로를 확인하세요.");
                return;
            }
            
            // 물리적으로 분리된 두 디스플레이(P1, P2)가 동일한 진행 상황과 텍스트를 유지해야 하므로 각각 독립적으로 데이터를 주입함
            if (pageSets.Count > 0)
            {
                PageSet firstSet = pageSets[0];
                if (firstSet.pageP1) firstSet.pageP1.SetupData(setting.page1);
                if (firstSet.pageP2) firstSet.pageP2.SetupData(setting.page1);
            }

            if (pageSets.Count > 1)
            {
                PageSet secondSet = pageSets[1];
                if (secondSet.pageP1) secondSet.pageP1.SetupData(setting.page2);
                if (secondSet.pageP2) secondSet.pageP2.SetupData(setting.page2);
            }

            if (pageSets.Count > 2)
            {
                PageSet thirdSet = pageSets[2];
                if (thirdSet.pageP1) thirdSet.pageP1.SetupData(setting.page3);
                if (thirdSet.pageP2) thirdSet.pageP2.SetupData(setting.page3);
            }

            if (pageSets.Count > 3)
            {
                PageSet fourthSet = pageSets[3];
                if (fourthSet.pageP1) fourthSet.pageP1.SetupData(setting.page4);
                if (fourthSet.pageP2) fourthSet.pageP2.SetupData(setting.page4);
            }

            if (pageSets.Count > 4)
            {
                PageSet fifthSet = pageSets[4];
                if (fifthSet.pageP1) fifthSet.pageP1.SetupData(setting.page5);
                if (fifthSet.pageP2) fifthSet.pageP2.SetupData(setting.page5);
            }
            
            if (pageSets.Count > 5)
            {
                PageSet sixthSet = pageSets[5];
                if (sixthSet.pageP1) sixthSet.pageP1.SetupData(setting.page6);
                if (sixthSet.pageP2) sixthSet.pageP2.SetupData(setting.page6);
            }
        }

        /// <summary> 모든 튜토리얼 단계가 끝나면 실제 플레이 튜토리얼 씬으로 전환함. </summary>
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