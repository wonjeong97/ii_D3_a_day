using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Network; 
using My.Scripts.Hardware; 
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._02_PlayTutorial.Pages
{
    /// <summary>
    /// 조작 튜토리얼 2페이지 데이터 모델.
    /// </summary>
    [Serializable]
    public class PlayTutorialPage2Data
    {
    }

    /// <summary>
    /// 조작 튜토리얼의 두 번째 페이지 컨트롤러.
    /// Why: 카드를 5초 동안 떼지 않고 유지하는 조작을 학습하기 위함.
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
        
        private bool _isCompleted;
        private bool _isWaitingForReset;
        private int _holdingAnswerIndex = -1;

        private readonly KeyCode[] _p1Keys = new KeyCode[] { 
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 
        };
        private readonly KeyCode[] _p2Keys = new KeyCode[] { 
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0 
        };

        /// <summary>
        /// 이전 페이지(Page1)에서 확정된 응답 인덱스를 초기값으로 설정함.
        /// </summary>
        /// <param name="answerIndex">1~5 사이의 응답 인덱스.</param>
        public void SetInitialAnswer(int answerIndex)
        {
            _holdingAnswerIndex = answerIndex;
        }

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

        /// <summary>
        /// 페이지 진입 시 RFID 폴링을 즉시 시작하고 연출을 수행함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            _isCompleted = false;
            _isWaitingForReset = false;

            if (RfidManager.Instance) 
            {
                RfidManager.Instance.onAnswerReceived += OnRfidAnswerReceived;
                RfidManager.Instance.StartPolling();
            }

            if (countdownUI) countdownUI.text = "5";
            if (mainGroupCanvas) mainGroupCanvas.alpha = 0f;

            _fadeCoroutine = StartCoroutine(FadeCanvasGroupRoutine(mainGroupCanvas, 0f, 1f, fadeDuration));

            // Why: Page1에서 누른 카드를 그대로 유지하고 진입했다면 바로 카운트다운 진행
            if (_holdingAnswerIndex != -1)
            {
                _countdownCoroutine = StartCoroutine(CountdownRoutine());
            }
        }

        /// <summary>
        /// 페이지 이탈 시 코루틴 및 RFID 폴링을 중단함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            
            if (RfidManager.Instance) 
            {
                RfidManager.Instance.StopPolling();
                RfidManager.Instance.onAnswerReceived -= OnRfidAnswerReceived;
            }

            if (!ReferenceEquals(_fadeCoroutine, null)) StopCoroutine(_fadeCoroutine);
            if (!ReferenceEquals(_countdownCoroutine, null)) StopCoroutine(_countdownCoroutine);
        }

        /// <summary>
        /// 매 프레임 키보드 디버그 입력을 검사함.
        /// Why: Update 루프 내부이므로 object.ReferenceEquals를 사용하여 극단적 최적화를 적용함.
        /// </summary>
        private void Update()
        {
            if (_isCompleted || _isWaitingForReset) return;

            int newlyPressedIndex = GetCurrentValidAnswerIndex();

            if (newlyPressedIndex != -1)
            {
                if (_holdingAnswerIndex == -1)
                {
                    _holdingAnswerIndex = newlyPressedIndex;
                    _countdownCoroutine = StartCoroutine(CountdownRoutine());
                }
                else if (_holdingAnswerIndex != newlyPressedIndex)
                {
                    // Why: 카운트 진행 중 다른 키/카드를 누르면 페널티를 부과하기 위함.
                    InterruptCountdown(newlyPressedIndex); 
                }
            }
        }

        /// <summary>
        /// RFID 응답 수신 이벤트 핸들러.
        /// Why: 폴링 중 카드가 인식되면 현재 유지 중인 카드와 비교하여 로직을 분기함.
        /// </summary>
        /// <param name="index">인식된 응답 인덱스 (1~5).</param>
        private void OnRfidAnswerReceived(int index)
        {
            if (_isCompleted || _isWaitingForReset) return;

            if (_holdingAnswerIndex == -1)
            {
                _holdingAnswerIndex = index;
                _countdownCoroutine = StartCoroutine(CountdownRoutine());
            }
            else if (_holdingAnswerIndex != index)
            {
                InterruptCountdown(index); 
            }
        }

        /// <summary>
        /// 키보드 디버그 입력을 감지하고 1~5번 인덱스로 변환함.
        /// </summary>
        /// <returns>인식된 인덱스 번호 (없을 시 -1).</returns>
        private int GetCurrentValidAnswerIndex()
        {
            bool isServer = false;
            
            if (!ReferenceEquals(TcpManager.Instance, null)) 
            {
                isServer = TcpManager.Instance.IsServer;
            }

            KeyCode[] keysToCheck = isServer ? _p1Keys : _p2Keys;
            
            for (int i = 0; i < keysToCheck.Length; i++)
            {
                if (Input.GetKeyDown(keysToCheck[i])) 
                {
                    return i + 1; 
                }
            }
            
            return -1;
        }

        /// <summary>
        /// 5초 카운트다운을 수행함.
        /// </summary>
        private IEnumerator CountdownRoutine()
        {
            // Why: 코루틴이 시작될 때마다 최초 및 1초 페널티 이후 리셋 시 효과음을 재생함.
            if (SoundManager.Instance)
            {   
                SoundManager.Instance.StopSFX();
                SoundManager.Instance.PlaySFX("공통_10_5초");
            }

            for (int i = 5; i >= 1; i--)
            {
                if (countdownUI) countdownUI.text = i.ToString();
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            _isCompleted = true;
            if (RfidManager.Instance) RfidManager.Instance.StopPolling();

            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }

        /// <summary>
        /// 카운트다운을 강제 중단하고 페널티를 시작함.
        /// </summary>
        /// <param name="newAnswerIndex">새로 인식된 응답 인덱스.</param>
        private void InterruptCountdown(int newAnswerIndex)
        {
            if (!ReferenceEquals(_countdownCoroutine, null))
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }

            _holdingAnswerIndex = newAnswerIndex; 
            StartCoroutine(ResetWaitRoutine());
        }

        /// <summary>
        /// 1초간의 페널티 대기 시간을 수행함.
        /// </summary>
        private IEnumerator ResetWaitRoutine()
        {
            _isWaitingForReset = true;
            
            // Why: 다른 카드를 누른 행위에 대한 1초 패널티 타임.
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            if (countdownUI) countdownUI.text = "5"; 
            _isWaitingForReset = false;

            // Why: 페널티가 끝나면 방금 갱신된 새로운 카드를 기준으로 카운트다운 자동 재시작.
            if (_holdingAnswerIndex != -1 && !_isCompleted)
            {
                _countdownCoroutine = StartCoroutine(CountdownRoutine());
            }
        }

        /// <summary>
        /// 캔버스 그룹 알파값 선형 보간 애니메이션.
        /// </summary>
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