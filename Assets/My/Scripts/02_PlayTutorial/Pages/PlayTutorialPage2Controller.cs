using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Network; 
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Utils;

namespace My.Scripts._02_PlayTutorial.Pages
{
    [Serializable]
    public class PlayTutorialPage2Data
    {
        // # TODO: 제이슨 구조 확정 시 데이터 추가
    }

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
        private KeyCode _holdingKey = KeyCode.None;

        private readonly KeyCode[] _p1Keys = new KeyCode[] { 
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 
        };
        private readonly KeyCode[] _p2Keys = new KeyCode[] { 
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0 
        };

        // 수정됨: Page1에서 누른 키를 초기값으로 셋팅
        public void SetInitialKey(KeyCode key)
        {
            _holdingKey = key;
        }

        public override void SetupData(object data)
        {
            PlayTutorialPage2Data pageData = data as PlayTutorialPage2Data;
            
            if (pageData != null) _cachedData = pageData;
            else Debug.LogWarning("[PlayTutorialPage2Controller] SetupData: 전달된 데이터가 null입니다.");
        }

        public override void OnEnter()
        {
            base.OnEnter();

            _isCompleted = false;
            _isWaitingForReset = false;

            if (countdownUI) countdownUI.text = "5";
            if (mainGroupCanvas) mainGroupCanvas.alpha = 0f;

            _fadeCoroutine = StartCoroutine(FadeCanvasGroupRoutine(mainGroupCanvas, 0f, 1f, fadeDuration));

            // 수정됨: Page1에서 전달받은 키가 있다면 화면에 들어오자마자 자동으로 카운트다운 시작
            if (_holdingKey != KeyCode.None)
            {
                _countdownCoroutine = StartCoroutine(CountdownRoutine());
            }
        }

        public override void OnExit()
        {
            base.OnExit();
            
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
        }

        private void Update()
        {
            if (_isCompleted || _isWaitingForReset) return;

            KeyCode newlyPressedKey = GetCurrentValidKeyDown();

            // 새로 눌린 유효한 키가 있을 때만 판단 (손을 떼는 행위는 무시하므로 카운트는 계속 진행됨)
            if (newlyPressedKey != KeyCode.None)
            {
                if (_holdingKey == KeyCode.None)
                {
                    _holdingKey = newlyPressedKey;
                    _countdownCoroutine = StartCoroutine(CountdownRoutine());
                }
                else if (_holdingKey != newlyPressedKey)
                {
                    // 카운트 진행 중 '기존과 다른 키'를 누르면 페널티 부여
                    InterruptCountdown(newlyPressedKey); 
                }
            }
        }

        private KeyCode GetCurrentValidKeyDown()
        {
            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

            KeyCode[] keysToCheck = isServer ? _p1Keys : _p2Keys;
            
            foreach (KeyCode key in keysToCheck)
            {
                if (Input.GetKeyDown(key)) return key;
            }
            return KeyCode.None;
        }

        private IEnumerator CountdownRoutine()
        {
            for (int i = 5; i >= 1; i--)
            {
                if (countdownUI) countdownUI.text = i.ToString();
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            _isCompleted = true;
            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }

        private void InterruptCountdown(KeyCode newKey)
        {
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }

            // 새로운 키로 갱신 후 페널티 시작
            _holdingKey = newKey; 
            StartCoroutine(ResetWaitRoutine());
        }

        private IEnumerator ResetWaitRoutine()
        {
            _isWaitingForReset = true;
            
            // 페널티 1초 대기
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            if (countdownUI) countdownUI.text = "5"; 
            _isWaitingForReset = false;

            // 수정됨: 페널티가 끝나면 방금 갱신된 새로운 키를 기준으로 카운트다운 자동 재시작
            if (_holdingKey != KeyCode.None && !_isCompleted)
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