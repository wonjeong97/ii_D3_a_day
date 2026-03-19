using System;
using My.Scripts.Core;
using My.Scripts._02_PlayTutorial.Pages;
using UnityEngine;
using My.Scripts.Global;
using Wonjeong.Utils;
using My.Scripts.Network; 

namespace My.Scripts._02_PlayTutorial
{
    [Serializable]
    public class PlayTutorialSetting
    {
        public PlayTutorialPage1Data page1;
        public PlayTutorialPage2Data page2;
        public PlayTutorialPage3Data page3;
    }

    public class PlayTutorialManager : BaseFlowManager
    {
        private bool _isLocalFinished = false;
        private bool _isRemoteFinished = false;

        protected override void Start()
        {
            base.Start();
        }

        protected override void LoadSettings()
        {
            PlayTutorialSetting setting = JsonLoader.Load<PlayTutorialSetting>(GameConstants.Path.PlayTutorial);

            if (setting == null)
            {
                Debug.LogError("[PlayTutorialManager] JSON/PlayTutorial 로드 실패.");
                return;
            }

            if (pages.Count > 0 && pages[0]) pages[0].SetupData(setting.page1);
            if (pages.Count > 1 && pages[1]) pages[1].SetupData(setting.page2);
            if (pages.Count > 2 && pages[2]) pages[2].SetupData(setting.page3);
        }

        /// <summary>
        /// 페이지 전환 시 Page1의 입력값(RFID 카테고리)을 Page2로 넘겨줍니다.
        /// Why: 2페이지가 시작되자마자 카운트다운을 진행하려면 1페이지에서 인식된 그룹 정보를 알아야 함.
        /// </summary>
        public override void TransitionToPage(int index)
        {
            if (pages != null && index >= 0 && index < pages.Count)
            {
                if (index == 1) 
                {
                    PlayTutorialPage1Controller page1 = pages[0] as PlayTutorialPage1Controller;
                    PlayTutorialPage2Controller page2 = pages[1] as PlayTutorialPage2Controller;

                    if (page1 && page2)
                    {
                        // 1페이지에서 인식된 RFID 카테고리(1~5)를 2페이지의 초기값으로 세팅함
                        page2.SetInitialCategory(page1.SelectedCategory);
                    }
                }
            }
            base.TransitionToPage(index);
        }

        protected override void OnAllFinished()
        {
            _isLocalFinished = true;
            Debug.Log("[PlayTutorialManager] 내 PC 플레이 튜토리얼 완료. 상대방 대기 중...");

            CheckSyncAndChangeScene();
        }

        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == "PLAY_TUTORIAL_COMPLETE")
            {
                _isRemoteFinished = true;
                CheckSyncAndChangeScene();
            }
        }

        private void CheckSyncAndChangeScene()
        {
            if (_isLocalFinished)
            {
                if (GameManager.Instance)
                {
                    Debug.Log("[PlayTutorialManager] 양방향 동기화 완료. Step1 씬으로 이동합니다.");
                    GameManager.Instance.ChangeScene(GameConstants.Scene.Step1, true); 
                }
                else
                {
                    Debug.LogError("[PlayTutorialManager] GameManager가 존재하지 않습니다.");
                }
            }
        }

        private void OnDestroy()
        {
        }
    }
}