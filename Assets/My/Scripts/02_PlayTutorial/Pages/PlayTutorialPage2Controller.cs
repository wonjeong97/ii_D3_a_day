using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Network; // TCP 매니저 네임스페이스 추가
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Utils;

namespace My.Scripts._02_PlayTutorial.Pages
{
    [Serializable]
    public class PlayTutorialPage2Data
    {
        // # TODO: 제이슨 구조 확정 시 텍스트 포맷 데이터 추가
    }

    /// <summary>
    /// 플레이 튜토리얼의 두 번째 페이지 컨트롤러.
    /// 지정된 키를 5초 동안 계속 누르고 있어야 완료되며, 도중에 끊기면 페널티(1초 대기 후 리셋)가 부여됨.
    /// </summary>
    public class PlayTutorialPage2Controller : GamePage
    {
        // Why: TCP 네트워크 상태에 따라 동적으로 판단하므로 isPlayer1 변수는 삭제함

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
            KeyCode.Alpha1, KeyCode.Keypad1, KeyCode.Alpha2, KeyCode.Keypad2, 
            KeyCode.Alpha3, KeyCode.Keypad3, KeyCode.Alpha4, KeyCode.Keypad4, KeyCode.Alpha5, KeyCode.Keypad5 
        };
        private readonly KeyCode[] _p2Keys = new KeyCode[] { 
            KeyCode.Alpha6, KeyCode.Keypad6, KeyCode.Alpha7, KeyCode.Keypad7, 
            KeyCode.Alpha8, KeyCode.Keypad8, KeyCode.Alpha9, KeyCode.Keypad9, KeyCode.Alpha0, KeyCode.Keypad0 
        };

        public override void SetupData(object data)
        {
            PlayTutorialPage2Data pageData = data as PlayTutorialPage2Data;
            
            if (pageData != null)
            {
                _cachedData = pageData;
            }
            else
            {
                Debug.LogWarning("[PlayTutorialPage2Controller] SetupData: 전달된 데이터가 null입니다.");
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();

            _isCompleted = false;
            _isWaitingForReset = false;
            _holdingKey = KeyCode.None;

            if (countdownUI) countdownUI.text = "5";
            if (mainGroupCanvas) mainGroupCanvas.alpha = 0f;

            _fadeCoroutine = StartCoroutine(FadeCanvasGroupRoutine(mainGroupCanvas, 0f, 1f, fadeDuration));
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

            // # TODO: RFID 장비 연동 시 이 Input 체크 부분을 'RFID 카드가 태그 중인가?'를 반환하는 통신 매니저 함수로 교체할 것
            KeyCode pressedKey = GetCurrentValidKey();

            if (pressedKey != KeyCode.None)
            {
                if (_holdingKey == KeyCode.None)
                {
                    _holdingKey = pressedKey;
                    _countdownCoroutine = StartCoroutine(CountdownRoutine());
                }
                else if (_holdingKey != pressedKey)
                {
                    InterruptCountdown();
                }
            }
            else
            {
                if (_holdingKey != KeyCode.None)
                {
                    InterruptCountdown();
                }
            }
        }

        private KeyCode GetCurrentValidKey()
        {
            // Why: TCP 매니저를 통해 현재 기기의 역할(방장/접속자)을 자동으로 판별하여 할당할 키를 결정함
            bool isServer = false;
            if (TcpManager.Instance)
            {
                isServer = TcpManager.Instance.IsServer;
            }

            KeyCode[] keysToCheck = isServer ? _p1Keys : _p2Keys;
            
            foreach (KeyCode key in keysToCheck)
            {
                if (Input.GetKey(key))
                {
                    return key;
                }
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

        private void InterruptCountdown()
        {
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }

            _holdingKey = KeyCode.None;
            StartCoroutine(ResetWaitRoutine());
        }

        private IEnumerator ResetWaitRoutine()
        {
            _isWaitingForReset = true;
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            if (countdownUI) countdownUI.text = "5"; 
            _isWaitingForReset = false;
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