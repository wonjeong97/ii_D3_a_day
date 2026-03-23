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
    /// 게임의 전반적인 상태, 씬 전환, 멀티 디스플레이 및 API 통신을 총괄하는 매니저 클래스.
    /// Why: 경고(Warning) 메시지들을 해결하기 위해 불필요한 비동기/using 선언을 제거하고 코드를 최적화함.
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

        // # TODO: Step2ThemeKey도 추후 SessionManager나 별도의 테마 매니저로 이관 고려
        public string Step2ThemeKey { get; set; } = "Sea_1";

        public int firstTaggedPlayer;

        [Header("Player Color Sprites")]
        [Tooltip("0:Cyan, 1:Pink, 2:Orange, 3:Green, 4:Red, 5:Yellow")]
        public Sprite[] playerColorSprites;

        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries = 10;
        [SerializeField] private float retryDelay = 1.0f;

      private void Awake()
        {
            Debug.unityLogger.logHandler = new TimestampLogHandler(Debug.unityLogger.logHandler);

            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // Why: 씬에 이미 SessionManager가 배치되어 아직 Awake 대기 중인지 먼저 확인하여 싱글톤 충돌과 인스펙터 값 증발을 방지함.
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

        private void Start()
        {
            Cursor.visible = false;
            Application.runInBackground = true;
            
            LoadSettings();
            
            if (reporter && reporter.show) reporter.show = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Application.wantsToQuit -= WantsToQuit;
            }
        }

        /// <summary>
        /// 두 번째 디스플레이(듀얼 모니터)를 강제로 활성화함.
        /// </summary>
        private void ActivateSecondaryDisplay()
        {
            if (Display.displays.Length > 1)
            {
                Display.displays[1].Activate();
                Debug.Log("[GameManager] Secondary display (Display 2) activated.");
            }
        }

        /// <summary>
        /// 인덱스에 해당하는 플레이어 컬러 스프라이트를 반환함.
        /// </summary>
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
        /// 설정 JSON 파일과 API JSON 파일을 파싱하여 내부 변수를 갱신함.
        /// </summary>
        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);
            if (settings != null)
            {
                _fadeTime = settings.fadeTime;
            }
            else
            {
                _fadeTime = 1.0f;
            }

            ApiConfig = JsonLoader.Load<ApiSettings>(GameConstants.Path.ApiSetting);
            if (ApiConfig == null) Debug.LogError("[GameManager] API.json 설정 파일을 로드하지 못했습니다.");
        }

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

            if (Input.GetKeyDown(KeyCode.F))
            {
                isDebugMode = !isDebugMode;
                Debug.Log($"<color=yellow>[GameManager] 디버그 모드 {(isDebugMode ? "활성화" : "비활성화")} 됨</color>");
            }
        }

        // =========================================================================================

        /// <summary>
        /// 지정된 씬으로 전환을 시작함.
        /// </summary>
        public void ChangeScene(string sceneName, bool doFade = false)
        {
            if (_isTransitioning) return;

            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[GameManager] ChangeScene 실패: 유효하지 않은 씬 이름 '{sceneName}'");
                return;
            }
            
            _isTransitioning = true;
            Debug.Log($"[GameManager] Scene Transition Requested: {sceneName}");
            StartCoroutine(ChangeSceneRoutine(sceneName, doFade));
        }

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
        /// API를 리셋한 뒤 타이틀로 복귀함.
        /// </summary>
        public void ReturnToTitle()
        {
            if (_isTransitioning) return;
            
            Debug.Log("[GameManager] 타이틀로 돌아감");

            SendResetStartAPI();
            SendExitRoomAPI();

            firstTaggedPlayer = 0;

            if (SessionManager.Instance) SessionManager.Instance.ClearSession();

            ChangeScene(GameConstants.Scene.Title);
        }

        // =========================================================================================

        #region API 호출 로직

        /// <summary>
        /// 최대 제한 횟수까지 지정된 간격으로 타임아웃 10초의 GET 요청을 재시도함.
        /// </summary>
        private IEnumerator SendGetRequestRoutine(string url)
        {
#if UNITY_EDITOR
            Debug.Log($"<color=orange>[GameManager] 에디터 모드 방지: 라이브 서버 API 갱신을 생략합니다. ({url})</color>");
            yield break;
#endif

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 10; 
                    yield return req.SendWebRequest();

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        yield break;
                    }

                    if (attempt < maxRetries - 1)
                    {
                        Debug.LogWarning($"[GameManager] API 전송 실패 ({attempt + 1}/{maxRetries}): {req.error}. {retryDelay}초 후 재시도...");
                        yield return CoroutineData.GetWaitForSeconds(retryDelay);
                    }
                    else
                    {
                        Debug.LogError($"[GameManager] API 전송 최종 실패 (URL: {url}) - {req.error}");
                    }
                }
            }
        }

        public void SendResetStartAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.ResetStartUrl}?idx_user={SessionManager.Instance.CurrentUserId}&code=d3";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        public void SendExitRoomAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.ExitRoomUrl}?code=d3&idx_user={SessionManager.Instance.CurrentUserId}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        public void SendTimeUpdateAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.UpdateTimeUrl}?idx_user={SessionManager.Instance.CurrentUserId}&option=end&code=d3";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        public void SendValueUpdateAPI(int qNo, string side, int value)
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.UpdateValueUrl}?idx_user={SessionManager.Instance.CurrentUserId}&q_no={qNo}&side={side}&code=d3&value={value}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        public void SendPieceUpdateAPI(int value)
        {
            if (value < 0 || !SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.UpdatePieceUrl}?idx_user={SessionManager.Instance.CurrentUserId}&code=d3&value={value}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        #endregion

        #region 프로그램 강제 종료 시 예외 처리

        /// <summary>
        /// 앱 종료 시그널을 받았을 때 호출되며 강제 종료를 막고 안전한 종료 루틴을 우선 실행함.
        /// </summary>
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

        private IEnumerator QuitRoutine()
        {
#if !UNITY_EDITOR
            if (SessionManager.Instance && SessionManager.Instance.CurrentUserId != 0 && ApiConfig != null)
            {
                int uid = SessionManager.Instance.CurrentUserId;

                string resetUrl = $"{ApiConfig.ResetStartUrl}?idx_user={uid}&code=d3";
                using (UnityWebRequest req = UnityWebRequest.Get(resetUrl))
                {
                    req.timeout = 2; 
                    yield return req.SendWebRequest();
                }

                string exitUrl = $"{ApiConfig.ExitRoomUrl}?code=d3&idx_user={uid}";
                using (UnityWebRequest req = UnityWebRequest.Get(exitUrl))
                {
                    req.timeout = 2;
                    yield return req.SendWebRequest();
                }
            }
#else
            Debug.Log("<color=orange>[GameManager] 에디터 모드 방지: 강제 종료 시 실제 유저의 세션(Reset, Exit) 폭파 방지됨</color>");
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
        private void OnApplicationQuit()
        {
            if (_isQuitSafe) return; 
            
            Debug.Log("<color=orange>[GameManager] 에디터 모드 방지: 에디터 강제 종료 시 실제 유저 세션 보호됨</color>");
        }
#endif

        #endregion
    }
}