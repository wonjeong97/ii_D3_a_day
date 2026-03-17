using System;
using My.Scripts.Core;
using My.Scripts._02_PlayTutorial.Pages;
using UnityEngine;
using My.Scripts.Global;
using Wonjeong.Utils;
using My.Scripts.Network; // TCP 통신 매니저 접근을 위한 네임스페이스

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
    /// 플레이 튜토리얼 씬의 매니저.
    /// 단일 페이지 흐름으로 동작하며, 마지막 3페이지 완료 시 TCP로 상대방과 동기화 후 Step1 씬으로 넘어감.
    /// </summary>
    public class PlayTutorialManager : BaseFlowManager
    {
        private bool _isLocalFinished = false;
        private bool _isRemoteFinished = false;

        protected override void Start()
        {
            base.Start();
            
            // Why: 상대방 PC의 튜토리얼 완료 신호를 받기 위해 이벤트 구독
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
            }
        }

        protected override void LoadSettings()
        {
            PlayTutorialSetting setting = JsonLoader.Load<PlayTutorialSetting>(GameConstants.Path.PlayTutorial);

            // 일반 C# 객체이므로 명시적 null 검사 수행
            if (setting == null)
            {
                Debug.LogError("[PlayTutorialManager] JSON/PlayTutorial 로드 실패.");
                return;
            }

            // 기기에 할당된 단일 페이지들에만 데이터 주입
            if (pages.Count > 0 && pages[0]) pages[0].SetupData(setting.page1);
            if (pages.Count > 1 && pages[1]) pages[1].SetupData(setting.page2);
            if (pages.Count > 2 && pages[2]) pages[2].SetupData(setting.page3);
        }

        /// <summary>
        /// 3페이지 연출까지 모두 끝났을 때 자동 호출됨.
        /// </summary>
        protected override void OnAllFinished()
        {
            _isLocalFinished = true;
            Debug.Log("[PlayTutorialManager] 내 PC 플레이 튜토리얼 완료. 상대방 대기 중...");

            // Why: 내 쪽 진행이 끝났음을 상대방에게 알려서 동기화를 유도함
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
            // Why: 양쪽 모두 3페이지 연출이 완전히 끝났을 때만 다음 씬으로 동시 진입함
            if (_isLocalFinished && _isRemoteFinished)
            {
                if (TcpManager.Instance)
                {
                    TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
                }

                if (GameManager.Instance)
                {
                    Debug.Log("[PlayTutorialManager] 양방향 동기화 완료. Step1 씬으로 이동합니다.");
                    
                    // Why: 업데이트된 전역 상수를 사용하여 Step1 씬으로 전환함
                    GameManager.Instance.ChangeScene(GameConstants.Scene.Step1); 
                }
                else
                {
                    Debug.LogError("[PlayTutorialManager] GameManager가 존재하지 않습니다.");
                }
            }
        }

        private void OnDestroy()
        {
            // 이벤트 구독 안전 해제
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
            }
        }
    }
}