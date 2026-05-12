using System;
using My.Scripts._07_Ending.Pages;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;
using Wonjeong.Utils;

namespace My.Scripts._07_Ending
{
    /// <summary>
    /// JSON에서 로드되는 엔딩 씬의 전체 데이터 구조체.
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
    /// 엔딩 시퀀스 완료 후 세션을 초기화하고 각자 타이틀 씬으로 복귀하는 프로세스를 관리함.
    /// </summary>
    public class EndingManager : BaseFlowManager
    {
        /// <summary>
        /// 매니저 초기화 시 첫 번째 페이지의 페이드 인 연출을 생략하도록 설정함.
        /// 씬 전환 직후 흐름이 끊기지 않고 자연스럽게 이어지도록 하기 위함.
        /// </summary>
        protected override void Start()
        {
            skipFirstPageFade = true;
            base.Start();
        }

        /// <summary>
        /// 외부 JSON 파일에서 엔딩 설정을 로드하여 각 페이지에 할당함.
        /// </summary>
        protected override void LoadSettings()
        {
            string lang = "ko";

            // 다국어 경로 구성을 위해 세션 매니저 인스턴스 유무를 확인함.
            if (SessionManager.Instance)
            {
                lang = SessionManager.Instance.CurrentLanguage;

                if (string.IsNullOrEmpty(lang))
                {
                    lang = "ko"; 
                }
            }
            else
            {
                Debug.LogWarning("[EndingManager] SessionManager 부재로 기본 언어(ko) 적용함.");
            }

            // # TODO: 향후 언어 팩 로드 실패 시 재시도 로직 추가 고려
            string path = $"{GameConstants.Path.ContentRoot}/{lang}/{GameConstants.Path.Ending}";
            EndingSetting setting = JsonLoader.Load<EndingSetting>(path);

            if (setting == null)
            {
                Debug.LogError($"[EndingManager] {path} 로드 실패.");
                return;
            }

            // 각 페이지 컴포넌트의 유효성을 검사한 뒤 데이터를 할당함.
            if (pages.Count > 0 && pages[0]) pages[0].SetupData(setting.page1);
            if (pages.Count > 1 && pages[1]) pages[1].SetupData(setting.page2);
            if (pages.Count > 2 && pages[2]) pages[2].SetupData(setting.page3);
        }

        /// <summary>
        /// 모든 엔딩 페이지 시퀀스가 종료되었을 때 호출됨.
        /// 세션 데이터를 초기화하고 방 리셋 API를 호출하여 기기를 초기 상태로 복구함.
        /// </summary>
        protected override void OnAllFinished()
        {
            Debug.Log("[EndingManager] 내 PC 엔딩 완료. 세션을 초기화하고 타이틀 씬으로 각자 복귀합니다.");

            if (GameManager.Instance)
            {
                // 세션 클리어 및 방 상태 리셋 후 타이틀 씬으로 전환함.
                GameManager.Instance.ReturnToTitle(true); 
            }
        }
    }
}