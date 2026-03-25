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
                textName.text = UIUtils.ReplacePlayerNamePlaceholders(textName.text);
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