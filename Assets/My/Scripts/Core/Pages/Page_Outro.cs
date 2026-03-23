using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Core.Data;
using My.Scripts.Network;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    public class Page_Outro : GamePage
    {
        [Header("UI Components")]
        [Tooltip("텍스트와 이미지를 동시에 페이드하기 위한 부모 캔버스 그룹")]
        [SerializeField] private CanvasGroup mainCg;
        [SerializeField] private Text textOutroUI;
        [SerializeField] private Text textOutro2UI;

        [Header("Settings")]
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float autoTransitionDelay = 3.0f;

        private CommonOutroData _cachedData;
        private string _syncCommand = "DEFAULT_OUTRO_COMPLETE";
        private bool _isCompleted = false;
        private Coroutine _sequenceCoroutine;

        public void SetSyncCommand(string command)
        {
            _syncCommand = command;
        }

        public override void SetupData(object data)
        {
            CommonOutroData pageData = data as CommonOutroData;
            
            if (pageData != null) _cachedData = pageData;
            else Debug.LogWarning("[Page_Outro] SetupData: 전달된 데이터가 null이거나 형식이 잘못되었습니다.");
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            ApplyDataToUI();

            if (mainCg) mainCg.alpha = 0f;

            if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = StartCoroutine(SequenceRoutine());
        }

        public override void OnExit()
        {
            base.OnExit();

            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }
        }

        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;

            SetUIText(textOutroUI, _cachedData.textOutro);
            SetUIText(textOutro2UI, _cachedData.textOutro2);
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

        private IEnumerator SequenceRoutine()
        {
            if (mainCg) yield return StartCoroutine(FadeCanvasGroupRoutine(mainCg, 0f, 1f, fadeDuration));
            yield return CoroutineData.GetWaitForSeconds(autoTransitionDelay);
            if (!_isCompleted) CompletePage();
        }

        private IEnumerator FadeCanvasGroupRoutine(CanvasGroup target, float start, float end, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (target) target.alpha = Mathf.Lerp(start, end, elapsed / duration);
                yield return null;
            }
            if (target) target.alpha = end;
        }

        private void CompletePage()
        {
            if (_isCompleted) return;
            _isCompleted = true;

            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }

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