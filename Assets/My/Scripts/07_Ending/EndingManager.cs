using System;
using My.Scripts._07_Ending.Pages;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Network;
using UnityEngine;
using Wonjeong.Utils;

namespace My.Scripts._07_Ending
{
    /// <summary>
    /// Ending.json의 구조에 맞춘 데이터 클래스.
    /// </summary>
    [Serializable]
    public class EndingSetting
    {
        public EndingPage1Data page1;
        public EndingPage2Data page2;
        public EndingPage3Data page3;
    }

    /// <summary>
    /// 엔딩 씬의 전체 흐름을 제어하는 매니저.
    /// 흐름: Page1 -> Page2 -> Page3 -> (동기화) -> 타이틀 씬 복귀
    /// </summary>
    public class EndingManager : BaseFlowManager
    {
        private bool _isLocalFinished = false;
        private bool _isRemoteFinished = false;

        protected override void Start()
        {
            skipFirstPageFade = true;
            base.Start();
            
            // 네트워크 메시지 수신 이벤트 등록
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
            }
        }

        protected override void LoadSettings()
        {
            EndingSetting setting = JsonLoader.Load<EndingSetting>(GameConstants.Path.Ending);

            if (setting == null)
            {
                Debug.LogError("[EndingManager] JSON/Ending 로드 실패. 데이터를 확인할 수 없습니다.");
                return;
            }

            // 인스펙터에 연결된 페이지들에 각각 알맞은 JSON 데이터를 주입합니다.
            if (pages.Count > 0 && pages[0]) pages[0].SetupData(setting.page1);
            if (pages.Count > 1 && pages[1]) pages[1].SetupData(setting.page2);
            if (pages.Count > 2 && pages[2]) pages[2].SetupData(setting.page3);
        }

        /// <summary>
        /// 엔딩의 마지막 페이지(Page3)까지 모두 끝났을 때 호출됨.
        /// </summary>
        protected override void OnAllFinished()
        {
            _isLocalFinished = true;
            Debug.Log("[EndingManager] 내 PC 엔딩 완료. 상대방 대기 중...");

            // 상대방에게 내 엔딩이 끝났음을 알림
            if (TcpManager.Instance)
            {
                TcpManager.Instance.SendMessageToTarget("ENDING_COMPLETE");
            }

            CheckSyncAndChangeScene();
        }

        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == "ENDING_COMPLETE")
            {
                _isRemoteFinished = true;
                Debug.Log("[EndingManager] 상대방 PC 엔딩 완료 신호 수신.");
                
                CheckSyncAndChangeScene();
            }
        }

        /// <summary>
        /// 양쪽 PC가 모두 엔딩을 다 봤는지 확인하고 타이틀 화면으로 넘김.
        /// </summary>
        private void CheckSyncAndChangeScene()
        {
            if (_isLocalFinished && _isRemoteFinished)
            {
                // 씬 전환 전 이벤트 해제 (메모리 누수 방지)
                if (TcpManager.Instance)
                {
                    TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
                }

                if (GameManager.Instance)
                {
                    Debug.Log("[EndingManager] 양방향 엔딩 동기화 완료. 타이틀 씬으로 복귀합니다.");
                    // GameManager에 ReturnToTitle() 등의 함수가 없다면 ChangeScene("00_Title")로 대체하세요.
                    GameManager.Instance.ChangeScene(GameConstants.Scene.Title); 
                }
                else
                {
                    Debug.LogError("[EndingManager] GameManager가 존재하지 않습니다.");
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