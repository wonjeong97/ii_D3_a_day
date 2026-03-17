using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Network; // TCP 매니저 네임스페이스 추가
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.Utils;

namespace My.Scripts._02_PlayTutorial.Pages
{
    [Serializable]
    public class PlayTutorialPage1Data
    {
        public TextSetting text1;
        public TextSetting text2;
    }

    /// <summary>
    /// 플레이 튜토리얼의 첫 번째 페이지 컨트롤러.
    /// 기기(Server, Client)별로 지정된 독립적인 키 입력을 감지하여 다음 단계로 전환함.
    /// </summary>
    public class PlayTutorialPage1Controller : GamePage
    {
        // Why: TCP 네트워크 상태에 따라 동적으로 판단하므로 isPlayer1 변수는 삭제함

        [Header("UI Components")]
        [SerializeField] private CanvasGroup text1Canvas;
        [SerializeField] private Text text1UI;
        
        [SerializeField] private CanvasGroup imageGroupCanvas;
        
        [SerializeField] private CanvasGroup text2Canvas;
        [SerializeField] private Text text2UI;

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float waitBetweenFades = 0.5f;

        private PlayTutorialPage1Data _cachedData;
        private Coroutine _animationCoroutine;
        private bool _isCompleted = false;

        public override void SetupData(object data)
        {
            PlayTutorialPage1Data pageData = data as PlayTutorialPage1Data;
            
            if (pageData != null)
            {
                _cachedData = pageData;
            }
            else
            {
                Debug.LogError("[PlayTutorialPage1Controller] SetupData: 전달된 데이터가 null입니다.");
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            if (text1Canvas) text1Canvas.alpha = 0f;
            if (imageGroupCanvas) imageGroupCanvas.alpha = 0f;
            if (text2Canvas) text2Canvas.alpha = 0f;

            if (_cachedData != null)
            {
                if (text1UI)
                {
                    if (_cachedData.text1 != null) text1UI.text = _cachedData.text1.text;
                    else Debug.LogWarning("[PlayTutorialPage1Controller] text1 데이터가 null입니다.");
                }

                if (text2UI)
                {
                    if (_cachedData.text2 != null)
                    {
                        if (text2UI.supportRichText == false) text2UI.supportRichText = true;
                        text2UI.text = _cachedData.text2.text;
                    }
                    else Debug.LogWarning("[PlayTutorialPage1Controller] text2 데이터가 null입니다.");
                }
            }
            else
            {
                Debug.LogError("[PlayTutorialPage1Controller] _cachedData가 없어 텍스트를 세팅할 수 없습니다.");
            }

            _animationCoroutine = StartCoroutine(SequenceFadeRoutine());
        }

        public override void OnExit()
        {
            base.OnExit();
            
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }
        }

        private void Update()
        {
            if (_isCompleted) return;

            // Why: TCP 매니저를 통해 현재 기기의 역할(방장/접속자)을 자동으로 판별함
            bool isServer = false;
            if (TcpManager.Instance)
            {
                isServer = TcpManager.Instance.IsServer;
            }

            if (isServer)
            {
                if (CheckP1Input()) OnValidInputReceived();
            }
            else
            {
                if (CheckP2Input()) OnValidInputReceived();
            }
        }

        private bool CheckP1Input()
        {
            return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1) ||
                   Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2) ||
                   Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3) ||
                   Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4) ||
                   Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5);
        }

        private bool CheckP2Input()
        {
            return Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6) ||
                   Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7) ||
                   Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8) ||
                   Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9) ||
                   Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0);
        }

        private void OnValidInputReceived()
        {
            _isCompleted = true; 

            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }

        private IEnumerator SequenceFadeRoutine()
        {
            if (text1Canvas) yield return StartCoroutine(FadeCanvasGroupRoutine(text1Canvas, 0f, 1f, fadeDuration));
            yield return CoroutineData.GetWaitForSeconds(waitBetweenFades);

            if (imageGroupCanvas) yield return StartCoroutine(FadeCanvasGroupRoutine(imageGroupCanvas, 0f, 1f, fadeDuration));
            yield return CoroutineData.GetWaitForSeconds(waitBetweenFades);

            if (text2Canvas) yield return StartCoroutine(FadeCanvasGroupRoutine(text2Canvas, 0f, 1f, fadeDuration));
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