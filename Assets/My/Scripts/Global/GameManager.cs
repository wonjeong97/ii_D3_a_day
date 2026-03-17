using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wonjeong.Reporter;

namespace My.Scripts.Global
{ 
    /// <summary>
    /// 사용자 유형을 정의하는 열거형.
    /// </summary>
    public enum UserType
    {
        A, // 커플 표준형
        B, // 친구
        C, // 동료
        D, // 부모-성인자녀
        E, // 부모-사춘기자녀 (추후)
        F  // 부부사이 (추후)
    }

    /// <summary>
    /// 게임의 전반적인 상태, 씬 전환 및 멀티 디스플레이를 관리하는 매니저 클래스.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        [SerializeField] private Reporter reporter;

        [Header("Settings")]
        [SerializeField] private float inactivityLimit = 60f;
        [SerializeField] private float fadeTime = 1.0f;

        private float currentInactivityTimer;
        private bool isTransitioning;

        // 플레이어 상태 정보
        public int firstTaggedPlayer = 0;
        public UserType currentUserType = UserType.A;

        [Header("Player Color Sprites")]
        [Tooltip("0:Cyan, 1:Pink, 2:Orange, 3:Green, 4:Red, 5:Yellow")]
        public Sprite[] playerColorSprites;

        /// <summary>
        /// 싱글톤을 초기화하고 독립된 두 번째 디스플레이를 활성화함.
        /// </summary>
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
            // 키오스크 환경을 위해 커서 숨김
            Cursor.visible = false;
            Application.runInBackground = true;
            if (reporter && reporter.show) reporter.show = false;
        }

        /// <summary>
        /// OS에서 인식된 두 번째 모니터가 있을 경우 이를 활성화함.
        /// </summary>
        private void ActivateSecondaryDisplay()
        {
            // Unity는 기본적으로 첫 번째 디스플레이만 활성화하므로 수동 활성화가 필요함
            if (Display.displays.Length > 1)
            {
                Display.displays[1].Activate();
                Debug.Log("GameManager: Secondary display (Display 2) activated.");
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.D) && reporter)
            {
                reporter.showGameManagerControl = !reporter.showGameManagerControl;
                if (reporter.show) reporter.show = false;
            }
            else if (Input.GetKeyDown(KeyCode.M)) Cursor.visible = !Cursor.visible;

            if (isTransitioning)
            {
                return;
            }

            HandleInactivity();
        }

        /// <summary>
        /// 타이틀 씬이 아닐 때 사용자 입력이 없으면 타이틀로 복귀시킴.
        /// </summary>
        private void HandleInactivity()
        {
            // 타이틀 화면에서는 자동 복귀 로직을 수행하지 않음
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
                if (currentInactivityTimer >= inactivityLimit)
                {
                    ReturnToTitle();
                }
            }
        }

        /// <summary>
        /// 지정된 씬으로 전환을 시작함.
        /// </summary>
        /// <param name="sceneName">이동할 씬의 이름.</param>
        public void ChangeScene(string sceneName)
        {
            if (isTransitioning)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[GameManager] ChangeScene 실패: 유효하지 않은 씬 이름 '{sceneName}'");
                return;
            }
            
            isTransitioning = true;
            StartCoroutine(ChangeSceneRoutine(sceneName));
        }

        /// <summary>
        /// 페이드 연출을 포함하여 비동기적으로 씬을 로드함.
        /// </summary>
        private IEnumerator ChangeSceneRoutine(string sceneName)
        {
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            while (asyncLoad != null && !asyncLoad.isDone)
            {
                yield return null;
            }

            isTransitioning = false;
        }

        /// <summary>
        /// 게임 상태를 초기화하고 타이틀 화면으로 돌아감.
        /// </summary>
        public void ReturnToTitle()
        {
            if (isTransitioning)
            {
                return;
            }
            
            firstTaggedPlayer = 0; 
            currentInactivityTimer = 0f;

            ChangeScene(GameConstants.Scene.Title);
        }
    }
}