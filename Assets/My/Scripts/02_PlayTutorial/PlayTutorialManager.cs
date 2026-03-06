using System;
using System.Collections;
using System.Collections.Generic;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;

namespace My.Scripts._02_Play_Tutorial
{
    /// <summary>
    /// PlayTutorial 씬 전용 JSON 설정 데이터.
    /// </summary>
    [Serializable]
    public class PlayTutorialSetting
    {
        // # TODO: 제이슨 구조 확정 시 각 페이지 데이터 구조체 필드 추가
    }

    /// <summary>
    /// P1과 P2의 튜토리얼 진행 상황을 완전히 독립적으로 제어하는 매니저.
    /// 화면 간 상호 간섭 없이 개별적인 페이지 전환 연출을 수행함.
    /// </summary>
    public class PlayTutorialManager : MonoBehaviour
    {
        [Header("Pages")]
        [SerializeField] private List<GamePage> p1Pages = new List<GamePage>();
        [SerializeField] private List<GamePage> p2Pages = new List<GamePage>();

        [Header("Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private int _p1CurrentIndex = -1;
        private int _p2CurrentIndex = -1;

        private bool _isP1Transitioning = false;
        private bool _isP2Transitioning = false;

        private void Start()
        {
            LoadSettings();
            
            // 씬 진입 시 양쪽 화면 모두 0번 페이지로 페이드인 시작
            TransitionP1ToPage(0);
            TransitionP2ToPage(0);
        }

        /// <summary>
        /// JSON 데이터를 로드하고 각 페이지에 주입함.
        /// </summary>
        private void LoadSettings()
        {
            // # TODO: PlayTutorial 전용 JSON 데이터를 JsonLoader로 읽어와 각 페이지(SetupData)에 주입하는 로직 연동
        }

        /// <summary>
        /// P1 화면을 특정 페이지 인덱스로 전환함.
        /// </summary>
        /// <param name="index">전환할 페이지 인덱스.</param>
        public void TransitionP1ToPage(int index)
        {
            if (_isP1Transitioning)
            {
                return;
            }

            if (index < 0 || index >= p1Pages.Count)
            {
                OnP1Finished();
                return;
            }

            StartCoroutine(PageTransitionRoutine(true, index));
        }

        /// <summary>
        /// P2 화면을 특정 페이지 인덱스로 전환함.
        /// </summary>
        /// <param name="index">전환할 페이지 인덱스.</param>
        public void TransitionP2ToPage(int index)
        {
            if (_isP2Transitioning)
            {
                return;
            }

            if (index < 0 || index >= p2Pages.Count)
            {
                OnP2Finished();
                return;
            }

            StartCoroutine(PageTransitionRoutine(false, index));
        }

        /// <summary>
        /// 단일 화면의 기존 페이지를 끄고 새 페이지를 켜는 비동기 시퀀스.
        /// </summary>
        /// <param name="isPlayer1">P1 화면 여부.</param>
        /// <param name="nextIndex">이동할 대상 인덱스.</param>
        private IEnumerator PageTransitionRoutine(bool isPlayer1, int nextIndex)
        {
            // Why: P1과 P2의 참조 데이터를 분기하여 한쪽의 화면 전환이 다른 쪽에 영향을 주지 않도록 함
            List<GamePage> targetPages = isPlayer1 ? p1Pages : p2Pages;
            int currentIndex = isPlayer1 ? _p1CurrentIndex : _p2CurrentIndex;

            if (isPlayer1) _isP1Transitioning = true;
            else _isP2Transitioning = true;

            // 1. 기존 페이지 페이드 아웃
            if (currentIndex >= 0 && currentIndex < targetPages.Count)
            {
                GamePage prevPage = targetPages[currentIndex];
                if (prevPage)
                {
                    yield return StartCoroutine(FadePageRoutine(prevPage, 1f, 0f));
                    prevPage.OnExit();
                }
            }

            // 인덱스 갱신
            if (isPlayer1) _p1CurrentIndex = nextIndex;
            else _p2CurrentIndex = nextIndex;

            // 2. 새 페이지 진입 및 페이드 인
            GamePage nextPage = targetPages[nextIndex];
            if (nextPage)
            {
                // Why: 페이지 내부에서 완료 이벤트 발생 시 자신의 화면에 맞는 다음 페이지로 넘어가도록 동적 연결
                nextPage.onStepComplete = (trigger) =>
                {
                    if (isPlayer1) TransitionP1ToPage(_p1CurrentIndex + 1);
                    else TransitionP2ToPage(_p2CurrentIndex + 1);
                };
                
                nextPage.OnEnter();
                yield return StartCoroutine(FadePageRoutine(nextPage, 0f, 1f));
            }
            else
            {
                Debug.LogWarning($"[PlayTutorialManager] {(isPlayer1 ? "P1" : "P2")}의 {nextIndex}번째 페이지 객체가 인스펙터에 할당되지 않았습니다.");
            }

            if (isPlayer1) _isP1Transitioning = false;
            else _isP2Transitioning = false;
        }

        /// <summary>
        /// 단일 GamePage 오브젝트의 CanvasGroup 알파값을 목표치로 변경함.
        /// </summary>
        /// <param name="page">대상 페이지 컨트롤러.</param>
        /// <param name="startAlpha">시작 알파.</param>
        /// <param name="endAlpha">목표 알파.</param>
        private IEnumerator FadePageRoutine(GamePage page, float startAlpha, float endAlpha)
        {
            if (!page) yield break;

            // # TODO: 반복적인 GetComponent 호출 방지를 위해 GamePage 클래스 내부에서 CanvasGroup을 public Getter로 제공하도록 리팩토링 검토
            CanvasGroup canvasGroup = page.GetComponent<CanvasGroup>();
            if (!canvasGroup)
            {
                Debug.LogWarning($"[PlayTutorialManager] {page.gameObject.name}에 CanvasGroup이 없습니다.");
                yield break;
            }

            float elapsed = 0f;
            canvasGroup.alpha = startAlpha;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeDuration);
                yield return null;
            }

            canvasGroup.alpha = endAlpha;
        }

