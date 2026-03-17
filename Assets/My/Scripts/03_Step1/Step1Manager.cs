using System;
using My.Scripts.Core;
using My.Scripts.Network;
using My.Scripts.Global;
using My.Scripts._03_Step1.Pages;
using UnityEngine;
using Wonjeong.Utils;

namespace My.Scripts._03_Step1
{
    // Why: Step1Page1Data와 Step1Page2Data는 개별 컨트롤러 스크립트(Pages)에 실제 구조체로 정의되었으므로 임시 선언을 삭제함
    // # TODO: 추후 3~6페이지 컨트롤러가 완성되면 각 페이지에 맞는 실제 데이터 구조체 선언부도 마저 삭제할 것
    [Serializable] public class Step1Page3Data { }
    [Serializable] public class Step1Page4Data { }
    [Serializable] public class Step1Page5Data { }
    [Serializable] public class Step1Page6Data { }

    [Serializable]
    public class Step1Setting
    {
        public Step1BackgroundData background;
        public Step1Page1Data page1;
        public Step1Page2Data page2;
        public Step1Page3Data page3;
        public Step1Page4Data page4;
        public Step1Page5Data page5;
        public Step1Page6Data page6;
    }

    /// <summary>
    /// Step1 씬의 전체 흐름을 제어하는 매니저.
    /// 배경 페이지를 독립적으로 띄우고, 1~6페이지의 순차 진행 후 TCP 동기화를 수행함.
    /// </summary>
    public class Step1Manager : BaseFlowManager
    {
        [Header("Background Setup")]
        [SerializeField] private Step1BackgroundController backgroundPage;

        private bool _isLocalFinished = false;
        private bool _isRemoteFinished = false;

        protected override void Start()
        {
            // Why: Step1 씬은 진입 즉시 1페이지가 페이드인 연출 없이 알파값 1로 노출되어야 함
            skipFirstPageFade = true;

            base.Start(); 
            
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
            }
        }

        protected override void LoadSettings()
        {
            Step1Setting setting = JsonLoader.Load<Step1Setting>(GameConstants.Path.Step1);

            if (setting == null)
            {
                Debug.LogError("[Step1Manager] JSON/Step1 로드 실패.");
                return;
            }

            // 1. 배경 페이지 독립 초기화
            // Why: 배경은 페이드 아웃되지 않고 계속 유지되어야 하므로 순차 리스트(pages)에 넣지 않고 단독으로 켜줌
            if (backgroundPage)
            {
                backgroundPage.SetupData(setting.background);
                backgroundPage.OnEnter();
            }
            else
            {
                Debug.LogWarning("[Step1Manager] backgroundPage가 인스펙터에 할당되지 않았습니다.");
            }

            // 2. 1~6페이지 순차 리스트 데이터 주입
            if (pages.Count > 0 && pages[0]) pages[0].SetupData(setting.page1);
            if (pages.Count > 1 && pages[1]) pages[1].SetupData(setting.page2);
            if (pages.Count > 2 && pages[2]) pages[2].SetupData(setting.page3);
            if (pages.Count > 3 && pages[3]) pages[3].SetupData(setting.page4);
            if (pages.Count > 4 && pages[4]) pages[4].SetupData(setting.page5);
            if (pages.Count > 5 && pages[5]) pages[5].SetupData(setting.page6);
        }

        protected override void OnAllFinished()
        {
            _isLocalFinished = true;
            Debug.Log("[Step1Manager] 내 PC Step1 완료. 상대방 대기 중...");

            if (TcpManager.Instance)
            {
                TcpManager.Instance.SendMessageToTarget("STEP1_COMPLETE");
            }

            CheckSyncAndChangeScene();
        }

        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == "STEP1_COMPLETE")
            {
                _isRemoteFinished = true;
                Debug.Log("[Step1Manager] 상대방 PC Step1 완료 신호 수신.");
                
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
                    Debug.Log("<color=cyan>[Step1Manager] 양방향 동기화 완료. Step2 씬으로 이동합니다.</color>");
                    // # TODO: Step2 씬 이름 상수를 GameConstants.Scene 에 추가하고 적용할 것
                    GameManager.Instance.ChangeScene("04_Step2"); 
                }
                else
                {
                    Debug.LogError("[Step1Manager] GameManager가 존재하지 않습니다.");
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