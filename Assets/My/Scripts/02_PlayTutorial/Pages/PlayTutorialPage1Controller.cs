using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using My.Scripts.Core;
using My.Scripts.Network;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
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

        [Header("Answers Images")]
        [SerializeField] private Image imgAnswer1;
        [SerializeField] private Image imgAnswer2;
        [SerializeField] private Image imgAnswer3;
        [SerializeField] private Image imgAnswer4;
        [SerializeField] private Image imgAnswer5;

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float waitBetweenFades = 0.5f;

        private PlayTutorialPage1Data _cachedData;
        private Coroutine _animationCoroutine;
        private bool _isCompleted;
        private bool _canAcceptInput;
        
        private readonly List<AsyncOperationHandle<Sprite>> _loadedImageHandles = new List<AsyncOperationHandle<Sprite>>();
        private CancellationTokenSource _cts;

        public KeyCode PressedKey { get; private set; } = KeyCode.None;

        public override void SetupData(object data)
        {
            PlayTutorialPage1Data pageData = data as PlayTutorialPage1Data;
            if (pageData != null) _cachedData = pageData;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _canAcceptInput = false; 
            PressedKey = KeyCode.None;

            if (text1Canvas) text1Canvas.alpha = 0f;
            if (imageGroupCanvas) imageGroupCanvas.alpha = 0f;
            if (text2Canvas) text2Canvas.alpha = 0f;

            string cart = SessionManager.Instance && !string.IsNullOrEmpty(SessionManager.Instance.Cartridge) ? SessionManager.Instance.Cartridge : "A";

            if (_cachedData != null)
            {
                if (text1UI) SetUIText(text1UI, _cachedData.text1);
                if (text2UI) SetUIText(text2UI, _cachedData.text2);

                if (text1UI)
                {
                    text1UI.text = text1UI.text.Replace("{Cartridge}", $"{cart} 카트리지");
                }
            }

            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }
            _cts = new CancellationTokenSource();

            LoadAndSetCartridgeImagesAsync(cart, _cts.Token).Forget();

            _animationCoroutine = StartCoroutine(SequenceFadeRoutine());
        }

        public override void OnExit()
        {
            base.OnExit();
            
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            ReleaseLoadedImages();

            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }
        }

        private async UniTaskVoid LoadAndSetCartridgeImagesAsync(string cart, CancellationToken token)
        {
            string legoCartKey = $"Lego_cart_{cart.ToLower()}";
            string[] keys = new string[] { legoCartKey, legoCartKey, legoCartKey, legoCartKey, legoCartKey };

            ReleaseLoadedImages();

            UniTask<Sprite>[] loadTasks = new UniTask<Sprite>[5];

            for (int i = 0; i < 5; i++)
            {
                AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(keys[i]);
                _loadedImageHandles.Add(handle); 
                loadTasks[i] = handle.Task.AsUniTask();
            }

            try
            {
                Sprite[] results = await UniTask.WhenAll(loadTasks);
                if (token.IsCancellationRequested) return;

                if (imgAnswer1 && results[0]) imgAnswer1.sprite = results[0];
                if (imgAnswer2 && results[1]) imgAnswer2.sprite = results[1];
                if (imgAnswer3 && results[2]) imgAnswer3.sprite = results[2];
                if (imgAnswer4 && results[3]) imgAnswer4.sprite = results[3];
                if (imgAnswer5 && results[4]) imgAnswer5.sprite = results[4];
            }
            catch (Exception e)
            {
                if (!token.IsCancellationRequested)
                {
                    Debug.LogError($"[PlayTutorialPage1Controller] 어드레서블 로드 실패: {e.Message}");
                    ReleaseLoadedImages();
                }
            }
        }

        private void ReleaseLoadedImages()
        {
            if (_loadedImageHandles == null || _loadedImageHandles.Count == 0) return;

            foreach (AsyncOperationHandle<Sprite> handle in _loadedImageHandles)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
            _loadedImageHandles.Clear();
        }

        private void Update()
        {
            if (_isCompleted || !_canAcceptInput) return;

            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

            KeyCode pressed = GetValidKey(isServer);

            if (pressed != KeyCode.None)
            {
                PressedKey = pressed; 
                OnValidInputReceived();
            }
        }

        private KeyCode GetValidKey(bool isServer)
        {
            KeyCode[] keys = isServer 
                ? new KeyCode[] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 }
                : new KeyCode[] { KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0 };
            
            foreach (KeyCode key in keys)
            {
                if (Input.GetKeyDown(key)) return key;
            }
            return KeyCode.None;
        }

        private void OnValidInputReceived()
        {
            _isCompleted = true; 
            if (onStepComplete != null) onStepComplete.Invoke(0);
        }

        private IEnumerator SequenceFadeRoutine()
        {
            if (text1Canvas) yield return StartCoroutine(FadeCanvasGroupRoutine(text1Canvas, 0f, 1f, fadeDuration));
            yield return CoroutineData.GetWaitForSeconds(waitBetweenFades);

            if (imageGroupCanvas) yield return StartCoroutine(FadeCanvasGroupRoutine(imageGroupCanvas, 0f, 1f, fadeDuration));
            yield return CoroutineData.GetWaitForSeconds(waitBetweenFades);

            if (text2Canvas) yield return StartCoroutine(FadeCanvasGroupRoutine(text2Canvas, 0f, 1f, fadeDuration));

            yield return CoroutineData.GetWaitForSeconds(0.5f);
            _canAcceptInput = true;
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