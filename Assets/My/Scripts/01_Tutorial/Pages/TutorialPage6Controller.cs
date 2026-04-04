using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using My.Scripts.Core;
using My.Scripts.Network;
using My.Scripts.Global;
using My.Scripts.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary>
    /// JSON에서 로드되는 튜토리얼 6페이지 데이터 구조체.
    /// </summary>
    [Serializable]
    public class TutorialPage6Data
    {
        public TextSetting descriptionTextA;
        public TextSetting descriptionTextB;
        public TextSetting cartNameA;
        public TextSetting cartNameB;
        public TextSetting cartNameC;
    }

    /// <summary>
    /// 카트리지 선택 연출이 진행되는 튜토리얼 6페이지 컨트롤러.
    /// 네트워크 역할에 따라 서로 다른 이미지를 비동기로 로드하고 시각적인 선택 애니메이션을 제공하기 위함.
    /// </summary>
    public class TutorialPage6Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup pageCanvasGroup;
        [SerializeField] private CanvasGroup cg1CanvasGroup;
        [SerializeField] private Text descriptionUI;

        [Header("Cart A")]
        [SerializeField] private CanvasGroup cartACanvas;
        [SerializeField] private RectTransform cartATransform;
        [SerializeField] private Text cartAText;
        [SerializeField] private Image cartAImage;

        [Header("Cart B")]
        [SerializeField] private CanvasGroup cartBCanvas;
        [SerializeField] private RectTransform cartBTransform;
        [SerializeField] private Text cartBText;
        [SerializeField] private Image cartBImage;

        [Header("Cart C")]
        [SerializeField] private CanvasGroup cartCCanvas;
        [SerializeField] private RectTransform cartCTransform;
        [SerializeField] private Text cartCText;
        [SerializeField] private Image cartCImage;

        [Header("Animation Settings")]
        [SerializeField] private float autoSelectDelay;
        [SerializeField] private float fadeDuration;
        [SerializeField] private float finalHoldTime;

        private Vector2 _selectedTargetPos;
        private TutorialPage6Data _cachedData;

        private Coroutine _autoSelectCoroutine;
        private Coroutine _waitAndInitCoroutine;
        private Coroutine _animationCoroutineA;
        private Coroutine _animationCoroutineB;
        private Coroutine _animationCoroutineC;

        private bool _isCompleted;
        private bool _isPreloadFinished;
        
        private List<AsyncOperationHandle<Sprite>> _loadedImageHandles;
        private CancellationTokenSource _cts;
        private Sprite[] _preloadedSprites;
        
        /// <summary>
        /// BaseFlowManager가 연출을 시작하기 전에 확인할 수 있는 페이지 준비 상태.
        /// 이미지가 완전히 준비될 때까지 화면 페이드 인을 지연시켜 깜빡임을 차단하기 위함.
        /// </summary>
        public override bool IsReady 
        {
            get { return _isPreloadFinished; }
        }

        /// <summary>
        /// 컴포넌트 활성화 시 핸들 관리 리스트를 초기화함.
        /// 필드 선언부의 리스트 초기화를 지양하고 생명주기에 맞춰 안전하게 메모리를 할당하기 위함.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            _loadedImageHandles = new List<AsyncOperationHandle<Sprite>>();
        }
        
        /// <summary>
        /// 매니저로부터 전달받은 페이지 데이터를 메모리에 캐싱함.
        /// 데이터 할당과 동시에 비동기 이미지 로드를 시작하여 대기 시간을 최소화하기 위함.
        /// </summary>
        /// <param name="data">JSON에서 역직렬화된 객체.</param>
        public override void SetupData(object data)
        {
            TutorialPage6Data pageData = data as TutorialPage6Data;
            if (pageData != null) _cachedData = pageData;

            _isPreloadFinished = false;

            if (_cts != null) 
            { 
                _cts.Cancel(); 
                _cts.Dispose(); 
            }
            _cts = new CancellationTokenSource();
            
            PreloadCartridgeImagesAsync(_cts.Token).Forget();
        }
        
        /// <summary>
        /// 어드레서블에서 카트리지 이미지를 비동기로 불러옴.
        /// 비활성화 상태에서 호출될 경우를 대비해 리스트를 직접 검사하고 초기화함.
        /// </summary>
        /// <param name="token">작업 취소를 위한 토큰.</param>
        private async UniTaskVoid PreloadCartridgeImagesAsync(CancellationToken token)
        {
            if (_loadedImageHandles == null)
            {
                _loadedImageHandles = new List<AsyncOperationHandle<Sprite>>();
            }

            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;
            
            string suffix = isServer ? "Server" : "Client";
            string[] keys = new string[] { $"Lego_A_{suffix}_0", $"Lego_B_{suffix}_0", $"Lego_C_{suffix}_0" };

            ReleaseLoadedImages();
            UniTask<Sprite>[] loadTasks = new UniTask<Sprite>[3];

            for (int i = 0; i < 3; i++)
            {
                AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(keys[i]);
                _loadedImageHandles.Add(handle);
                loadTasks[i] = handle.Task.AsUniTask();
            }

            try
            {
                Sprite[] results = await UniTask.WhenAll(loadTasks);
                if (token.IsCancellationRequested) return;

                _preloadedSprites = results;
                _isPreloadFinished = true;
            }
            catch (Exception e)
            {
                if (!token.IsCancellationRequested)
                {
                    Debug.LogError($"[TutorialPage6] 프리로드 실패: {e.Message}");
                    _isPreloadFinished = true;
                }
            }
        }

        /// <summary>
        /// 캐싱된 스프라이트를 안전하게 UI에 적용함.
        /// </summary>
        private void ApplyCachedSprites()
        {
            if (_preloadedSprites == null || _preloadedSprites.Length < 3) return;
            
            if (cartAImage && _preloadedSprites[0]) cartAImage.sprite = _preloadedSprites[0];
            if (cartBImage && _preloadedSprites[1]) cartBImage.sprite = _preloadedSprites[1];
            if (cartCImage && _preloadedSprites[2]) cartCImage.sprite = _preloadedSprites[2];
        }

        /// <summary>
        /// 캐싱된 텍스트 데이터를 UI에 갱신함.
        /// 서버/클라이언트 역할에 따라 서로 다른 텍스트를 제공하고 플레이스홀더를 실제 값으로 치환하기 위함.
        /// </summary>
        private void ApplyDataToUI()
        {
            if (_cachedData != null)
            {
                if (descriptionUI)
                {
                    string rawText = string.Empty;
                    bool isServer = false;

                    if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

                    if (isServer && _cachedData.descriptionTextA != null)
                        rawText = _cachedData.descriptionTextA.text;
                    else if (!isServer && _cachedData.descriptionTextB != null)
                        rawText = _cachedData.descriptionTextB.text;

                    if (!string.IsNullOrEmpty(rawText))
                    {
                        string cart = "A";
                        if (SessionManager.Instance && !string.IsNullOrEmpty(SessionManager.Instance.Cartridge))
                        {
                            cart = SessionManager.Instance.Cartridge;
                        }
                        string processedText = UIUtils.ReplacePlayerNamePlaceholders(rawText).Replace("{cartridge}", $"{cart} 카트리지");
                        descriptionUI.text = processedText;
                    }
                }

                if (cartAText && _cachedData.cartNameA != null) 
                {
                    SetUIText(cartAText, _cachedData.cartNameA);
                    cartAText.text = _cachedData.cartNameA.text;
                }
                if (cartBText && _cachedData.cartNameB != null) 
                {
                    SetUIText(cartBText, _cachedData.cartNameB);
                    cartBText.text = _cachedData.cartNameB.text;
                }
                if (cartCText && _cachedData.cartNameC != null) 
                {
                    SetUIText(cartCText, _cachedData.cartNameC);
                    cartCText.text = _cachedData.cartNameC.text;
                }
            }
        }

        /// <summary>
        /// 페이지 진입 시 연출 요소들을 초기화하고 이미지 로딩 대기 코루틴을 가동함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            if (_loadedImageHandles == null)
            {
                _loadedImageHandles = new List<AsyncOperationHandle<Sprite>>();
            }

            CalculateDynamicPositions();

            if (cg1CanvasGroup) cg1CanvasGroup.alpha = 1f;
            if (cartACanvas) cartACanvas.alpha = 1f;
            if (cartBCanvas) cartBCanvas.alpha = 1f;
            if (cartCCanvas) cartCCanvas.alpha = 1f;

            ApplyDataToUI();

            if (_waitAndInitCoroutine != null) StopCoroutine(_waitAndInitCoroutine);
            _waitAndInitCoroutine = StartCoroutine(WaitAndInitRoutine());
        }

        /// <summary>
        /// 이미지가 완전히 로드될 때까지 대기한 후 화면 페이드 인을 시작함.
        /// 리소스가 비어있는 상태에서 애니메이션이 일어나는 것을 차단하기 위함.
        /// </summary>
        private IEnumerator WaitAndInitRoutine()
        {
            while (!_isPreloadFinished)
            {
                yield return null;
            }

            ApplyCachedSprites();

            if (_autoSelectCoroutine != null) StopCoroutine(_autoSelectCoroutine);
            _autoSelectCoroutine = StartCoroutine(AutoSelectRoutine());
        }

        /// <summary>
        /// 페이지 이탈 시 실행 중인 코루틴 및 비동기 이미지 로드를 강제 중단함.
        /// </summary>
        public override void OnExit()
        {
            StopAllAnimationCoroutines();

            if (_autoSelectCoroutine != null)
            {
                StopCoroutine(_autoSelectCoroutine);
                _autoSelectCoroutine = null;
            }
            
            if (_waitAndInitCoroutine != null)
            {
                StopCoroutine(_waitAndInitCoroutine);
                _waitAndInitCoroutine = null;
            }

            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            ReleaseLoadedImages();
        }

        /// <summary>
        /// 로드된 어드레서블 핸들을 해제함.
        /// 불필요해진 텍스처 메모리를 명시적으로 반환하기 위함.
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

        /// <summary>
        /// 화면 해상도 비율에 맞춰 애니메이션 목표 좌표를 동적으로 계산함.
        /// 하드코딩된 기준 좌표를 다양한 종횡비 환경에서도 동일한 비율로 렌더링하기 위함.
        /// </summary>
        private void CalculateDynamicPositions()
        {
            RectTransform rt = transform as RectTransform;
            if (rt && rt.rect.width > 0 && rt.rect.height > 0)
            {
                float scaleX = rt.rect.width / 1920f;
                float scaleY = rt.rect.height / 1080f;

                _selectedTargetPos = new Vector2(900f * scaleX, -590f * scaleY);
            }
            else
            {
                _selectedTargetPos = new Vector2(900f, -590f);
            }
        }

        /// <summary>
        /// 일정 시간 대기 후 세션에 저장된 카트리지 정보를 기반으로 선택 연출을 시작함.
        /// </summary>
        private IEnumerator AutoSelectRoutine()
        {
            float delay = autoSelectDelay > 0f ? autoSelectDelay : 1.5f;
            yield return CoroutineData.GetWaitForSeconds(delay);

            if (!_isCompleted)
            {
                int targetCartIndex = 1;

                if (SessionManager.Instance)
                {
                    string cart = SessionManager.Instance.Cartridge;
                    if (!string.IsNullOrEmpty(cart))
                    {
                        switch (cart.ToUpper())
                        {
                            case "A": targetCartIndex = 1; break;
                            case "B": targetCartIndex = 2; break;
                            case "C": targetCartIndex = 3; break;
                            case "D": targetCartIndex = 4; break;
                            default: targetCartIndex = 1; break;
                        }
                    }
                }

                SelectLegoCart(targetCartIndex);
            }
        }

        /// <summary>
        /// 선택된 카트리지에 따라 개별 UI 애니메이션을 할당하고 실행함.
        /// </summary>
        private void SelectLegoCart(int selectedIndex)
        {
            _isCompleted = true;

            ManageCartAnimation(ref _animationCoroutineA, cartACanvas, cartATransform, selectedIndex == 1);
            ManageCartAnimation(ref _animationCoroutineB, cartBCanvas, cartBTransform, selectedIndex == 2);
            ManageCartAnimation(ref _animationCoroutineC, cartCCanvas, cartCTransform, selectedIndex == 3);

            StartCoroutine(FinalTransitionRoutine());
        }

        /// <summary>
        /// 애니메이션 종료 후 화면을 페이드 아웃하고 다음 단계로 전환함.
        /// </summary>
        private IEnumerator FinalTransitionRoutine()
        {
            float holdTime = finalHoldTime > 0f ? finalHoldTime : 3.0f;
            yield return CoroutineData.GetWaitForSeconds(holdTime);

            float elapsed = 0f;
            float duration = fadeDuration > 0f ? fadeDuration : 0.5f;
            float startAlpha = cg1CanvasGroup ? cg1CanvasGroup.alpha : 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (cg1CanvasGroup) cg1CanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }

            if (cg1CanvasGroup) cg1CanvasGroup.alpha = 0f;
            if (pageCanvasGroup) pageCanvasGroup.alpha = 1f;

            if (onStepComplete != null) onStepComplete.Invoke(0);
        }

        /// <summary>
        /// 기존 애니메이션을 중단하고 새로운 카트리지 애니메이션 코루틴을 할당함.
        /// </summary>
        private void ManageCartAnimation(ref Coroutine currentRoutine, CanvasGroup cg, RectTransform rectTransform, bool isSelected)
        {
            if (currentRoutine != null)
            {
                StopCoroutine(currentRoutine);
                currentRoutine = null;
            }

            if (cg && rectTransform)
            {
                currentRoutine = StartCoroutine(CartAnimationRoutine(cg, rectTransform, isSelected));
            }
        }

        /// <summary>
        /// 선택 여부에 따라 카트리지의 알파값과 위치를 선형 보간함.
        /// </summary>
        private IEnumerator CartAnimationRoutine(CanvasGroup cg, RectTransform rectTransform, bool isSelected)
        {
            float elapsed = 0f;
            float duration = fadeDuration > 0f ? fadeDuration : 0.5f;
            float startAlpha = cg.alpha;
            float targetAlpha = isSelected ? 1f : 0f;
            Vector2 startPos = rectTransform.anchoredPosition;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (cg) cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

                if (isSelected && rectTransform)
                {
                    rectTransform.anchoredPosition = Vector2.Lerp(startPos, _selectedTargetPos, t);
                }

                yield return null;
            }

            if (cg) cg.alpha = targetAlpha;
            if (isSelected && rectTransform) rectTransform.anchoredPosition = _selectedTargetPos;

            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("레고_2");
        }

        /// <summary>
        /// 모든 카트리지 애니메이션 코루틴을 강제 중단함.
        /// </summary>
        private void StopAllAnimationCoroutines()
        {
            if (_animationCoroutineA != null) StopCoroutine(_animationCoroutineA);
            if (_animationCoroutineB != null) StopCoroutine(_animationCoroutineB);
            if (_animationCoroutineC != null) StopCoroutine(_animationCoroutineC);
        }
    }
}