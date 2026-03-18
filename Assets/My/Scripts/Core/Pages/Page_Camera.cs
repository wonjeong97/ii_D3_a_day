using System.Collections;
using My.Scripts.Data;
using My.Scripts.Network;
using UnityEngine;
using UnityEngine.UI;

namespace My.Scripts.Core.Pages
{
    public class Page_Camera : GamePage
    {
        [Header("Canvas Groups")]
        [SerializeField] private CanvasGroup textAnswerCompleteCg;
        [SerializeField] private CanvasGroup textMySceneCg;
        [SerializeField] private CanvasGroup imageCg;

        [Header("Dynamic UI Components")]
        [SerializeField] private Text textAnswerCompleteUI;
        [SerializeField] private Text textMySceneUI;

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private CommonResultPageData _cachedData; 
        private string _syncCommand = "DEFAULT_RESULT_COMPLETE";
        private bool _isCompleted = false;
        private Coroutine _sequenceCoroutine;

        public void SetSyncCommand(string command)
        {
            _syncCommand = command;
        }

        public override void SetupData(object data)
        {
            CommonResultPageData pageData = data as CommonResultPageData;
            
            if (pageData != null) _cachedData = pageData;
            else Debug.LogWarning("[Page_Camera] SetupData: 전달된 데이터가 null이거나 형식이 잘못되었습니다.");
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            ApplyDataToUI();

            if (textAnswerCompleteCg) textAnswerCompleteCg.alpha = 0f;
            if (textMySceneCg) textMySceneCg.alpha = 0f;
            if (imageCg) imageCg.alpha = 0f;

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

            SetUIText(textAnswerCompleteUI, _cachedData.textAnswerComplete);
            SetUIText(textMySceneUI, _cachedData.textMyScene);
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
            if (textAnswerCompleteCg) yield return StartCoroutine(FadeCanvasGroupRoutine(textAnswerCompleteCg, 0f, 1f, fadeDuration));
            if (imageCg) yield return StartCoroutine(FadeCanvasGroupRoutine(imageCg, 0f, 1f, fadeDuration));
            yield return new WaitForSeconds(3.0f);
            
            if (textMySceneCg) yield return StartCoroutine(FadeCanvasGroupRoutine(textMySceneCg, 0f, 1f, fadeDuration));
            yield return new WaitForSeconds(1.0f);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                
                if (textAnswerCompleteCg) textAnswerCompleteCg.alpha = Mathf.Lerp(1f, 0f, t);
                if (imageCg) imageCg.alpha = Mathf.Lerp(1f, 0f, t);
                if (textMySceneCg) textMySceneCg.alpha = Mathf.Lerp(1f, 0f, t);
                
                yield return null;
            }

            if (textAnswerCompleteCg) textAnswerCompleteCg.alpha = 0f;
            if (imageCg) imageCg.alpha = 0f;
            if (textMySceneCg) textMySceneCg.alpha = 0f;

            // 사진 기록 완료 시 텍스트 서식 일괄 교체
            SetUIText(textAnswerCompleteUI, _cachedData.textPhotoSaved);

            if (textAnswerCompleteCg) yield return StartCoroutine(FadeCanvasGroupRoutine(textAnswerCompleteCg, 0f, 1f, fadeDuration));

            yield return new WaitForSeconds(1.0f);

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

            if (TcpManager.Instance && TcpManager.Instance.IsServer)
            {
                TcpManager.Instance.SendMessageToTarget(_syncCommand, "");
            }

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