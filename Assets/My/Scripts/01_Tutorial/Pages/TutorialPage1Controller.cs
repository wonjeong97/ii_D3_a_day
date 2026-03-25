using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Network;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{   
    /// <summary>
    /// JSON에서 로드되는 튜토리얼 1페이지 데이터 구조체.
    /// </summary>
    [Serializable]
    public class TutorialPage1Data
    {
        public TextSetting descriptionText; 
    }
    
    /// <summary>
    /// 첫 번째 튜토리얼 페이지 컨트롤러.
    /// 서버에서 확인 입력을 받으면 클라이언트에 TCP 신호를 보내 동시에 다음 페이지로 넘어갑니다.
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
            
            // Why: 씬 동기화와 동일하게 권한을 서버가 전담하여 넘김 입력을 처리함
            if (TcpManager.Instance)
            {
                if (TcpManager.Instance.IsServer)
                {
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    {
                        OnConfirmInput();
                    }
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
                if (!string.IsNullOrEmpty(_cachedMessage))
                {
                    descriptionText.text = _cachedMessage;
                }
                else
                {
                    Debug.LogWarning("[TutorialPage1Controller] _cachedMessage가 비어있습니다. 텍스트를 갱신하지 않습니다.");
                }
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
            // Why: 서버가 완료 신호를 보내 클라이언트도 함께 페이지를 넘기도록 유도함
            if (TcpManager.Instance)
            {
                if (TcpManager.Instance.IsServer)
                {
                    TcpManager.Instance.SendMessageToTarget("PAGE1_COMPLETE", "");
                }
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

        /// <summary>
        /// 완료 신호를 발생시켜 매니저의 TransitionToNext를 트리거합니다.
        /// </summary>
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
            
            if (SoundManager.Instance)
            {
                SoundManager.Instance.StopBGM();
                SoundManager.Instance.PlayBGM("MainBGM");                
            }
            
            _isPageActive = false;
            
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

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