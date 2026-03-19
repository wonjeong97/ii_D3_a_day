using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Global; 
using My.Scripts.Hardware; 
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._02_PlayTutorial.Pages
{
    [Serializable]
    public class PlayTutorialPage2Data { }

    /// <summary>
    /// 카운트다운(페널티) 대기 페이지 컨트롤러.
    /// Why: RFID 스캔 루프를 유지하여 변경을 감지하되, 카운트가 1이 되면 입력을 차단하여 안정적으로 완료되게 함.
    /// </summary>
    public class PlayTutorialPage2Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup mainGroupCanvas;
        [SerializeField] private Text countdownUI;

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private PlayTutorialPage2Data _cachedData;
        private Coroutine _fadeCoroutine;
        private Coroutine _countdownCoroutine;
        
        private bool _isCompleted = false;
        private bool _isWaitingForReset = false;
        private bool _canAcceptInput = false;

        private int _holdingCategory = 0;

        public void SetInitialCategory(int category)
        {
            _holdingCategory = category;
        }

        public override void SetupData(object data)
        {
            PlayTutorialPage2Data pageData = data as PlayTutorialPage2Data;
            if (pageData != null) _cachedData = pageData;
        }

        public override void OnEnter()
        {
            base.OnEnter();

            _isCompleted = false;
            _isWaitingForReset = false;
            _canAcceptInput = true;

            if (countdownUI) countdownUI.text = "5";
            if (mainGroupCanvas) mainGroupCanvas.alpha = 0f;

            _fadeCoroutine = StartCoroutine(FadeCanvasGroupRoutine(mainGroupCanvas, 0f, 1f, fadeDuration));

            if (RfidManager.Instance)
            {
                RfidManager.Instance.onCardRead += OnCardRecognized;
            }

            if (_holdingCategory != 0)
            {
                _countdownCoroutine = StartCoroutine(CountdownRoutine());
            }

            StartAutoReadLoop().Forget();
        }

        public override void OnExit()
        {
            base.OnExit();
            
            _canAcceptInput = false;

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);

            if (RfidManager.Instance)
            {
                RfidManager.Instance.onCardRead -= OnCardRecognized;
            }
        }

        private async UniTaskVoid StartAutoReadLoop()
        {
            // Why: 루프는 유지하되, _canAcceptInput이 true일 때만 하드웨어에 Read 신호를 보냄
            while (!_isCompleted && !this.GetCancellationTokenOnDestroy().IsCancellationRequested)
            {
                if (_canAcceptInput)
                {
                    if (RfidManager.Instance) RfidManager.Instance.TryReadCard().Forget();
                }
                await UniTask.Delay(TimeSpan.FromSeconds(1.0f), delayTiming: PlayerLoopTiming.Update);
            }
        }

        private void OnCardRecognized(string uid, int category)
        {
            // 입력 차단 상태(_canAcceptInput == false)일 경우 수신된 신호 무시
            if (_isCompleted || _isWaitingForReset || !_canAcceptInput || category == 0) return;
            
            ProcessInput(category);
        }

        private void Update()
        {
            if (_isCompleted || _isWaitingForReset || !_canAcceptInput) return;

            KeyCode pressed = GetCurrentValidKeyDown();
            if (pressed != KeyCode.None)
            {
                int category = pressed - KeyCode.Alpha0;
                UnityEngine.Debug.Log($"[PlayTutorialPage2] Debug: Injected Category {category}.");
                ProcessInput(category);
            }
        }

        private void ProcessInput(int category)
        {
            if (_holdingCategory == 0)
            {
                _holdingCategory = category;
                _countdownCoroutine = StartCoroutine(CountdownRoutine());
            }
            else if (_holdingCategory != category)
            {
                InterruptCountdown(category);
            }
        }

        private KeyCode GetCurrentValidKeyDown()
        {
            KeyCode[] keysToCheck = new KeyCode[] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };
            foreach (KeyCode key in keysToCheck)
            {
                if (Input.GetKeyDown(key)) return key;
            }
            return KeyCode.None;
        }

        private IEnumerator CountdownRoutine()
        {
            if (SoundManager.Instance)
            {   
                SoundManager.Instance.StopSFX();
                SoundManager.Instance.PlaySFX("공통_10_5초");
            }

            for (int i = 5; i >= 1; i--)
            {
                if (countdownUI) countdownUI.text = i.ToString();
                
                // Why: 카운트다운이 1이 되는 순간부터 RFID 스캔 및 입력을 완전히 차단함
                if (i <= 1)
                {
                    _canAcceptInput = false;
                }

                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            _isCompleted = true;
            if (onStepComplete != null) onStepComplete.Invoke(0);
        }

        private void InterruptCountdown(int newCategory)
        {
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }

            _holdingCategory = newCategory; 
            StartCoroutine(ResetWaitRoutine());
        }

        private IEnumerator ResetWaitRoutine()
        {
            _isWaitingForReset = true;
            yield return CoroutineData.GetWaitForSeconds(1.0f); 

            if (countdownUI) countdownUI.text = "5"; 
            _isWaitingForReset = false;

            if (_holdingCategory != 0 && !_isCompleted)
            {
                _countdownCoroutine = StartCoroutine(CountdownRoutine());
            }
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
    }
}