using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Network;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;

namespace My.Scripts._03_Step1.Pages
{
    [Serializable]
    public class Step1Page1Data
    {
        public TextSetting nameA; // 서버(P1)용 이름
        public TextSetting nameB; // 클라이언트(P2)용 이름
    }

    /// <summary>
    /// Step1의 첫 번째 본문 페이지 컨트롤러.
    /// 진입 후 3초가 지나면 자동으로 완료 처리되며, 디버그를 위해 엔터 키 스킵을 지원함.
    /// </summary>
    public class Step1Page1Controller : GamePage
    {
        [Header("Dynamic UI Components")]
        [SerializeField] private Text textName;
        [SerializeField] private Text textDate;

        [Header("Settings")]
        [SerializeField] private float autoTransitionDelay = 3.0f; // 자동 넘김 대기 시간

        private Step1Page1Data _cachedData;
        private bool _isCompleted = false;
        private Coroutine _autoTransitionCoroutine;

        public override void SetupData(object data)
        {
            Step1Page1Data pageData = data as Step1Page1Data;
            
            // 일반 C# 객체이므로 명시적 null 검사 수행
            if (pageData != null)
            {
                _cachedData = pageData;
            }
            else
            {
                Debug.LogWarning("[Step1Page1Controller] SetupData: 전달된 데이터가 null입니다.");
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            ApplyDataToUI();
            ApplyCurrentDate();

            // Why: 페이지 진입 시 3초 타이머를 시작하여 자동 전환을 유도함
            if (_autoTransitionCoroutine != null)
            {
                StopCoroutine(_autoTransitionCoroutine);
            }
            _autoTransitionCoroutine = StartCoroutine(AutoTransitionRoutine());
        }

        public override void OnExit()
        {
            base.OnExit();

            // Why: 페이지가 예상보다 일찍 꺼지거나 전환될 때 메모리 누수를 막기 위해 타이머를 안전하게 중단함
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
                string targetName = string.Empty;
                bool isServer = false;

                if (TcpManager.Instance)
                {
                    isServer = TcpManager.Instance.IsServer;
                }

                if (isServer)
                {
                    if (_cachedData.nameA != null) targetName = _cachedData.nameA.text;
                }
                else
                {
                    if (_cachedData.nameB != null) targetName = _cachedData.nameB.text;
                }

                textName.text = targetName;
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
            if (TcpManager.Instance)
            {
                isServer = TcpManager.Instance.IsServer;
            }

            bool canSkip = isServer;

#if UNITY_EDITOR
            // Why: 에디터에서는 3초를 기다리지 않고 엔터 키로 즉시 테스트를 넘길 수 있도록 허용함
            canSkip = true;
#endif

            if (canSkip)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    CompletePage();
                }
            }
        }

        /// <summary> 3초 대기 후 자동으로 다음 페이지 넘김을 트리거하는 코루틴. </summary>
        private IEnumerator AutoTransitionRoutine()
        {
            yield return new WaitForSeconds(autoTransitionDelay);
            
            if (!_isCompleted)
            {
                CompletePage();
            }
        }

        private void CompletePage()
        {
            if (_isCompleted) return;
            _isCompleted = true;

            // Why: 서버일 경우에만 클라이언트로 동기화 신호를 전송함
            if (TcpManager.Instance && TcpManager.Instance.IsServer)
            {
                TcpManager.Instance.SendMessageToTarget("STEP1_PAGE1_COMPLETE", "");
            }

            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }

        private void OnEnable()
        {
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
            }
        }

        private void OnDisable()
        {
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
            }
        }

        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == "STEP1_PAGE1_COMPLETE" && !_isCompleted)
            {
                _isCompleted = true;
                
                if (onStepComplete != null)
                {
                    onStepComplete.Invoke(0);
                }
            }
        }
    }
}