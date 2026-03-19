using System;
using My.Scripts.Core;
using My.Scripts._01_Tutorial.Pages;
using UnityEngine;
using My.Scripts.Global;
using Wonjeong.Utils;
using My.Scripts.Network; // TCP 통신 매니저 접근을 위한 네임스페이스 추가

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
    /// 단일 흐름으로 동작하며, TCP 통신을 통해 다른 PC와의 완료 타이밍을 동기화함.
    /// </summary>
    public class TutorialManager : BaseFlowManager
    {
        private bool _isLocalFinished = false;
        private bool _isRemoteFinished = false;

        protected override void Start()
        {
            base.Start(); // 상위 클래스(BaseFlowManager)의 초기화 실행
            
            // Why: 상대방 PC의 완료 신호를 수신하기 위해 네트워크 이벤트를 구독함
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
            }
        }

        protected override void LoadSettings()
        {
            TutorialSetting setting = JsonLoader.Load<TutorialSetting>(GameConstants.Path.Tutorial);

            // 일반 C# 객체이므로 명시적 null 검사 적용
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

        /// <summary>
        /// 내 PC의 튜토리얼 흐름이 모두 끝났을 때 호출됨.
        /// </summary>
        protected override void OnAllFinished()
        {
            _isLocalFinished = true;
            Debug.Log("[TutorialManager] 내 PC 튜토리얼 완료. 상대방을 기다립니다...");

            // 내 튜토리얼이 끝났음을 상대방 PC에 전송함
            // if (TcpManager.Instance)
            // {
            //     TcpManager.Instance.SendMessageToTarget("TUTORIAL_COMPLETE");
            // }
            
            CheckSyncAndChangeScene();
        }

        /// <summary>
        /// TCP를 통해 메시지가 수신될 때마다 호출되는 콜백.
        /// </summary>
        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            // C# 객체 및 문자열 명시적 null 검사
            if (msg != null && msg.command == "TUTORIAL_COMPLETE")
            {
                _isRemoteFinished = true;
                Debug.Log("[TutorialManager] 상대방 PC 튜토리얼 완료 신호 수신.");
                
                CheckSyncAndChangeScene();
            }
        }

        /// <summary>
        /// 양쪽 PC가 모두 완료 상태인지 확인하고 다음 씬으로 동시 진입함.
        /// </summary>
        private void CheckSyncAndChangeScene()
        {
            // Why: 양쪽 PC의 완료 플래그가 모두 true일 때만 씬을 전환하여 완벽한 타이밍 동기화를 이룸
            if (_isLocalFinished /*&& _isRemoteFinished*/)
            {
                // 중복 씬 전환을 막기 위해 이벤트 구독 즉시 해제
                if (TcpManager.Instance)
                {
                    TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
                }

                if (GameManager.Instance)
                {
                    Debug.Log("[TutorialManager] 양방향 동기화 완료. PlayTutorial로 이동합니다.");
                    GameManager.Instance.ChangeScene(GameConstants.Scene.PlayTutorial);
                }
                else
                {
                    Debug.LogError("[TutorialManager] GameManager가 존재하지 않습니다.");
                }
            }
        }

        private void OnDestroy()
        {
            // Why: 씬이 강제로 파괴되거나 전환될 때 메모리 누수 및 에러를 막기 위해 이벤트 구독을 안전하게 해제함
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
            }
        }
    }
}