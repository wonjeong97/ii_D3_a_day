using System;
using System.Collections;
using System.Collections.Generic;
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

    /// <summary>
    /// 플레이 튜토리얼의 첫 번째 페이지 컨트롤러.
    /// Why: 세션에 저장된 카트리지에 따라 안내 텍스트를 치환하고 어드레서블에서 동적 이미지를 불러옴.
    /// </summary>
    public class PlayTutorialPage1Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup text1Canvas;
        [SerializeField] private Text text1UI;
        
        [SerializeField] private CanvasGroup imageGroupCanvas;
        
        [SerializeField] private CanvasGroup text2Canvas;
        [SerializeField] private Text text2UI;

        [Header("Answers Images")]
        [Tooltip("어드레서블을 통해 동적으로 교체될 5개의 보기 이미지를 연결하세요.")]
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

        public KeyCode PressedKey { get; private set; } = KeyCode.None;

        /// <summary>
        /// JSON 매니저로부터 전달받은 페이지 데이터를 캐싱함.
        /// </summary>
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

        /// <summary>
        /// 페이지 진입 시 초기값을 세팅하고 텍스트 치환 및 이미지 로드를 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _canAcceptInput = false; 
            PressedKey = KeyCode.None;

            if (text1Canvas) text1Canvas.alpha = 0f;
            if (imageGroupCanvas) imageGroupCanvas.alpha = 0f;
            if (text2Canvas) text2Canvas.alpha = 0f;

            if (_cachedData != null)
            {
                if (text1UI) SetUIText(text1UI, _cachedData.text1);
                if (text2UI) SetUIText(text2UI, _cachedData.text2);

                // Why: 서식이 일괄 적용된 후 문자열 내부의 플레이스홀더를 현재 세션 정보로 치환함.
                if (text1UI && SessionManager.Instance)
                {
                    string cart = string.IsNullOrEmpty(SessionManager.Instance.Cartridge) ? "A" : SessionManager.Instance.Cartridge;
                    text1UI.text = text1UI.text.Replace("{Cartridge}", $"{cart} 카트리지");
                }
            }
            else
            {
                Debug.LogError("[PlayTutorialPage1Controller] _cachedData가 없어 텍스트를 세팅할 수 없습니다.");
            }

            LoadAndSetCartridgeImagesAsync().Forget();

            _animationCoroutine = StartCoroutine(SequenceFadeRoutine());
        }

        /// <summary>
        /// 페이지 비활성화 시 연출을 정지하고 할당된 메모리를 해제함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            
            ReleaseLoadedImages();

            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }
        }

        /// <summary>
        /// 카트리지 정보를 바탕으로 어드레서블에서 이미지를 비동기 병렬 로드함.
        /// Why: 동일한 카트리지 이미지를 5개의 응답 보기에 일괄 적용하기 위함.
        /// </summary>
        private async UniTaskVoid LoadAndSetCartridgeImagesAsync()
        {
            if (!SessionManager.Instance || string.IsNullOrEmpty(SessionManager.Instance.Cartridge)) return;

            string cartridge = SessionManager.Instance.Cartridge.ToLower();
            string legoCartKey = $"Lego_cart_{cartridge}";

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

                if (imgAnswer1 && results[0]) imgAnswer1.sprite = results[0];
                if (imgAnswer2 && results[1]) imgAnswer2.sprite = results[1];
                if (imgAnswer3 && results[2]) imgAnswer3.sprite = results[2];
                if (imgAnswer4 && results[3]) imgAnswer4.sprite = results[3];
                if (imgAnswer5 && results[4]) imgAnswer5.sprite = results[4];
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayTutorialPage1Controller] 어드레서블 로드 실패: {e.Message}");
                ReleaseLoadedImages();
            }
        }

        /// <summary>
        /// 현재 페이지에서 로드한 어드레서블 핸들의 메모리를 해제함.
        /// </summary>
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

        /// <summary> PC 역할에 맞는 유효 키보드 입력을 감지함. </summary>
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

            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }

        /// <summary>
        /// 텍스트와 이미지를 순차적으로 화면에 페이드 인 시키는 코루틴.
        /// </summary>
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