        /// <summary>
        /// P1 화면의 모든 튜토리얼 단계가 완료되었을 때 호출됨.
        /// </summary>
        private void OnP1Finished()
        {
            // Why: 각 화면이 언제 종료되었는지 개별적으로 추적하기 위함
            Debug.Log("[PlayTutorialManager] P1 완료");
            CheckAllFinished();
        }

        /// <summary>
        /// P2 화면의 모든 튜토리얼 단계가 완료되었을 때 호출됨.
        /// </summary>
        private void OnP2Finished()
        {
            // Why: 각 화면이 언제 종료되었는지 개별적으로 추적하기 위함
            Debug.Log("[PlayTutorialManager] P2 완료");
            CheckAllFinished();
        }

        /// <summary>
        /// 두 플레이어가 모두 튜토리얼을 마쳤는지 확인하고 메인 게임으로 전환함.
        /// </summary>
        private void CheckAllFinished()
        {
            bool isP1Done = _p1CurrentIndex >= p1Pages.Count;
            bool isP2Done = _p2CurrentIndex >= p2Pages.Count;

            // Why: 한쪽이 일찍 끝나더라도 다른 쪽이 끝날 때까지 대기시켜 흐름을 동기화함
            if (isP1Done && isP2Done)
            {
                Debug.Log("[PlayTutorialManager] 양쪽 모두 튜토리얼 완료. 본 게임 씬으로 이동합니다.");
                
                // # TODO: GameConstants.Scene에 MainPlay 씬 이름 정의 후 아래 주석 해제하여 씬 이동 연동
                /*
                if (GameManager.Instance)
                {
                    GameManager.Instance.ChangeScene(GameConstants.Scene.MainPlay);
                }
                */
            }
        }
    }
}