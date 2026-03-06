using System;
using System.Collections;
using My.Scripts.Core;
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
        [Header("Display Settings")]
        [SerializeField] private bool isPlayer1; 

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

        // P1과 P2가 각각 허용하는 키 목록
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
                    // Why: 입력을 처음 감지하면 5초 카운트다운 코루틴을 시작함
                    _holdingKey = pressedKey;
                    _countdownCoroutine = StartCoroutine(CountdownRoutine());
                }
                else if (_holdingKey != pressedKey)
                {
                    // Why: 누르던 도중 다른 카드로 교체(다른 키 입력)되면 진행을 무효화함
                    InterruptCountdown();
                }
            }
            else
            {
                if (_holdingKey != KeyCode.None)
                {
                    // Why: 홀드 중이던 키에서 손을 떼면 진행을 무효화함
                    InterruptCountdown();
                }
            }
        }

        /// <summary>
        /// 인스펙터 설정(P1, P2)에 따라 현재 눌려있는 유효한 키코드를 반환함.
        /// </summary>
        private KeyCode GetCurrentValidKey()
        {
            KeyCode[] keysToCheck = isPlayer1 ? _p1Keys : _p2Keys;
            
            foreach (KeyCode key in keysToCheck)
            {
                if (Input.GetKey(key))
                {
                    return key;
                }
            }
            return KeyCode.None;
        }

        /// <summary>
        /// 5초 동안 1초 주기로 카운트를 줄여나가는 루틴.
        /// </summary>
        private IEnumerator CountdownRoutine()
        {
            // Why: RFID 카드가 리더기에 5초간 안정적으로 태그되고 있는지 폴링(Polling) 간격인 1초마다 시각적으로 피드백함
            for (int i = 5; i >= 1; i--)
            {
                if (countdownUI) countdownUI.text = i.ToString();
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            // 5초 모두 유지 달성
            _isCompleted = true;
            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }

        /// <summary>
        /// 입력이 끊겼을 때 카운트다운을 즉시 멈추고 페널티 루틴을 시작함.
        /// </summary>
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

        /// <summary>
        /// 1초 동안 멈춘 뒤 다시 카운트를 5로 리셋하여 재입력을 기다림.
        /// </summary>
        private IEnumerator ResetWaitRoutine()
        {
            _isWaitingForReset = true;

            // Why: 카드를 떼었을 때 즉시 리셋되지 않고 1초간 멈춘 상태를 보여주어 사용자에게 페널티(실패) 상황을 명확히 인지시킴
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
                
                if (target) 
                {
                    target.alpha = Mathf.Lerp(start, end, elapsed / duration);
                }
                
                yield return null;
            }

            if (target) 
            {
                target.alpha = end;
            }
        }
    }
}