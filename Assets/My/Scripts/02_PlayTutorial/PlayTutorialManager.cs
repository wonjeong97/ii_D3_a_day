using System;
using System.Collections;
using System.Collections.Generic;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts._02_PlayTutorial.Pages;
using UnityEngine;
using Wonjeong.Utils;

namespace My.Scripts._02_PlayTutorial
{
    [Serializable]
    public class PlayTutorialSetting
    {
        public PlayTutorialPage1Data page1;
        public PlayTutorialPage2Data page2;
        public PlayTutorialPage3Data page3;
    }

    /// <summary>
    /// 싱글톤 기반으로 두 플레이어의 튜토리얼 진행을 관리하는 매니저.
    /// 개별 진행 후 카운트(_finishCount)를 통해 최종 동기화를 처리함.
    /// </summary>
    public class PlayTutorialManager : MonoBehaviour
    {   
        public static PlayTutorialManager Instance { get; private set; }
        
        [Header("Pages")]
        [SerializeField] private List<GamePage> p1Pages = new List<GamePage>();
        [SerializeField] private List<GamePage> p2Pages = new List<GamePage>();

        [Header("Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private int _p1CurrentIndex = -1;
        private int _p2CurrentIndex = -1;
        private bool _isP1Transitioning = false;
        private bool _isP2Transitioning = false;

        private int _finishCount = 0;

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            LoadSettings();

            // 초기화 시 모든 페이지를 비활성화하여 겹침 방지
            foreach (GamePage page in p1Pages)
            {
                if (page) page.gameObject.SetActive(false);
            }
            foreach (GamePage page in p2Pages)
            {
                if (page) page.gameObject.SetActive(false);
            }

            if (p1Pages.Count > 0) TransitionP1ToPage(0);
            if (p2Pages.Count > 0) TransitionP2ToPage(0);
        }

        private void LoadSettings()
        {
            string jsonPath = "JSON/PlayTutorial";
            PlayTutorialSetting setting = JsonLoader.Load<PlayTutorialSetting>(jsonPath);

            if (setting == null)
            {
                Debug.LogError($"[PlayTutorialManager] {jsonPath} 로드 실패.");
                return;
            }

            if (p1Pages.Count > 0 && p1Pages[0]) p1Pages[0].SetupData(setting.page1);
            if (p1Pages.Count > 1 && p1Pages[1]) p1Pages[1].SetupData(setting.page2);
            if (p1Pages.Count > 2 && p1Pages[2]) p1Pages[2].SetupData(setting.page3);

            if (p2Pages.Count > 0 && p2Pages[0]) p2Pages[0].SetupData(setting.page1);
            if (p2Pages.Count > 1 && p2Pages[1]) p2Pages[1].SetupData(setting.page2);
            if (p2Pages.Count > 2 && p2Pages[2]) p2Pages[2].SetupData(setting.page3);
        }

        public void TransitionP1ToPage(int index)
        {
            if (_isP1Transitioning) return;

            // Why: 완료 처리는 PlayTutorialFinished에서 전담하므로, 인덱스를 초과하면 로직만 종료함
            if (index >= p1Pages.Count)
            {
                _p1CurrentIndex = p1Pages.Count;
                return;
            }

            StartCoroutine(PageTransitionRoutine(true, index));
        }

        public void TransitionP2ToPage(int index)
        {
            if (_isP2Transitioning) return;

            if (index >= p2Pages.Count)
            {
                _p2CurrentIndex = p2Pages.Count;
                return;
            }

            StartCoroutine(PageTransitionRoutine(false, index));
        }

        private IEnumerator PageTransitionRoutine(bool isPlayer1, int nextIndex)
        {
            if (isPlayer1) _isP1Transitioning = true;
            else _isP2Transitioning = true;

            List<GamePage> targetList = isPlayer1 ? p1Pages : p2Pages;
            int currentIndex = isPlayer1 ? _p1CurrentIndex : _p2CurrentIndex;

            if (currentIndex >= 0 && currentIndex < targetList.Count)
            {
                GamePage prev = targetList[currentIndex];
                if (prev)
                {
                    yield return StartCoroutine(FadeRoutine(prev, 1f, 0f));
                    prev.OnExit();
                    prev.gameObject.SetActive(false);
                }
            }

            if (isPlayer1) _p1CurrentIndex = nextIndex;
            else _p2CurrentIndex = nextIndex;

            GamePage nextPage = targetList[nextIndex];
            if (nextPage)
            {
                nextPage.gameObject.SetActive(true);
                nextPage.onStepComplete = (trigger) =>
                {
                    if (isPlayer1) TransitionP1ToPage(nextIndex + 1);
                    else TransitionP2ToPage(nextIndex + 1);
                };
                nextPage.OnEnter();
                yield return StartCoroutine(FadeRoutine(nextPage, 0f, 1f));
            }

            if (isPlayer1) _isP1Transitioning = false;
            else _isP2Transitioning = false;
        }

        private IEnumerator FadeRoutine(GamePage page, float start, float end)
        {
            CanvasGroup group = page.GetComponent<CanvasGroup>();
            if (!group) yield break;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(start, end, elapsed / fadeDuration);
                yield return null;
            }

            group.alpha = end;
        }

        /// <summary>
        /// 개별 튜토리얼 종료 신호를 수신하고 카운트를 증가시킴.
        /// </summary>
        public void PlayTutorialFinished()
        {
            _finishCount++;
            CheckAllFinished();
        }

        /// <summary>
        /// 두 플레이어가 모두 튜토리얼을 끝냈는지 확인하고 씬을 전환함.
        /// </summary>
        private void CheckAllFinished()
        {
            // Why: 누가 먼저 끝냈는지 계산할 필요 없이 카운트가 2에 도달하면 즉시 동기화 완료 처리
            if (_finishCount >= 2)
            {
                Debug.Log("<color=cyan>[PlayTutorialManager] 동기화 완료. 메인 게임으로 이동합니다.</color>");
                
                if (GameManager.Instance)
                {
                    //GameManager.Instance.ChangeScene(GameConstants.Scene.MainPlay); // TODO: 해당 씬 상수가 정의되어 있는지 확인
                }
                else
                {
                    Debug.LogError("[PlayTutorialManager] GameManager가 존재하지 않습니다.");
                }
            }
        }
    }
}