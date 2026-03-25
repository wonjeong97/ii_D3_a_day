using System;
using System.Collections;
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

            // Why: 늦게 도착한 PC가 대기 중인 PC의 락을 즉시 풀어주기 위해 무조건 1회 발송함
            if (TcpManager.Instance)
            {
                TcpManager.Instance.SendMessageToTarget("PLAY_TUTORIAL_COMPLETE", "");
            }

            StartCoroutine(SendCompleteSignalRoutine());
            CheckSyncAndChangeScene();
        }

        private IEnumerator SendCompleteSignalRoutine()
        {
            // 내가 먼저 도착했을 경우 상대방이 올 때까지 1초마다 계속 쏴줌
            while (_isLocalFinished && !_isRemoteFinished)
            {
                yield return CoroutineData.GetWaitForSeconds(1.0f);
                if (TcpManager.Instance)
                {
                    TcpManager.Instance.SendMessageToTarget("PLAY_TUTORIAL_COMPLETE", "");
                }
            }
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

                Debug.Log("[PlayTutorialManager] 양방향 동기화 완료. 즉시 Step1 씬으로 이동합니다.");
                
                if (GameManager.Instance)
                {
                    GameManager.Instance.ChangeScene(GameConstants.Scene.Step1, true); 
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