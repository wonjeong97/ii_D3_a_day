using System;
using System.Collections;
using Cysharp.Threading.Tasks; 
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
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        [SerializeField] private Reporter reporter;

        public bool isDebugMode;
        
        private bool _isTransitioning;
        private float _fadeTime = 1.0f;
        private bool _isQuitting;
        private bool _isQuitSafe;

        public ApiSettings ApiConfig { get; set; }

        public int firstTaggedPlayer;

        [Header("Player Color Sprites")]
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

        private void ActivateSecondaryDisplay()
        {
            if (Display.displays.Length > 1)
            {
                Display.displays[1].Activate();
            }
        }

        public Sprite GetColorSprite(ColorData color)
        {
            int index = (int)color;
            if (index >= 0 && playerColorSprites != null && index < playerColorSprites.Length)
            {
                return playerColorSprites[index];
            }
            return null;
        }

        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);
            if (settings != null) _fadeTime = settings.fadeTime;
            else _fadeTime = 1.0f;

            ApiConfig = JsonLoader.Load<ApiSettings>(GameConstants.Path.ApiSetting);
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
        }

        public void ChangeScene(string sceneName, bool doFade = false)
        {
            if (_isTransitioning) return;
            _isTransitioning = true;
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

        public void ReturnToTitle()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;
            StartCoroutine(ReturnToTitleRoutine());
        }

        private IEnumerator ReturnToTitleRoutine()
        {
            Debug.Log("[GameManager] 타이틀로 돌아감");

            if (SessionManager.Instance && SessionManager.Instance.CurrentUserIdx != 0 && ApiConfig != null)
            {
                int uid = SessionManager.Instance.CurrentUserIdx;
                string resetUrl = $"{ApiConfig.ResetStartUrl}?idx_user={uid}&code=d3";
                yield return StartCoroutine(SendGetRequestRoutine(resetUrl));

                string exitUrl = $"{ApiConfig.ExitRoomUrl}?code=d3&idx_user={uid}";
                yield return StartCoroutine(SendGetRequestRoutine(exitUrl));
            }

            firstTaggedPlayer = 0;
            if (SessionManager.Instance) SessionManager.Instance.ClearSession();

            _isTransitioning = false; 
            ChangeScene(GameConstants.Scene.Title);
        }

        #region API 호출 로직

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

                    if (req.result == UnityWebRequest.Result.Success) yield break;

                    if (attempt < maxRetries - 1)
                        yield return CoroutineData.GetWaitForSeconds(retryDelay);
                }
            }
        }

        public void SendResetStartAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserIdx == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.ResetStartUrl}?idx_user={SessionManager.Instance.CurrentUserIdx}&code=d3";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        public void SendExitRoomAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserIdx == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.ExitRoomUrl}?code=d3&idx_user={SessionManager.Instance.CurrentUserIdx}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        public void SendTimeUpdateAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserIdx == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.UpdateTimeUrl}?idx_user={SessionManager.Instance.CurrentUserIdx}&option=end&code=d3";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        public void SendValueUpdateAPI(int qNo, string side, int value)
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserIdx == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.UpdateValueUrl}?idx_user={SessionManager.Instance.CurrentUserIdx}&q_no={qNo}&side={side}&code=d3&value={value}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        public void SendPieceUpdateAPI(int value)
        {
            if (value < 0 || !SessionManager.Instance || SessionManager.Instance.CurrentUserIdx == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.UpdatePieceUrl}?idx_user={SessionManager.Instance.CurrentUserIdx}&code=d3&value={value}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        #endregion

        #region 프로그램 강제 종료 시 예외 처리

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
            if (SessionManager.Instance && SessionManager.Instance.CurrentUserIdx != 0 && ApiConfig != null)
            {
                int uid = SessionManager.Instance.CurrentUserIdx;
                string resetUrl = $"{ApiConfig.ResetStartUrl}?idx_user={uid}&code=d3";
                yield return StartCoroutine(SendGetRequestRoutine(resetUrl));

                string exitUrl = $"{ApiConfig.ExitRoomUrl}?code=d3&idx_user={uid}";
                yield return StartCoroutine(SendGetRequestRoutine(exitUrl));
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
            _isQuitSafe = true;
        }
#endif

        #endregion
    }
}