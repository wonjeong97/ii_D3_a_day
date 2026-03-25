using System.Collections;
using My.Scripts.Core.Data;
using My.Scripts.Network; 
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    public class Page_Loading : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup mainCg;
        [SerializeField] private Text text1UI;
        [SerializeField] private Text text2UI;

        [Header("Settings")]
        [SerializeField] private float waitTime = 10.0f;
        [SerializeField] private float fadeDuration = 0.5f;

        private CommonLoadingData _cachedData;
        private Coroutine _loadingCoroutine;
        private bool _isCompleted;
        
        private string _readyCmd = string.Empty;
        private string _completeCmd = string.Empty;
        private bool _isRemoteReady;

        private void Start()
        {
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
            }
        }

        public void SetSyncCommands(string readyCmd, string completeCmd)
        {
            _readyCmd = readyCmd;
            _completeCmd = completeCmd;
        }

        public override void SetupData(object data)
        {
            CommonLoadingData pageData = data as CommonLoadingData;
            if (pageData != null) _cachedData = pageData;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            ApplyDataToUI();

            if (mainCg) mainCg.alpha = 0f;

            if (_loadingCoroutine != null) StopCoroutine(_loadingCoroutine);
            _loadingCoroutine = StartCoroutine(LoadingRoutine());
        }

        public override void OnExit()
        {
            base.OnExit();
            _isRemoteReady = false;

            if (_loadingCoroutine != null)
            {
                StopCoroutine(_loadingCoroutine);
                _loadingCoroutine = null;
            }

            if (SoundManager.Instance) SoundManager.Instance.StopSFX();
        }

        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;
            if (text1UI) SetUIText(text1UI, _cachedData.text1);
            if (text2UI) SetUIText(text2UI, _cachedData.text2);
        }

        private IEnumerator LoadingRoutine()
        {
            if (mainCg)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    mainCg.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
                    yield return null;
                }
                mainCg.alpha = 1f;
            }

            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("레고_5");

            if (!string.IsNullOrEmpty(_readyCmd) && !string.IsNullOrEmpty(_completeCmd))
            {
                // Why: 늦게 도착한 PC가 대기 중인 PC의 락을 즉시 풀어주기 위해 무조건 1회 발송함
                if (TcpManager.Instance)
                {
                    TcpManager.Instance.SendMessageToTarget(_readyCmd, "");
                }

                // 내가 먼저 도착했을 경우 상대방이 올 때까지 1초마다 계속 쏴줌
                while (!_isRemoteReady)
                {
                    yield return CoroutineData.GetWaitForSeconds(1.0f);
                    if (TcpManager.Instance)
                    {
                        TcpManager.Instance.SendMessageToTarget(_readyCmd, "");
                    }
                }

                yield return CoroutineData.GetWaitForSeconds(3.0f);

                if (TcpManager.Instance && TcpManager.Instance.IsServer)
                {
                    TcpManager.Instance.SendMessageToTarget(_completeCmd, "");
                }
                CompletePage();
            }
            else
            {
                yield return CoroutineData.GetWaitForSeconds(waitTime);
                CompletePage();
            }
        }

        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg == null) return;

            if (!string.IsNullOrEmpty(_readyCmd) && msg.command == _readyCmd)
            {
                _isRemoteReady = true;
            }
            else if (!string.IsNullOrEmpty(_completeCmd) && msg.command == _completeCmd)
            {
                CompletePage();
            }
        }

        private void CompletePage()
        {
            if (_isCompleted) return;
            _isCompleted = true;

            if (onStepComplete != null) onStepComplete.Invoke(0);
        }
        
        private void OnDestroy()
        {
            if (TcpManager.Instance) TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
        }
    }
}