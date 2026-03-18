using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Network; // TCP 매니저 접근
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{   
    [Serializable]
    public class TutorialPage1Data
    {
        public TextSetting descriptionText; 
    }
    
    /// <summary>
    /// 첫 번째 튜토리얼 페이지 컨트롤러.
    /// 서버(P1)에서 확인 입력을 받으면 클라이언트(P2)에 TCP 신호를 보내 동시에 다음 페이지로 넘어감.
    /// </summary>
    public class TutorialPage1Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup textCanvasGroup;
        [SerializeField] private Text descriptionText;

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private string _cachedMessage = string.Empty;
        private bool _isPageActive;
        private Coroutine _fadeCoroutine;

        private void Update()
        {   
            if (!_isPageActive) return;
            
            // Why: 씬 동기화와 동일하게 외부 API 신호를 전담하는 서버에서만 넘김 입력을 처리함
            if (TcpManager.Instance && TcpManager.Instance.IsServer)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    OnConfirmInput();
                }
            }
        }

        public override void SetupData(object data)
        {
            TutorialPage1Data pageData = data as TutorialPage1Data;
            
            if (pageData != null)
            {
                if (pageData.descriptionText != null)
                {
                    _cachedMessage = pageData.descriptionText.text;
                }
                else
                {
                    Debug.LogWarning("[TutorialPage1Controller] descriptionText 데이터가 null입니다.");
                }
            }
            else
            {   
                Debug.LogError("[TutorialPage1Controller] 전달된 데이터가 null이거나 형식이 잘못되었습니다.");
            }
        }

        public override void OnEnter()
        {
            base.OnEnter(); 

            _isPageActive = true;
            
            if (descriptionText)
            {
                descriptionText.text = !string.IsNullOrEmpty(_cachedMessage) 
                    ? _cachedMessage 
                    : "엔터 키를 눌러 진행하세요.";
            }

            if (textCanvasGroup)
            {
                textCanvasGroup.alpha = 0f;
                _fadeCoroutine = StartCoroutine(FadeTextRoutine(textCanvasGroup, 0f, 1f, fadeDuration));
            }

            // Why: 클라이언트가 서버의 페이지 넘김 신호를 받기 위해 진입 시 구독함
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
            }
        }

        private void OnConfirmInput()
        {
            // Why: 서버가 먼저 완료 신호를 보내 클라이언트도 함께 페이지를 넘기도록 유도함
            if (TcpManager.Instance && TcpManager.Instance.IsServer)
            {
                TcpManager.Instance.SendMessageToTarget("PAGE1_COMPLETE", "");
            }

            CompletePage();
        }

        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == "PAGE1_COMPLETE")
            {
                CompletePage();
            }
        }

        /// <summary> 완료 신호를 발생시켜 매니저의 TransitionToNext()를 트리거함. </summary>
        private void CompletePage()
        {
            if (!_isPageActive) return; 
            _isPageActive = false;
            
            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }

        public override void OnExit()
        {
            base.OnExit();
            
            if (SoundManager.Instance) // BGM 재시작
            {
                SoundManager.Instance.StopBGM();
                SoundManager.Instance?.PlayBGM("MainBGM");                
            }
            
            _isPageActive = false;
            
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            // Why: 이벤트 중복 구독을 막기 위해 페이지 종료 시 안전하게 해제함
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
            }
        }

        private IEnumerator FadeTextRoutine(CanvasGroup target, float start, float end, float duration)
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