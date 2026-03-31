using System.Collections;
using My.Scripts.Core;
using My.Scripts.Core.Data;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Wonjeong.Data;
using Wonjeong.Reporter;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Global
{ 
    /// <summary>
    /// 게임 전체의 생명주기, 씬 전환, 전역 설정 및 서버 API 통신을 총괄하는 메인 매니저.
    /// 싱글톤으로 유지되며 프로그램 종료 시 세션 정리 로직을 수행함.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        [SerializeField] private Reporter reporter;

        public bool isDebugMode;
        
        private bool _isTransitioning;
        private float _fadeTime;
        private bool _isQuitting;
        private bool _isQuitSafe;

        public ApiSettings ApiConfig { get; set; }

        public int firstTaggedPlayer;

        [Header("Player Color Sprites")]
        public Sprite[] playerColorSprites;

        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries = 10;
        [SerializeField] private float retryDelay = 1.0f;

        /// <summary>
        /// 싱글톤 인스턴스를 초기화하고 타임스탬프 로그 핸들러를 등록함.
        /// 세션 매니저가 없을 경우 동적으로 생성하여 데이터 연속성을 보장함.
        /// </summary>
        private void Awake()
        {
            Debug.unityLogger.logHandler = new TimestampLogHandler(Debug.unityLogger.logHandler);

            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                if (!SessionManager.Instance)
                {
                    SessionManager existingSession = FindFirstObjectByType<SessionManager>();
                    if (!existingSession)
                    {
                        GameObject sessionObj = new GameObject("SessionManager");
                        sessionObj.AddComponent<SessionManager>();
                    }
                }

                Application.wantsToQuit += WantsToQuit;
                ActivateSecondaryDisplay();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 초기 실행 환경 설정 및 외부 JSON 데이터를 로드함.
        /// 마우스 커서를 숨기고 백그라운드 실행을 허용하여 다중 디스플레이 환경을 최적화함.
        /// </summary>
        private void Start()
        {
            Cursor.visible = false;
            Application.runInBackground = true;
            
            LoadSettings();
            
            if (reporter && reporter.show) reporter.show = false;
        }

        /// <summary>
        /// 객체 파괴 시 종료 이벤트 구독을 해제함.
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Application.wantsToQuit -= WantsToQuit;
            }
        }

        /// <summary>
        /// 연결된 보조 모니터가 있을 경우 두 번째 디스플레이를 활성화함.
        /// </summary>
        private void ActivateSecondaryDisplay()
        {
            if (Display.displays.Length > 1)
            {
                Display.displays[1].Activate();
            }
        }

        /// <summary>
        /// 컬러 데이터 값에 대응하는 플레이어 색상 스프라이트를 반환함.
        /// </summary>
        /// <param name="color">ColorData 열거형 값.</param>
        /// <returns>매칭된 Sprite 객체.</returns>
        public Sprite GetColorSprite(ColorData color)
        {
            int index = (int)color;
            if (index >= 0 && playerColorSprites != null && index < playerColorSprites.Length)
            {
                return playerColorSprites[index];
            }
            return null;
        }

        /// <summary>
        /// 외부 JSON 파일로부터 페이드 시간 및 API 엔드포인트 설정을 로드함.
        /// </summary>
        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);
            if (settings != null) _fadeTime = settings.fadeTime;
            else _fadeTime = 1.0f;

            ApiConfig = JsonLoader.Load<ApiSettings>(GameConstants.Path.ApiSetting);
        }

        /// <summary>
        /// 매 프레임 디버그 도구 호출 및 커서 가시성 제어 키를 감시함.
        /// </summary>
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.D) && reporter)
            {
                reporter.showGameManagerControl = !reporter.showGameManagerControl;
                if (reporter.show) reporter.show = false;
            }
            else if (Input.GetKeyDown(KeyCode.M)) 
            {
                Cursor.visible = !Cursor.visible;
            }
        }

        /// <summary>
        /// 페이드 효과를 포함하여 다른 씬으로 비동기 전환함.
        /// 중복 전환 요청을 방지하기 위해 트랜지션 플래그를 사용함.
        /// </summary>
        /// <param name="sceneName">이동할 씬 이름.</param>
        /// <param name="doFade">페이드 아웃/인 적용 여부.</param>
        public void ChangeScene(string sceneName, bool doFade = false)
        {
            if (_isTransitioning) return;
            _isTransitioning = true;
            StartCoroutine(ChangeSceneRoutine(sceneName, doFade));
        }

        /// <summary>
        /// 페이드 아웃, 씬 로드, 페이드 인 과정을 순차적으로 제어함.
        /// 씬 로드 직후 발생할 수 있는 프레임 드랍을 고려해 0.2초의 대기 시간을 가짐.
        /// </summary>
        private IEnumerator ChangeSceneRoutine(string sceneName, bool doFade)
        {
            if (doFade && FadeManager.Instance)
            {
                bool fadeDone = false;
                FadeManager.Instance.FadeOut(_fadeTime, () => fadeDone = true);
                while (!fadeDone) yield return null;
            }

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            while (asyncLoad != null && !asyncLoad.isDone) yield return null;

            yield return CoroutineData.GetWaitForSeconds(0.2f);

            if (doFade && FadeManager.Instance)
            {
                bool fadeInDone = false;
                FadeManager.Instance.FadeIn(_fadeTime, () => fadeInDone = true);
                while (!fadeInDone) yield return null;
            }

            _isTransitioning = false;
        }

        /// <summary>
        /// 현재 유저 세션을 정리하고 타이틀 화면으로 복귀함.
        /// 서버에 유저 퇴장 및 방 리셋 신호를 발송하여 다음 체험자가 원활히 시작하게 함.
        /// </summary>
        public void ReturnToTitle()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;
            StartCoroutine(ReturnToTitleRoutine(isClear));
        }

        /// <summary>
        /// 세션 데이터 초기화 및 서버 API 호출 후 씬을 전환함.
        /// </summary>
        private IEnumerator ReturnToTitleRoutine()
        {
            Debug.Log("[GameManager] 타이틀 복귀 시퀀스 시작");

            if (SessionManager.Instance && SessionManager.Instance.CurrentUserIdx != 0 && ApiConfig != null)
            {
                int uid = SessionManager.Instance.CurrentUserIdx;
                string moduleCode = SessionManager.Instance.CurrentModuleCode;
                
                if (!isClear)
                {
                    string resetUrl = $"{ApiConfig.ResetStartUrl}?idx_user={uid}&code={moduleCode}";
                    yield return StartCoroutine(SendGetRequestRoutine(resetUrl));
                }

                string exitUrl = $"{ApiConfig.ExitRoomUrl}?code={moduleCode}&idx_user={uid}";
                yield return StartCoroutine(SendGetRequestRoutine(exitUrl));
            }

            firstTaggedPlayer = 0;
            if (SessionManager.Instance) SessionManager.Instance.ClearSession();

            _isTransitioning = false; 
            ChangeScene(GameConstants.Scene.Title, true);
        }

        #region API 호출 로직

        /// <summary>
        /// 지정된 URL로 GET 요청을 발송하며 실패 시 지정된 횟수만큼 재시도함.
        /// 에디터 모드에서는 서버 데이터 꼬임을 방지하기 위해 실제 통신을 차단함.
        /// </summary>
        private IEnumerator SendGetRequestRoutine(string url)
        {
#if UNITY_EDITOR
            Debug.Log($"<color=orange>[GameManager] 에디터 모드: API 호출 생략 ({url})</color>");
            yield break;
#endif
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 10; 
                    yield return req.SendWebRequest();

                    if (req.result == UnityWebRequest.Result.Success) yield break;

                    if (attempt < maxRetries - 1)
                        yield return CoroutineData.GetWaitForSeconds(retryDelay);
                }
            }
        }

        /// <summary>
        /// 서버에 방 리셋 및 시작 상태 초기화 명령을 전달함.
        /// </summary>
        public void SendResetStartAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserIdx == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.ResetStartUrl}?idx_user={SessionManager.Instance.CurrentUserIdx}&code={SessionManager.Instance.CurrentModuleCode}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        /// <summary>
        /// 서버에 현재 유저의 퇴장 처리를 요청함.
        /// </summary>
        public void SendExitRoomAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserIdx == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.ExitRoomUrl}?code={SessionManager.Instance.CurrentModuleCode}&idx_user={SessionManager.Instance.CurrentUserIdx}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        /// <summary>
        /// 콘텐츠 종료 시각을 업데이트하기 위한 API를 발송함.
        /// </summary>
        public void SendTimeUpdateAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserIdx == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.UpdateTimeUrl}?idx_user={SessionManager.Instance.CurrentUserIdx}&option=end&code={SessionManager.Instance.CurrentModuleCode}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        /// <summary>
        /// 체험 과정에서 발생한 선택지 결과 값을 서버에 저장함.
        /// </summary>
        /// <param name="qNo">문항 번호.</param>
        /// <param name="side">기기 위치 (left/right).</param>
        /// <param name="value">선택한 결과 값.</param>
        public void SendValueUpdateAPI(int qNo, string side, int value)
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserIdx == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.UpdateValueUrl}?idx_user={SessionManager.Instance.CurrentUserIdx}&q_no={qNo}&side={side}&code={SessionManager.Instance.CurrentModuleCode}&value={value}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        /// <summary>
        /// 획득한 보상 조각 개수를 서버에 업데이트함.
        /// </summary>
        /// <param name="value">업데이트할 조각 수.</param>
        public void SendPieceUpdateAPI(int value)
        {
            if (value < 0 || !SessionManager.Instance || SessionManager.Instance.CurrentUserIdx == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.UpdatePieceUrl}?idx_user={SessionManager.Instance.CurrentUserIdx}&code={SessionManager.Instance.CurrentModuleCode}&value={value}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        #endregion

        #region 프로그램 강제 종료 시 예외 처리

        /// <summary>
        /// 유니티 종료 요청 수신 시 서버 데이터 정리를 위한 코루틴을 우선 실행함.
        /// 비정상 종료 시에도 방 상태를 초기화하여 시스템 락을 방지하기 위함.
        /// </summary>
        /// <returns>즉시 종료 여부.</returns>
        private bool WantsToQuit()
        {
            if (_isQuitSafe) return true;

            if (!_isQuitting)
            {
                _isQuitting = true;
                StartCoroutine(QuitRoutine());
            }

            return false;
        }

        /// <summary>
        /// 서버 데이터 정리 완료 후 안전하게 프로그램을 종료함.
        /// </summary>
        private IEnumerator QuitRoutine()
        {
#if !UNITY_EDITOR
            if (SessionManager.Instance && SessionManager.Instance.CurrentUserIdx != 0 && ApiConfig != null)
            {
                int uid = SessionManager.Instance.CurrentUserIdx;
                string moduleCode = SessionManager.Instance.CurrentModuleCode;
                
                string resetUrl = $"{ApiConfig.ResetStartUrl}?idx_user={uid}&code={moduleCode}";
                yield return StartCoroutine(SendGetRequestRoutine(resetUrl));

                string exitUrl = $"{ApiConfig.ExitRoomUrl}?code={moduleCode}&idx_user={uid}";
                yield return StartCoroutine(SendGetRequestRoutine(exitUrl));
            }
#else
            Debug.Log("<color=orange>[GameManager] 에디터 모드: 세션 유지 상태 종료</color>");
#endif

            _isQuitSafe = true;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            
            yield break;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 종료 시 종료 플래그를 동기화함.
        /// </summary>
        private void OnApplicationQuit()
        {
            if (_isQuitSafe) return; 
            _isQuitSafe = true;
        }
#endif

        #endregion
    }
}