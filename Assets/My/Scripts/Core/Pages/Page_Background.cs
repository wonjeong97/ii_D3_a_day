using System;
using My.Scripts.Core;
using My.Scripts.Core.Data;
using My.Scripts.Network;
using My.Scripts.Global;
using My.Scripts.UI;
using UnityEngine;
using UnityEngine.UI;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 모든 페이지의 하단 또는 상단에 고정되어 플레이어 정보와 날짜를 표시하는 배경 페이지 컨트롤러.
    /// 공통 UI 요소의 일관성을 유지하고 실시간 데이터를 렌더링하기 위함.
    /// </summary>
    public class Page_Background : GamePage
    {
        [Header("Dynamic UI Components")]
        [SerializeField] private Text textQuestion;
        [SerializeField] private Text textName;
        [SerializeField] private Text textDate;

        private CommonBackgroundData _cachedData;

        /// <summary>
        /// 외부에서 전달된 공통 배경 데이터를 메모리에 캐싱함.
        /// </summary>
        /// <param name="data">CommonBackgroundData 타입의 데이터 객체.</param>
        public override void SetupData(object data)
        {
            CommonBackgroundData pageData = data as CommonBackgroundData;
            if (pageData != null) _cachedData = pageData;
        }

        /// <summary>
        /// 페이지 활성화 시 유저 이름과 현재 시스템 날짜를 화면에 갱신함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            ApplyDataToUI();
            ApplyCurrentDate();
        }

        /// <summary>
        /// 네트워크 역할에 맞는 이름을 선택하고 플레이스홀더를 실제 사용자 이름으로 치환함.
        /// 서버 PC와 클라이언트 PC가 각자 자신의 정보를 올바르게 표시하도록 하기 위함.
        /// </summary>
        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;

            if (textName)
            {
                bool isServer = false;
                if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

                SetUIText(textName, isServer ? _cachedData.nameA : _cachedData.nameB);
                textName.text = UIUtils.ReplacePlayerNamePlaceholders(textName.text);
            }
        }

        /// <summary>
        /// 현재 시스템의 날짜 정보를 가져와 정해진 포맷으로 텍스트를 갱신함.
        /// </summary>
        private void ApplyCurrentDate()
        {
            if (textDate) textDate.text = DateTime.Now.ToString("yyyy.MM.dd");
        }

        /// <summary>
        /// 현재 진행 중인 문항의 진행도나 질문 텍스트를 외부에서 주입함.
        /// </summary>
        /// <param name="questionString">표시할 질문 또는 진행도 문자열.</param>
        public void SetQuestionText(string questionString)
        {
            if (textQuestion) textQuestion.text = questionString;
        }
    }
}