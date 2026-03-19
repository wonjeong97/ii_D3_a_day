using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Hardware;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets; // 어드레서블 추가
using UnityEngine.ResourceManagement.AsyncOperations; // 비동기 핸들 추가
using Cysharp.Threading.Tasks;
using Wonjeong.Data;
using Wonjeong.Utils;

namespace My.Scripts._02_PlayTutorial.Pages
{
    [Serializable]
    public class PlayTutorialPage1Data
    {
        public string imageKey; // 추가됨: JSON에서 "Lego_{API}" 등을 읽어오기 위함
        public TextSetting text1;
        public TextSetting text2;
    }
    
    /// <summary>
    /// 플레이 튜토리얼의 첫 번째 페이지 컨트롤러.
    /// Why: 연출 완료 후 1초 주기로 RFID 카드를 스캔하며, 유저의 API 값에 따라 동적으로 보기 이미지를 로드함.
    /// </summary>
    public class PlayTutorialPage1Controller : GamePage
    {   
        [Header("UI Components")]
        [SerializeField] private CanvasGroup text1Canvas;
        [SerializeField] private Text text1UI;
        [SerializeField] private CanvasGroup imageGroupCanvas;
        [SerializeField] private CanvasGroup text2Canvas;
        [SerializeField] private Text text2UI;

        [Header("Answer Background Images")]
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
        private bool _isCompleted = false;
        private bool _canAcceptInput = false;

        // 어드레서블 메모리 관리를 위한 핸들 변수
        private AsyncOperationHandle<Sprite> _spriteHandle;

        public int SelectedCategory { get; private set; } = 0;

        public override void SetupData(object data)
        {
            PlayTutorialPage1Data pageData = data as PlayTutorialPage1Data;
            if (pageData != null) _cachedData = pageData;
        }

        /// <summary>
        /// 어드레서블에서 전달받은 새로운 스프라이트로 5개의 보기 배경 이미지를 교체함.
        /// </summary>
        public void ChangeAnswerImages(Sprite[] newSprites)
        {
            if (newSprites == null) return;

            Image[] targetImages = new Image[] { imgAnswer1, imgAnswer2, imgAnswer3, imgAnswer4, imgAnswer5 };
            
            for (int i = 0; i < 5; i++)
            {
                if (i < newSprites.Length && newSprites[i] && targetImages[i])
                {
                    targetImages[i].sprite = newSprites[i];
                }
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _canAcceptInput = false;
            SelectedCategory = 0;
            
            string rawText = string.Empty;
            string cartName = "_";

            if (text1Canvas) text1Canvas.alpha = 0f;
            if (imageGroupCanvas) imageGroupCanvas.alpha = 0f;
            if (text2Canvas) text2Canvas.alpha = 0f;
            
            if (_cachedData != null)
            {
                if (text1UI) SetUIText(text1UI, _cachedData.text1);
                if (text2UI) SetUIText(text2UI, _cachedData.text2);
            }

            if (_cachedData != null && _cachedData.text1 != null)
            {
                rawText = _cachedData.text1.text;
            }
           
            if (GameManager.Instance && !string.IsNullOrEmpty(GameManager.Instance.CartridgeKey))
            {
                cartName = GameManager.Instance.CartridgeKey;
            }

            if (!string.IsNullOrEmpty(rawText))
            {
                string processedText = rawText .Replace("{cartridge}", $"{cartName} 카트리지");
                text1UI.text = processedText;
            }

            // JSON 설정값에 따른 동적 이미지를 비동기로 불러와 씌움
            LoadDynamicImageAsync().Forget();

            if (RfidManager.Instance)
            {
                RfidManager.Instance.onCardRead += OnCardRecognized;
            }

            _animationCoroutine = StartCoroutine(SequenceFadeRoutine());
        }

        public override void OnExit()
        {
            base.OnExit();
            if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
            
            if (RfidManager.Instance)
            {
                RfidManager.Instance.onCardRead -= OnCardRecognized;
            }

            // 페이지 전환 시 불필요한 메모리 해제
            ReleaseDynamicImage();
        }

        /// <summary>
        /// JSON에 등록된 imageKey를 확인하여, '{API}' 태그를 현재 유저의 아바타 값으로 치환 후 이미지를 로드함.
        /// </summary>
        private async UniTaskVoid LoadDynamicImageAsync()
        {
            if (_cachedData == null || string.IsNullOrEmpty(_cachedData.imageKey)) return;
            
            string key = _cachedData.imageKey;
            
            // '{API}' 치환 로직 (GameManager의 CartridgeKey 사용)
            if (key.Contains("{API}"))
            {
                string apiValue = GameManager.Instance ? GameManager.Instance.CartridgeKey : "A";
                key = key.Replace("{API}", apiValue);
            }

            ReleaseDynamicImage(); 

            try
            {
                _spriteHandle = Addressables.LoadAssetAsync<Sprite>(key);
                Sprite loadedSprite = await _spriteHandle.ToUniTask();

                if (loadedSprite)
                {
                    // 로드한 1장의 이미지를 5개의 모든 보기 버튼에 동일하게 적용함
                    Sprite[] spritesToApply = new Sprite[] { loadedSprite, loadedSprite, loadedSprite, loadedSprite, loadedSprite };
                    ChangeAnswerImages(spritesToApply);
                    
                    UnityEngine.Debug.Log($"[PlayTutorialPage1] 동적 이미지 로드 성공 및 적용 완료 (Key: {key})");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[PlayTutorialPage1] 동적 이미지 로드 실패 (Key: {key}): {e.Message}");
            }
        }

        private void ReleaseDynamicImage()
        {
            if (_spriteHandle.IsValid())
            {
                Addressables.Release(_spriteHandle);
            }
        }

        private void Update()
        {
            if (_isCompleted || !_canAcceptInput) return;

            KeyCode pressed = GetValidKey(true);
            if (pressed != KeyCode.None)
            {
                int category = pressed - KeyCode.Alpha0; 
                UnityEngine.Debug.Log($"[PlayTutorialPage1] Debug: Key Pressed {pressed}. Injected Category {category}.");
                
                SelectedCategory = category; 
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

        private void OnCardRecognized(string uid, int category)
        {
            if (_isCompleted || !_canAcceptInput) return;

            if (category == 0) return;
            
            UnityEngine.Debug.Log($"<color=cyan>[PlayTutorialPage1] RFID Tagged: {uid} (Type: {category})</color>");

            SelectedCategory = category;
            OnValidInputReceived();
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
            StartAutoReadLoop().Forget();
        }

        private async UniTaskVoid StartAutoReadLoop()
        {
            while (!_isCompleted && _canAcceptInput && !this.GetCancellationTokenOnDestroy().IsCancellationRequested)
            {
                if (RfidManager.Instance) RfidManager.Instance.TryReadCard().Forget();
                await UniTask.Delay(TimeSpan.FromSeconds(1.0f), delayTiming: PlayerLoopTiming.Update);
            }
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