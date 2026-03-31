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
    public class Page_Background : GamePage
    {
        [Header("Dynamic UI Components")]
        [SerializeField] private Text textQuestion;
        [SerializeField] private Text textName;
        [SerializeField] private Text textDate;

        private CommonBackgroundData _cachedData;

        public override void SetupData(object data)
        {
            CommonBackgroundData pageData = data as CommonBackgroundData;
            if (pageData != null) _cachedData = pageData;
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
                bool isServer = false;
                if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

                SetUIText(textName, isServer ? _cachedData.nameA : _cachedData.nameB);
                
                // JSON 파일 내부에 {nameA}, {nameB} 태그가 잘못 복사된 경우를 대비한 강제 보정
                string rawText = textName.text;
                if (isServer && rawText.Contains("{nameB}")) rawText = rawText.Replace("{nameB}", "{nameA}");
                if (!isServer && rawText.Contains("{nameA}")) rawText = rawText.Replace("{nameA}", "{nameB}");

                textName.text = UIUtils.ReplacePlayerNamePlaceholders(rawText);
            }
        }

        private void ApplyCurrentDate()
        {
            if (textDate) textDate.text = DateTime.Now.ToString("yyyy.MM.dd");
        }

        public void SetQuestionText(string questionString)
        {
            if (textQuestion) textQuestion.text = questionString;
        }
    }
}