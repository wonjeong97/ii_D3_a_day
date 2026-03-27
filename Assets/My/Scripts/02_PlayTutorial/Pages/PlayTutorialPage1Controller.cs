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
    [Serializable]
    public class PlayTutorialPage1Data
    {
        public TextSetting text1;
        public TextSetting text2;
        public TextSetting textPopupWarning; 
        public TextSetting textPopupTimeout; 
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

        [Header("Popup UI")]
        [SerializeField] private CanvasGroup popupCanvasGroup;
        [SerializeField] private Text popupTextUI;

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float waitBetweenFades = 0.5f;

        private PlayTutorialPage1Data _cachedData;
        private Coroutine _animationCoroutine;
        private Coroutine _inactivityMonitorCoroutine;
        private Coroutine _popupFadeOutCoroutine;

        private bool _isCompleted;
        private bool _canAcceptInput;
        
        private readonly List<AsyncOperationHandle<Sprite>> _loadedImageHandles = new List<AsyncOperationHandle<Sprite>>();
        private CancellationTokenSource _cts;

        public KeyCode PressedKey { get; private set; } = KeyCode.None;
        public int PressedAnswerIndex { get; private set; } = -1; 

        /// <summary>
        /// 페이지 데이터 초기화.
        /// Why: 전달된 UI 세팅 데이터를 캐싱하여 페이지 활성화 시 사용하기 위함.
        /// </summary>
        /// <param name="data">JSON에서 역직렬화된 데이터 객체.</param>
        public override void SetupData(object data)
        {
            PlayTutorialPage1Data pageData = data as PlayTutorialPage1Data;
            
            if (pageData != null) 
            {
                _cachedData = pageData;
            }
            else 
            {
                UnityEngine.Debug.LogWarning("[PlayTutorialPage1Controller] 전달된 데이터가 PlayTutorialPage1Data 타입이 아닙니다.");
            }
        }

        /// <summary>
        /// 페이지 진입 시 초기화 및 연출 시작.
        /// Why: 컴포넌트를 초기화하고 RFID 이벤트 구독 및 비동기 이미지 로드를 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _canAcceptInput = false; 
            PressedKey = KeyCode.None;
            PressedAnswerIndex = -1;

            if (RfidManager.Instance) 
            {
                RfidManager.Instance.onAnswerReceived += OnRfidAnswerReceived;
            }

            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
            }

            if (text1Canvas) text1Canvas.alpha = 0f;
            if (imageGroupCanvas) imageGroupCanvas.alpha = 0f;
            if (text2Canvas) text2Canvas.alpha = 0f;
            if (popupCanvasGroup) popupCanvasGroup.alpha = 0f;

            string cart = "A";
            if (SessionManager.Instance)
            {
                if (!string.IsNullOrEmpty(SessionManager.Instance.Cartridge))
                {
                    cart = SessionManager.Instance.Cartridge;
                }
            }

            if (_cachedData != null)
            {
                if (text1UI) SetUIText(text1UI, _cachedData.text1);
                if (text2UI) SetUIText(text2UI, _cachedData.text2);

                if (text1UI)
                {
                    text1UI.text = text1UI.text.Replace("{Cartridge}", cart + " 카트리지");
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

        /// <summary>
        /// 페이지 이탈 시 자원 정리.
        /// Why: 코루틴 중단 및 RFID 폴링 취소, 메모리 해제를 수행함.
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

            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }

            if (_inactivityMonitorCoroutine != null) StopCoroutine(_inactivityMonitorCoroutine);
            if (_popupFadeOutCoroutine != null) StopCoroutine(_popupFadeOutCoroutine);
        }

        /// <summary>
        /// 카트리지 이미지 비동기 로드 및 적용.
        /// Why: 동기적 로드로 인한 프레임 드랍을 막고 안전하게 스프라이트를 적용함.
        /// </summary>
        /// <param name="cart">로드할 카트리지 문자열.</param>
        /// <param name="token">작업 취소를 위한 토큰.</param>
        private async UniTaskVoid LoadAndSetCartridgeImagesAsync(string cart, CancellationToken token)
        {
            string legoCartKey = "Lego_cart_" + cart.ToLower();
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
                    UnityEngine.Debug.LogError("[PlayTutorialPage1Controller] 어드레서블 로드 실패: " + e.Message);
                    ReleaseLoadedImages();
                }
            }
        }

        /// <summary>
        /// 로드된 어드레서블 핸들 해제.
        /// Why: 불필요해진 메모리를 명시적으로 반환함.
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
        /// 매 프레임 키보드 디버그 입력 검사.
        /// Why: 물리 하드웨어 없이 개발 환경에서도 원활한 테스트가 가능하도록 입력을 보조함. 극단적 최적화 연산 적용.
        /// </summary>
        private void Update()
        {
            if (_isCompleted || !_canAcceptInput) return;

            bool isServer = false;
            if (!object.ReferenceEquals(TcpManager.Instance, null)) 
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
        /// RFID 응답 수신 이벤트 핸들러.
        /// Why: 폴링 중 카드가 인식되면 해당 인덱스로 상태를 갱신하고 입력을 완료함.
        /// </summary>
        /// <param name="index">인식된 응답 인덱스 번호 (1~5).</param>
        private void OnRfidAnswerReceived(int index)
        {
            if (_isCompleted || !_canAcceptInput) return;
            
            PressedAnswerIndex = index; 
            OnValidInputReceived();
        }

        /// <summary>
        /// 현재 PC 역할에 따른 유효 키 추출.
        /// Why: 서버와 클라이언트가 서로 다른 입력 키 세트를 사용하도록 분리함.
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
        /// 화면 UI 페이드 인 연출.
        /// Why: 텍스트와 이미지를 순차적으로 노출시킨 후 입력 대기 상태로 전환함.
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
            if (RfidManager.Instance) 
            {
                RfidManager.Instance.StartPolling();
            }

            // 입력을 받을 수 있는 상태가 되면 무응답 감지 코루틴을 시작함
            if (_inactivityMonitorCoroutine != null) StopCoroutine(_inactivityMonitorCoroutine);
            _inactivityMonitorCoroutine = StartCoroutine(InactivityMonitorRoutine());
        }

        /// <summary>
        /// 2단계 무응답 감지 코루틴.
        /// Why: 일정 시간 입력이 없을 시 팝업을 띄우고, 끝까지 응답이 없으면 타이틀로 강제 초기화함.
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
            
            UnityEngine.Debug.LogWarning("[PlayTutorialPage1Controller] 장시간 무응답으로 인해 타이틀 화면으로 초기화합니다.");

            if (TcpManager.Instance) TcpManager.Instance.SendMessageToTarget("RETURN_TO_TITLE", "");
            if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
        }

        /// <summary>
        /// 입력 처리 완료.
        /// Why: 입력을 확정짓고 폴링을 중단한 후 매니저로 이벤트를 전달함.
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
        /// 네트워크 메시지 수신 처리 (타이틀 강제 복귀 동기화).
        /// </summary>
        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == "RETURN_TO_TITLE")
            {
                if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
            }
        }

        /// <summary>
        /// 캔버스 그룹 페이드 처리 유틸 코루틴.
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