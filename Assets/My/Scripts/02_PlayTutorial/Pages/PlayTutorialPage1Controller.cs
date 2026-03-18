using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Network; 
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

    public class PlayTutorialPage1Controller : GamePage
    {
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

        // 수정됨: Page2로 넘겨주기 위해 방금 누른 키를 저장하는 프로퍼티
        public KeyCode PressedKey { get; private set; } = KeyCode.None;

        public override void SetupData(object data)
        {
            PlayTutorialPage1Data pageData = data as PlayTutorialPage1Data;
            
            if (pageData != null) _cachedData = pageData;
            else Debug.LogError("[PlayTutorialPage1Controller] SetupData: 전달된 데이터가 null입니다.");
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            PressedKey = KeyCode.None;

            if (text1Canvas) text1Canvas.alpha = 0f;
            if (imageGroupCanvas) imageGroupCanvas.alpha = 0f;
            if (text2Canvas) text2Canvas.alpha = 0f;

            if (_cachedData != null)
            {
                // 이전 래퍼 메서드 적용하여 위치/서식까지 일괄 적용
                if (text1UI) SetUIText(text1UI, _cachedData.text1);
                if (text2UI) SetUIText(text2UI, _cachedData.text2);
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

            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

            KeyCode pressed = GetValidKey(isServer);

            if (pressed != KeyCode.None)
            {
                PressedKey = pressed; // 눌린 키를 저장
                OnValidInputReceived();
            }
        }

        // 수정됨: 넘패드 입력을 제거하고 눌린 키(KeyCode)를 직접 반환함
        private KeyCode GetValidKey(bool isServer)
        {
            KeyCode[] keys = isServer 
                ? new KeyCode[] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 }
                : new KeyCode[] { KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0 };
            
            foreach (var key in keys)
            {
                if (Input.GetKeyDown(key)) return key;
            }
            return KeyCode.None;
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