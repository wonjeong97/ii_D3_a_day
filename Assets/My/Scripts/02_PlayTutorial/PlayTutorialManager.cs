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

    /// <summary>
    /// 플레이 튜토리얼 씬의 페이지 전환 흐름을 제어하는 매니저.
    /// Why: 모든 튜토리얼이 끝나면 서버/클라이언트 양방향 완료 신호를 동기화한 뒤 Step1 씬으로 함께 이동함.
    /// </summary>
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
                UnityEngine.Debug.LogError("[PlayTutorialManager] JSON/PlayTutorial 로드 실패.");
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
                        // Why: RFID 연동 리팩토링으로 인해 KeyCode 대신 1~5 정수형 인덱스를 전달함
                        page2.SetInitialAnswer(page1.PressedAnswerIndex);
                    }
                }
            }
            base.TransitionToPage(index);
        }

        protected override void OnAllFinished()
        {
            _isLocalFinished = true;
            UnityEngine.Debug.Log("[PlayTutorialManager] 내 PC 플레이 튜토리얼 완료. 상대방 대기 중...");

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
            // Why: 내가 먼저 도착했을 경우 상대방이 올 때까지 1초마다 계속 동기화 신호를 발송함
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
                UnityEngine.Debug.Log("[PlayTutorialManager] 상대방 PC 플레이 튜토리얼 완료 신호 수신.");
                
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

                UnityEngine.Debug.Log("[PlayTutorialManager] 양방향 동기화 완료. 즉시 Step1 씬으로 이동합니다.");
                
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