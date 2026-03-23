using System;
using System.Collections;
using My.Scripts.Core.Data;
using My.Scripts.Global; 
using My.Scripts.Network;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    public class Page_Intro : GamePage
    {
        [Header("Dynamic UI Components")]
        [SerializeField] private Text textName;
        [SerializeField] private Text textDate;

        [Header("Settings")]
        [SerializeField] private float autoTransitionDelay = 3.0f;

        private CommonIntroData _cachedData;
        private bool _isCompleted = false;
        private Coroutine _autoTransitionCoroutine;
        private string _syncCommand = "DEFAULT_INTRO_COMPLETE";

        public void SetSyncCommand(string command)
        {
            _syncCommand = command;
        }

        public override void SetupData(object data)
        {
            CommonIntroData pageData = data as CommonIntroData;
            
            if (pageData != null) _cachedData = pageData;
            else Debug.LogWarning("[Page_Intro] SetupData: 전달된 데이터가 null입니다.");
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            ApplyDataToUI();
            ApplyCurrentDate();

            if (_autoTransitionCoroutine != null) StopCoroutine(_autoTransitionCoroutine);
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_13");
            _autoTransitionCoroutine = StartCoroutine(AutoTransitionRoutine());
        }

        public override void OnExit()
        {
            base.OnExit();

            if (_autoTransitionCoroutine != null)
            {
                StopCoroutine(_autoTransitionCoroutine);
                _autoTransitionCoroutine = null;
            }
        }

        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;

            if (textName)
            {
                bool isServer = false;
                if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

                // 서버(PC 1)면 nameA를, 클라이언트(PC 2)면 nameB의 위치와 서식을 적용
                SetUIText(textName, isServer ? _cachedData.nameA : _cachedData.nameB);

                // Why: SetUIText로 폰트, 색상 등 서식이 덮어씌워진 직후 실제 유저 이름으로 치환함
                if (SessionManager.Instance)
                {
                    string nameA = !string.IsNullOrEmpty(SessionManager.Instance.PlayerAFirstName) 
                        ? SessionManager.Instance.PlayerAFirstName 
                        : "사용자A";
                    string nameB = !string.IsNullOrEmpty(SessionManager.Instance.PlayerBFirstName) 
                        ? SessionManager.Instance.PlayerBFirstName 
                        : "사용자B";

                    textName.text = textName.text.Replace("{nameA}", nameA).Replace("{nameB}", nameB);
                }
            }
        }

        private void ApplyCurrentDate()
        {
            if (textDate)
            {
                textDate.text = DateTime.Now.ToString("yyyy.MM.dd");
            }
        }

        private void Update()
        {
            if (_isCompleted) return;

            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

            bool canSkip = isServer;

#if UNITY_EDITOR
            canSkip = true;
#endif

            if (canSkip)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) CompletePage();
            }
        }

        private IEnumerator AutoTransitionRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(autoTransitionDelay);
            if (!_isCompleted) CompletePage();
        }

        private void CompletePage()
        {
            if (_isCompleted) return;
            _isCompleted = true;

            // if (TcpManager.Instance && TcpManager.Instance.IsServer)
            // {
            //     TcpManager.Instance.SendMessageToTarget(_syncCommand, "");
            // }

            if (onStepComplete != null) onStepComplete.Invoke(0);
        }

        private void OnEnable()
        {
            if (TcpManager.Instance) TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
        }

        private void OnDisable()
        {
            if (TcpManager.Instance) TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
        }

        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == _syncCommand && !_isCompleted)
            {
                _isCompleted = true;
                if (onStepComplete != null) onStepComplete.Invoke(0);
            }
        }
    }
}