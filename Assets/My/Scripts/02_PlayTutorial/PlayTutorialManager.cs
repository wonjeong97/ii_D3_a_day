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
    /// <summary>
    /// 조작 튜토리얼 씬의 페이지별 JSON 데이터를 매핑하기 위한 데이터 구조체.
    /// </summary>
    [Serializable]
    public class PlayTutorialSetting
    {
        public PlayTutorialPage1Data page1;
        public PlayTutorialPage2Data page2;
        public PlayTutorialPage3Data page3;
    }

    /// <summary>
    /// 플레이 튜토리얼 씬의 페이지 전환 흐름을 제어하는 매니저.
    /// 모든 튜토리얼이 끝나면 서버와 클라이언트 양방향 완료 신호를 동기화한 뒤 Step1 씬으로 함께 이동함.
    /// </summary>
    public class PlayTutorialManager : BaseFlowManager
    {
        private bool _isLocalFinished;
        private bool _isRemoteFinished;
        
        [Header("Sub Canvas UI")]
        [Tooltip("모니터 2(서브 캔버스)를 페이드 아웃 시킬 검은색 패널의 CanvasGroup")]
        [SerializeField] private CanvasGroup subCanvasFadeCg;

        /// <summary>
        /// 매니저 초기화 및 네트워크 메시지 수신 이벤트를 구독함.
        /// </summary>
        protected override void Start()
        {
            base.Start();
            
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
            }
        }

        /// <summary>
        /// 외부 JSON 파일에서 조작 튜토리얼 데이터를 로드하여 각 페이지에 할당함.
        /// </summary>
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
        /// 지정된 인덱스의 페이지로 전환함.
        /// 두 번째 페이지 진입 시 첫 번째 페이지에서 확정된 응답(RFID) 인덱스 정보를 전달하여 연속적인 조작을 검증하기 위함.
        /// </summary>
        /// <param name="index">전환할 페이지의 인덱스 번호.</param>
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
                        page2.SetInitialAnswer(page1.PressedAnswerIndex);
                    }
                }
            }
            base.TransitionToPage(index);
        }

        /// <summary>
        /// 로컬 PC의 모든 페이지 흐름이 완료되었을 때 호출됨.
        /// 상대방 PC의 진행이 끝날 때까지 대기하며 동기화 신호를 발송하기 위함.
        /// </summary>
        protected override void OnAllFinished()
        {
            _isLocalFinished = true;
            Debug.Log("[PlayTutorialManager] 내 PC 플레이 튜토리얼 완료. 상대방 대기 중...");

            if (TcpManager.Instance)
            {
                TcpManager.Instance.SendMessageToTarget("PLAY_TUTORIAL_COMPLETE", "");
            }

            StartCoroutine(SendCompleteSignalRoutine());
            CheckSyncAndChangeScene();
        }

        /// <summary>
        /// 상대방 PC가 완료될 때까지 1초 간격으로 동기화 완료 신호를 지속 발송함.
        /// 늦게 도달한 PC가 대기 중인 PC의 잠금을 즉시 해제하도록 유도하기 위함.
        /// </summary>
        private IEnumerator SendCompleteSignalRoutine()
        {
            while (_isLocalFinished && !_isRemoteFinished)
            {
                yield return CoroutineData.GetWaitForSeconds(1.0f);
                if (TcpManager.Instance)
                {
                    TcpManager.Instance.SendMessageToTarget("PLAY_TUTORIAL_COMPLETE", "");
                }
            }
        }

        /// <summary>
        /// 상대방의 튜토리얼 완료 신호를 수신하고 동기화 상태를 갱신함.
        /// </summary>
        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == "PLAY_TUTORIAL_COMPLETE")
            {
                _isRemoteFinished = true;
                Debug.Log("[PlayTutorialManager] 상대방 PC 플레이 튜토리얼 완료 신호 수신.");
                
                CheckSyncAndChangeScene();
            }
        }

        /// <summary>
        /// 양쪽 PC 모두 튜토리얼을 완료했는지 확인하고 다음 씬으로 전환함.
        /// 메인 디스플레이가 페이드 아웃될 때 서브 캔버스도 동시에 페이드 아웃되도록 처리함.
        /// </summary>
        private void CheckSyncAndChangeScene()
        {
            if (_isLocalFinished && _isRemoteFinished)
            {
                if (TcpManager.Instance)
                {
                    TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
                }

                Debug.Log("[PlayTutorialManager] 양방향 동기화 완료. 즉시 Step1 씬으로 이동합니다.");
                
                if (subCanvasFadeCg)
                {
                    StartCoroutine(SubCanvasFadeOutRoutine());
                }

                if (GameManager.Instance)
                {
                    GameManager.Instance.ChangeScene(GameConstants.Scene.Step1, true); 
                }
            }
        }

        /// <summary>
        /// 서브 캔버스의 검은색 패널 알파값을 올려 메인 카메라 페이드아웃과 타이밍을 맞춤.
        /// GameManager의 기본 페이드아웃 시간(약 1초)과 동기화됨.
        /// </summary>
        private IEnumerator SubCanvasFadeOutRoutine()
        {
            float elapsed = 0f;
            float duration = 1.0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (subCanvasFadeCg) subCanvasFadeCg.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
                yield return null;
            }
            
            if (subCanvasFadeCg) subCanvasFadeCg.alpha = 1f;
        }

        /// <summary>
        /// 씬 종료 시 네트워크 메시지 수신 이벤트를 해제함.
        /// </summary>
        private void OnDestroy()
        {
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
            }
        }
    }
}