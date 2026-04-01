using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using My.Scripts.Core;
using My.Scripts.Network;
using My.Scripts.Global;
using My.Scripts.Hardware; 
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._02_PlayTutorial.Pages
{
    /// <summary>
    /// JSON에서 로드되는 조작 튜토리얼 1페이지 데이터 구조체.
    /// </summary>
    [Serializable]
    public class PlayTutorialPage1Data
    {
        public TextSetting text1;
        public TextSetting text2;
        public TextSetting textPopupWarning; 
        public TextSetting textPopupTimeout; 
    }

    /// <summary>
    /// 조작 튜토리얼의 첫 번째 페이지 컨트롤러.
    /// 카드를 한 번 올려놓는 기본 조작 방식을 학습시키기 위함.
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
        [SerializeField] private Image imgAnswer1;
        [SerializeField] private Image imgAnswer2;
        [SerializeField] private Image imgAnswer3;
        [SerializeField] private Image imgAnswer4;
        [SerializeField] private Image imgAnswer5;

        [Header("Popup UI")]
        [SerializeField] private CanvasGroup popupCanvasGroup;
        [SerializeField] private Text popupTextUI;

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration;
        [SerializeField] private float waitBetweenFades;

        private PlayTutorialPage1Data _cachedData;
        private Coroutine _animationCoroutine;
        private Coroutine _inactivityMonitorCoroutine;
        private Coroutine _popupFadeOutCoroutine;
        private Coroutine _waitAndInitCoroutine;

        private bool _isCompleted;
        private bool _canAcceptInput;
        private bool _isPreloadFinished;
        
        private List<AsyncOperationHandle<Sprite>> _loadedImageHandles;
        private CancellationTokenSource _cts;
        
        private string _cartridge;
        private Sprite[] _preloadedSprites;

        public KeyCode PressedKey { get; private set; }
        public int PressedAnswerIndex { get; private set; }

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
        /// 전달된 UI 세팅 데이터를 캐싱하여 페이지 활성화 시 렌더링에 활용함.
        /// 데이터 할당과 동시에 비동기 이미지 로드를 시작하여 대기 시간을 최소화하기 위함.
        /// </summary>
        /// <param name="data">JSON에서 역직렬화된 객체.</param>
        public override void SetupData(object data)
        {
            PlayTutorialPage1Data pageData = data as PlayTutorialPage1Data;
            if (pageData != null)
            {
                _cachedData = pageData;
            }
            else
            {
                _cachedData = null;
                Debug.LogWarning("[PlayTutorialPage1] SetupData 타입 불일치 또는 null 입력");
            }

            _preloadedSprites = null;
            _isPreloadFinished = false;

            if (_cts != null) 
            { 
                _cts.Cancel(); 
                _cts.Dispose(); 
            }
            _cts = new CancellationTokenSource();
            
            PreloadLegoImagesAsync(_cts.Token).Forget();
        }
        
        /// <summary>
        /// 어드레서블에서 카트리지 이미지를 비동기로 불러옴.
        /// 비활성화 상태에서 호출될 경우를 대비해 리스트를 직접 검사하고 초기화함.
        /// </summary>
        /// <param name="token">작업 취소를 위한 토큰.</param>
        private async UniTaskVoid PreloadLegoImagesAsync(CancellationToken token)
        {
            if (_loadedImageHandles == null)
            {
                _loadedImageHandles = new List<AsyncOperationHandle<Sprite>>();
            }

            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;
            
            string roleStr = isServer ? "Server" : "Client";
            string cartStr = "A";

            if (SessionManager.Instance && !string.IsNullOrEmpty(SessionManager.Instance.Cartridge))
            {
                cartStr = SessionManager.Instance.Cartridge.ToUpper();
            }

            string[] keys = new string[5];
            for (int i = 0; i < 5; i++) 
            {
                keys[i] = $"Lego_{cartStr}_{roleStr}_{i + 1}";
            }

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

                _preloadedSprites = results;
                _isPreloadFinished = true;
            }
            catch (Exception e)
            {
                if (!token.IsCancellationRequested)
                {
                    Debug.LogError($"[PlayTutorialPage1] 프리로드 실패: {e.Message}");
                    _preloadedSprites = null;
                    _isPreloadFinished = true;
                }
            }
        }

        /// <summary>
        /// 캐싱된 스프라이트를 안전하게 UI에 적용함.
        /// </summary>
        private void ApplyCachedSprites()
        {
            if (_preloadedSprites == null || _preloadedSprites.Length < 5) return;
            
            if (imgAnswer1 && _preloadedSprites[0]) imgAnswer1.sprite = _preloadedSprites[0];
            if (imgAnswer2 && _preloadedSprites[1]) imgAnswer2.sprite = _preloadedSprites[1];
            if (imgAnswer3 && _preloadedSprites[2]) imgAnswer3.sprite = _preloadedSprites[2];
            if (imgAnswer4 && _preloadedSprites[3]) imgAnswer4.sprite = _preloadedSprites[3];
            if (imgAnswer5 && _preloadedSprites[4]) imgAnswer5.sprite = _preloadedSprites[4];
        }

        /// <summary>
        /// 페이지 진입 시 연출 요소들을 초기화하고 이미지 로딩 대기 코루틴을 가동함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            _isCompleted = false;
            _canAcceptInput = false; 
            PressedKey = KeyCode.None;
            PressedAnswerIndex = -1;
            _cartridge = "A";

            if (_loadedImageHandles == null)
            {
                _loadedImageHandles = new List<AsyncOperationHandle<Sprite>>();
            }

            if (RfidManager.Instance) RfidManager.Instance.onAnswerReceived += OnRfidAnswerReceived;
            if (TcpManager.Instance) TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;

            if (text1Canvas) text1Canvas.alpha = 0f;
            if (imageGroupCanvas) imageGroupCanvas.alpha = 0f;
            if (text2Canvas) text2Canvas.alpha = 0f;
            if (popupCanvasGroup) popupCanvasGroup.alpha = 0f;
            
            if (SessionManager.Instance)
            {
                if (!string.IsNullOrEmpty(SessionManager.Instance.Cartridge))
                {
                    _cartridge = SessionManager.Instance.Cartridge;
                }
            }

            if (_cachedData != null)
            {
                if (text1UI) SetUIText(text1UI, _cachedData.text1);
                if (text2UI) SetUIText(text2UI, _cachedData.text2);

                if (text1UI) text1UI.text = text1UI.text.Replace("{Cartridge}", _cartridge + " 카트리지");
            }

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

            if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
            _animationCoroutine = StartCoroutine(SequenceFadeRoutine());
        }

        /// <summary>
        /// 코루틴 중단 및 RFID 폴링 취소, 메모리 해제를 수행함.
        /// 백그라운드 연산 낭비 및 메모리 누수를 방지하기 위함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            
            if (RfidManager.Instance) 
            {
                RfidManager.Instance.StopPolling();
                RfidManager.Instance.onAnswerReceived -= OnRfidAnswerReceived;
            }

            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
            }

            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            ReleaseLoadedImages();

            if (_waitAndInitCoroutine != null) StopCoroutine(_waitAndInitCoroutine);
            if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
            if (_inactivityMonitorCoroutine != null) StopCoroutine(_inactivityMonitorCoroutine);
            if (_popupFadeOutCoroutine != null) StopCoroutine(_popupFadeOutCoroutine);
        }

        /// <summary>
        /// 로드된 어드레서블 핸들을 해제함.
        /// 불필요해진 메모리를 명시적으로 반환하기 위함.
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
        /// 매 프레임 키보드 디버그 입력을 검사함.
        /// 물리 하드웨어 없이 개발 환경에서도 원활한 테스트가 가능하도록 입력을 보조함. 
        /// </summary>
        private void Update()
        {
            if (_isCompleted || !_canAcceptInput) return;

            bool isServer = false;
            if (TcpManager.Instance) 
            {
                isServer = TcpManager.Instance.IsServer;
            }

            KeyCode pressed = GetValidKey(isServer);

            if (pressed != KeyCode.None)
            {
                PressedKey = pressed; 
                
                if (pressed >= KeyCode.Alpha1 && pressed <= KeyCode.Alpha5) 
                {
                    PressedAnswerIndex = pressed - KeyCode.Alpha1 + 1;
                }
                else if (pressed >= KeyCode.Alpha6 && pressed <= KeyCode.Alpha9) 
                {
                    PressedAnswerIndex = pressed - KeyCode.Alpha6 + 1;
                }
                else if (pressed == KeyCode.Alpha0) 
                {
                    PressedAnswerIndex = 5;
                }

                OnValidInputReceived();
            }
        }

        /// <summary>
        /// 폴링 중 카드가 인식되면 해당 인덱스로 상태를 갱신하고 입력을 확정함.
        /// </summary>
        /// <param name="index">인식된 응답 인덱스 번호 (1~5).</param>
        private void OnRfidAnswerReceived(int index)
        {
            if (_isCompleted || !_canAcceptInput) return;
            
            PressedAnswerIndex = index; 
            OnValidInputReceived();
        }

        /// <summary>
        /// 현재 PC 역할에 따른 유효 키를 추출함.
        /// 서버와 클라이언트가 서로 다른 키 세트를 사용하게 분리하여 하나의 키보드로 양쪽 PC를 시뮬레이션하기 위함.
        /// </summary>
        /// <param name="isServer">서버 여부.</param>
        /// <returns>눌린 KeyCode 반환.</returns>
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

        /// <summary>
        /// 텍스트와 이미지를 순차적으로 노출시킨 후 입력 대기 상태로 전환함.
        /// </summary>
        private IEnumerator SequenceFadeRoutine()
        {
            float fDuration = fadeDuration > 0f ? fadeDuration : 0.5f;
            float waitDuration = waitBetweenFades > 0f ? waitBetweenFades : 0.5f;

            if (text1Canvas) yield return StartCoroutine(FadeCanvasGroupRoutine(text1Canvas, 0f, 1f, fDuration));
            yield return CoroutineData.GetWaitForSeconds(waitDuration);

            if (imageGroupCanvas) yield return StartCoroutine(FadeCanvasGroupRoutine(imageGroupCanvas, 0f, 1f, fDuration));
            yield return CoroutineData.GetWaitForSeconds(waitDuration);

            if (text2Canvas) yield return StartCoroutine(FadeCanvasGroupRoutine(text2Canvas, 0f, 1f, fDuration));

            yield return CoroutineData.GetWaitForSeconds(0.5f);
            
            _canAcceptInput = true;
            if (RfidManager.Instance) 
            {
                RfidManager.Instance.StartPolling();
            }

            if (_inactivityMonitorCoroutine != null) StopCoroutine(_inactivityMonitorCoroutine);
            _inactivityMonitorCoroutine = StartCoroutine(InactivityMonitorRoutine());
        }

        /// <summary>
        /// 일정 시간 입력이 없을 시 경고 팝업을 띄우고 끝까지 응답이 없으면 타이틀로 강제 초기화함.
        /// 기기 방치 시 원래 대기 상태로 복구하기 위함.
        /// </summary>
        private IEnumerator InactivityMonitorRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(20.0f);
            if (_isCompleted) yield break;

            if (_cachedData != null && _cachedData.textPopupWarning != null && popupTextUI)
            {
                SetUIText(popupTextUI, _cachedData.textPopupWarning);
            }

            if (popupCanvasGroup) yield return StartCoroutine(FadeCanvasGroupRoutine(popupCanvasGroup, popupCanvasGroup.alpha, 1f, 0.5f));
            yield return CoroutineData.GetWaitForSeconds(3.0f);
            if (popupCanvasGroup) yield return StartCoroutine(FadeCanvasGroupRoutine(popupCanvasGroup, popupCanvasGroup.alpha, 0f, 0.5f));

            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_23");

            yield return CoroutineData.GetWaitForSeconds(10.0f);
            if (_isCompleted) yield break;

            if (_cachedData != null && _cachedData.textPopupTimeout != null && popupTextUI)
            {
                SetUIText(popupTextUI, _cachedData.textPopupTimeout);
            }

            if (popupCanvasGroup) yield return StartCoroutine(FadeCanvasGroupRoutine(popupCanvasGroup, popupCanvasGroup.alpha, 1f, 0.5f));
            yield return CoroutineData.GetWaitForSeconds(3.0f);

            if (_isCompleted) yield break;
            
            Debug.LogWarning("[PlayTutorialPage1Controller] 장시간 무응답으로 인해 타이틀 화면으로 초기화합니다.");

            if (TcpManager.Instance) TcpManager.Instance.SendMessageToTarget("RETURN_TO_TITLE", "");
            if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
        }

        /// <summary>
        /// 입력을 확정짓고 폴링을 중단한 후 매니저로 완료 이벤트를 전달함.
        /// </summary>
        private void OnValidInputReceived()
        {
            if (_inactivityMonitorCoroutine != null) StopCoroutine(_inactivityMonitorCoroutine);
            if (SoundManager.Instance) SoundManager.Instance.StopSFX();
            
            if (popupCanvasGroup && popupCanvasGroup.alpha > 0f)
            {
                if (_popupFadeOutCoroutine != null) StopCoroutine(_popupFadeOutCoroutine);
                _popupFadeOutCoroutine = StartCoroutine(FadeCanvasGroupRoutine(popupCanvasGroup, popupCanvasGroup.alpha, 0f, 0.5f));
            }

            if (RfidManager.Instance) 
            {
                RfidManager.Instance.StopPolling();
            }

            _isCompleted = true; 
            
            if (onStepComplete != null) 
            {
                onStepComplete.Invoke(0);
            }
        }

        /// <summary>
        /// 상대 PC의 강제 종료 또는 타임아웃 발생 시 현재 씬도 타이틀로 강제 복귀시켜 상태를 동기화함.
        /// </summary>
        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == "RETURN_TO_TITLE")
            {
                if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
            }
        }

        /// <summary>
        /// 캔버스 그룹의 알파값을 선형 보간하여 페이드 연출을 수행함.
        /// </summary>
        private IEnumerator FadeCanvasGroupRoutine(CanvasGroup target, float start, float end, float duration)
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