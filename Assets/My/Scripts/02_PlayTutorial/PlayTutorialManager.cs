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
            
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
            }
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
        /// 페이지 전환 시 Page1의 입력값을 Page2로 넘겨줍니다.
        /// </summary>
        public override void TransitionToPage(int index)
        {
            if (pages != null && index >= 0 && index < pages.Count)
            {
                if (index == 1) // Page 2로 전환될 때
                {
                    PlayTutorialPage1Controller page1 = pages[0] as PlayTutorialPage1Controller;
                    PlayTutorialPage2Controller page2 = pages[1] as PlayTutorialPage2Controller;

                    if (page1 && page2)
                    {
                        // Page1에서 누른 키를 Page2에 전달
                        page2.SetInitialKey(page1.PressedKey);
                    }
                }
            }
            base.TransitionToPage(index);
        }

        protected override void OnAllFinished()
        {
            _isLocalFinished = true;
            Debug.Log("[PlayTutorialManager] 내 PC 플레이 튜토리얼 완료. 상대방 대기 중...");

            if (TcpManager.Instance)
            {
                TcpManager.Instance.SendMessageToTarget("PLAY_TUTORIAL_COMPLETE");
            }

            CheckSyncAndChangeScene();
        }

        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == "PLAY_TUTORIAL_COMPLETE")
            {
                _isRemoteFinished = true;
                Debug.Log("[PlayTutorialManager] 상대방 PC 플레이 튜토리얼 완료 신호 수신.");
                
                CheckSyncAndChangeScene();
            }
        }

        private void CheckSyncAndChangeScene()
        {
            if (_isLocalFinished && _isRemoteFinished)
            {
                if (TcpManager.Instance)
                {
                    TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
                }

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
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
            }
        }
    }
}