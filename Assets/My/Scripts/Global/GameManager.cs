using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wonjeong.Reporter;
using Wonjeong.UI; // FadeManager 접근을 위해 추가

namespace My.Scripts.Global
{ 
    public enum UserType { A, B, C, D, E, F }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        [SerializeField] private Reporter reporter;

        [Header("Settings")]
        [SerializeField] private float inactivityLimit = 60f;
        [SerializeField] private float fadeTime = 1.0f; // 페이드 연출 시간

        private float currentInactivityTimer;
        private bool isTransitioning;
        public string Step2MainThemeKey { get; set; } = "None";
        public int Step2SubThemeKey { get; set; } = 0;
        public string CartridgeKey { get; set; } = "C";

        public int firstTaggedPlayer = 0;
        public UserType currentUserType = UserType.A;

        public Sprite[] playerColorSprites;

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
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
            if (reporter && reporter.show) reporter.show = false;

            // Why: 초기화 시 화면을 밝히는 FadeIn 수행
            if (FadeManager.Instance) FadeManager.Instance.FadeIn(fadeTime);
        }

        private void ActivateSecondaryDisplay()
        {
            if (Display.displays.Length > 1)
            {
                Display.displays[1].Activate();
            }
        }

        private void Update()
        {
            if (isTransitioning) return;
            HandleInactivity();
        }

        private void HandleInactivity()
        {
            if (SceneManager.GetActiveScene().name == GameConstants.Scene.Title)
            {
                currentInactivityTimer = 0f;
                return;
            }

            if (Input.anyKey || Input.touchCount > 0)
            {
                currentInactivityTimer = 0f;
            }
            else
            {
                currentInactivityTimer += Time.deltaTime;
                if (currentInactivityTimer >= inactivityLimit) ReturnToTitle();
            }
        }

        /// <summary>
        /// 씬 전환을 수행합니다. doFade가 true일 경우 FadeManager를 사용하여 페이드 효과를 적용합니다.
        /// </summary>
        /// <param name="sceneName">이동할 씬 이름</param>
        /// <param name="doFade">페이드 효과 적용 여부 (기본값 false)</param>
        public void ChangeScene(string sceneName, bool doFade = false)
        {
            if (isTransitioning) return;
            
            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[GameManager] ChangeScene 실패: {sceneName}");
                return;
            }
            
            isTransitioning = true;
            StartCoroutine(ChangeSceneRoutine(sceneName, doFade));
        }

        /// <summary>
        /// 페이드 아웃 -> 씬 로드 -> 페이드 인 순서로 전환 로직을 제어합니다.
        /// Why: 매개변수에 따라 연출 여부를 결정하여 유연한 씬 전환을 지원하기 위함입니다.
        /// </summary>
        private IEnumerator ChangeSceneRoutine(string sceneName, bool doFade)
        {
            // 1. Fade Out (선택 사항)
            if (doFade && FadeManager.Instance)
            {
                bool fadeDone = false;
                FadeManager.Instance.FadeOut(fadeTime, () => fadeDone = true);
                yield return new WaitUntil(() => fadeDone);
            }

            // 2. 비동기 씬 로드
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            while (asyncLoad != null && !asyncLoad.isDone)
            {
                yield return null;
            }

            // 3. Fade In (선택 사항)
            if (doFade && FadeManager.Instance)
            {
                yield return new WaitForSeconds(0.2f); // 안정화를 위한 짧은 대기
                bool fadeDone = false;
                FadeManager.Instance.FadeIn(fadeTime, () => fadeDone = true);
                yield return new WaitUntil(() => fadeDone);
            }

            isTransitioning = false;
        }

        public void ReturnToTitle()
        {
            if (isTransitioning) return;
            firstTaggedPlayer = 0; 
            currentInactivityTimer = 0f;
            // 타이틀 복귀 시 페이드를 원한다면 true로 변경하세요.
            ChangeScene(GameConstants.Scene.Title, false); 
        }
    }
}