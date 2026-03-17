using System;
using My.Scripts.Core;
using My.Scripts.Network; 
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;

namespace My.Scripts._03_Step1.Pages
{
    [Serializable]
    public class Step1BackgroundData
    {
        public TextSetting nameA; // 서버(P1)용 이름
        public TextSetting nameB; // 클라이언트(P2)용 이름
    }

    /// <summary>
    /// Step1의 공통 배경(Background)을 관리하는 페이지 컨트롤러.
    /// 외부 페이지에서 진행도 텍스트를 직접 변경할 수 있도록 수동 변경 메서드를 제공함.
    /// </summary>
    public class Step1BackgroundController : GamePage
    {
        [Header("Dynamic UI Components")]
        [SerializeField] private Text textQuestion;
        [SerializeField] private Text textName;
        [SerializeField] private Text textDate;

        private Step1BackgroundData _cachedData;

        public override void SetupData(object data)
        {
            Step1BackgroundData pageData = data as Step1BackgroundData;
            
            // 일반 C# 객체이므로 명시적 null 검사 수행
            if (pageData != null)
            {
                _cachedData = pageData;
            }
            else
            {
                Debug.LogWarning("[Step1BackgroundController] SetupData: 전달된 데이터가 null입니다.");
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();

            ApplyDataToUI();
            ApplyCurrentDate();
        }

        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;

            if (textName)
            {
                string targetName = string.Empty;
                bool isServer = false;

                // Why: 현재 PC가 서버(P1)인지 클라이언트(P2)인지 판별함
                if (TcpManager.Instance)
                {
                    isServer = TcpManager.Instance.IsServer;
                }

                // Why: 판별된 통신 역할에 따라 A 또는 B의 이름을 UI에 할당함
                if (isServer)
                {
                    if (_cachedData.nameA != null) targetName = _cachedData.nameA.text;
                }
                else
                {
                    if (_cachedData.nameB != null) targetName = _cachedData.nameB.text;
                }

                textName.text = targetName;
            }
        }

        private void ApplyCurrentDate()
        {
            if (textDate)
            {
                // Why: PC의 현재 시스템 날짜를 yyyy.MM.dd 포맷으로 변환하여 출력함
                textDate.text = DateTime.Now.ToString("yyyy.MM.dd");
            }
        }

        /// <summary> 
        /// 외부 본문 페이지(1~6)에서 자신의 진행도에 맞춰 배경의 텍스트를 갱신할 때 호출함. 
        /// </summary>
        public void SetQuestionText(string questionString)
        {
            // Why: 각 페이지가 로드될 때 자신의 상태(예: "1/2")를 배경 UI에 직접 주입할 수 있도록 함
            if (textQuestion)
            {
                textQuestion.text = questionString;
            }
        }
    }
}