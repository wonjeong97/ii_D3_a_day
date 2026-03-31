using System.Collections;
using My.Scripts.Core.Data;
using My.Scripts.Network; 
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 대규모 리소스 로드 또는 네트워크 동기화가 필요한 구간에서 대기 화면을 표시하는 페이지 컨트롤러.
    /// 양쪽 PC가 모두 준비될 때까지 흐름을 일시 중단하고 상태를 동기화하기 위함.
    /// </summary>
    public class Page_Loading : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private CanvasGroup mainCg;
        [SerializeField] private Text text1UI;
        [SerializeField] private Text text2UI;

        [Header("Settings")]
        [SerializeField] private float waitTime;
        [SerializeField] private float fadeDuration;

        private CommonLoadingData _cachedData;
        private Coroutine _loadingCoroutine;
        private bool _isCompleted;
        
        private string _readyCmd;
        private string _completeCmd;
        private bool _isRemoteReady;

        /// <summary>
        /// 객체 생성 시 네트워크 메시지 수신 이벤트를 구독함.
        /// </summary>
        private void Start()
        {
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
            }
        }

        /// <summary>
        /// 동기화에 사용할 준비 완료 및 실행 완료 명령어를 설정함.
        /// 씬별로 다른 커맨드를 사용하여 네트워크 메시지 간섭을 방지하기 위함.
        /// </summary>
        /// <param name="readyCmd">상대방에게 준비됨을 알리는 커맨드.</param>
        /// <param name="completeCmd">모든 준비가 끝나 다음으로 넘어감을 알리는 커맨드.</param>
        public void SetSyncCommands(string readyCmd, string completeCmd)
        {
            _readyCmd = readyCmd;
            _completeCmd = completeCmd;
        }

        /// <summary>
        /// 외부로부터 전달받은 로딩 페이지 데이터를 메모리에 캐싱함.
        /// </summary>
        /// <param name="data">CommonLoadingData 타입의 데이터 객체.</param>
        public override void SetupData(object data)
        {
            CommonLoadingData pageData = data as CommonLoadingData;
            if (pageData != null) _cachedData = pageData;
        }

        /// <summary>
        /// 페이지 진입 시 UI를 초기화하고 동기화 루틴을 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            ApplyDataToUI();

            if (mainCg) mainCg.alpha = 0f;

            if (_loadingCoroutine != null) StopCoroutine(_loadingCoroutine);
            _loadingCoroutine = StartCoroutine(LoadingRoutine());
        }

        /// <summary>
        /// 페이지 이탈 시 진행 중인 루틴과 사운드를 정지함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            _isRemoteReady = false;

            if (_loadingCoroutine != null)
            {
                StopCoroutine(_loadingCoroutine);
                _loadingCoroutine = null;
            }

            if (SoundManager.Instance) SoundManager.Instance.StopSFX();
        }

        /// <summary>
        /// 캐싱된 데이터를 UI 텍스트 컴포넌트에 적용함.
        /// </summary>
        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;
            if (text1UI) SetUIText(text1UI, _cachedData.text1);
            if (text2UI) SetUIText(text2UI, _cachedData.text2);
        }

        /// <summary>
        /// 화면 페이드 인 후 상대방 PC의 준비 신호를 대기함.
        /// 늦게 도착한 PC가 대기 중인 PC의 락을 즉시 해제할 수 있도록 지속적으로 신호를 발송함.
        /// </summary>
        private IEnumerator LoadingRoutine()
        {
            if (mainCg)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    mainCg.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
                    yield return null;
                }
                mainCg.alpha = 1f;
            }

            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("레고_5");

            if (!string.IsNullOrEmpty(_readyCmd) && !string.IsNullOrEmpty(_completeCmd))
            {
                if (TcpManager.Instance)
                {
                    TcpManager.Instance.SendMessageToTarget(_readyCmd, "");
                }

                while (!_isRemoteReady)
                {
                    yield return CoroutineData.GetWaitForSeconds(1.0f);
                    if (TcpManager.Instance)
                    {
                        TcpManager.Instance.SendMessageToTarget(_readyCmd, "");
                    }
                }

                // 양쪽 모두 준비된 후 연출 안정성을 위해 추가 대기 시간을 가짐.
                yield return CoroutineData.GetWaitForSeconds(3.0f);

                if (TcpManager.Instance && TcpManager.Instance.IsServer)
                {
                    TcpManager.Instance.SendMessageToTarget(_completeCmd, "");
                }
                CompletePage();
            }
            else
            {
                // 동기화 커맨드가 없는 경우 지정된 시간만큼만 대기함.
                yield return CoroutineData.GetWaitForSeconds(waitTime);
                CompletePage();
            }
        }

        /// <summary>
        /// 수신된 네트워크 메시지를 분석하여 상대방의 준비 상태를 갱신하거나 완료 처리를 수행함.
        /// </summary>
        /// <param name="msg">수신된 TcpMessage 객체.</param>
        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg == null) return;

            if (!string.IsNullOrEmpty(_readyCmd) && msg.command == _readyCmd)
            {
                _isRemoteReady = true;
            }
            else if (!string.IsNullOrEmpty(_completeCmd) && msg.command == _completeCmd)
            {
                CompletePage();
            }
        }

        /// <summary>
        /// 로딩 시퀀스를 종료하고 매니저에게 페이지 완료 이벤트를 전달함.
        /// </summary>
        private void CompletePage()
        {
            if (_isCompleted) return;
            _isCompleted = true;

            if (onStepComplete != null) onStepComplete.Invoke(0);
        }
        
        /// <summary>
        /// 객체 파괴 시 등록된 네트워크 이벤트를 해제하여 메모리 누수를 방지함.
        /// </summary>
        private void OnDestroy()
        {
            if (TcpManager.Instance) TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
        }
    }
}