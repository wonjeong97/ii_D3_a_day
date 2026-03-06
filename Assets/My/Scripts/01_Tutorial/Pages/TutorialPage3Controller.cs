using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;

namespace My.Scripts._01_Tutorial.Pages
{   
    /// <summary>
    /// JSON에서 로드되는 튜토리얼 3페이지 데이터 구조체.
    /// </summary>
    [Serializable]
    public class TutorialPage3Data
    {
        public TextSetting descriptionText; // 양쪽 공통 설명
        public TextSetting playerNameA;     // P1(Display 1)용 이름
        public TextSetting playerNameB;     // P2(Display 2)용 이름
    }
    
    /// <summary>
    /// 세 번째 튜토리얼 페이지 컨트롤러.
    /// 공통 설명과 P1/P2 개별 데이터를 출력하며, 데이터 누락 시 에러 로그를 남기고 안전하게 처리함.
    /// </summary>
    public class TutorialPage3Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private Text descriptionUI;
        [SerializeField] private Text nameUI;

        [Header("Display Settings")]
        [Tooltip("P1용 프리팹이면 체크, P2용이면 체크 해제")]
        [SerializeField] private bool isPlayer1;

        
        private readonly float _autoTransitionDelay = 10.0f;
        private TutorialPage3Data _cachedData;
        private Coroutine _autoTransitionCoroutine;

        /// <summary>
        /// TutorialManager로부터 전달받은 데이터를 캐싱함.
        /// </summary>
        /// <param name="data">TutorialPage3Data 타입의 데이터 객체.</param>
        public override void SetupData(object data)
        {
            TutorialPage3Data pageData = data as TutorialPage3Data;
            
            if (pageData != null)
            {
                _cachedData = pageData;
            }
            else
            {
                _cachedData = null;
                Debug.LogError("[TutorialPage3Controller] SetupData: 전달된 데이터가 TutorialPage3Data 형식이 아닙니다.");
            }
        }

        /// <summary>
        /// 페이지 진입 시 UI를 갱신하고 자동 전환 타이머를 시작함.
        /// 데이터 내부 필드가 null일 경우를 대비해 안전한 접근 방식을 사용함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            if (_cachedData == null)
            {
                Debug.LogError("[TutorialPage3Controller] OnEnter: 캐싱된 데이터가 없습니다.");
                return;
            }

            // 1. 공통 설명 문구 적용 (객체 유효성 확인 후 텍스트 접근)
            if (descriptionUI) 
            {
                // Why: JSON 파일에 descriptionText 키가 누락되었을 경우 NullReference 방지
                descriptionUI.text = _cachedData.descriptionText != null 
                    ? _cachedData.descriptionText.text 
                    : "설명 데이터가 없습니다.";
            }

            // 2. 인스펙터 설정(isPlayer1)에 따라 A 또는 B의 이름을 선택적 적용
            if (nameUI)
            {
                // Why: P1/P2 각각 다른 필드를 참조하되, 해당 필드 자체가 null인지 검사함
                string targetName = "이름 정보 없음";

                if (isPlayer1)
                {
                    if (_cachedData.playerNameA != null)
                    {
                        targetName = _cachedData.playerNameA.text;
                    }
                }
                else
                {
                    if (_cachedData.playerNameB != null)
                    {
                        targetName = _cachedData.playerNameB.text;
                    }
                }

                nameUI.text = targetName;
            }

            // 3초 후 자동으로 다음 페이지로 넘어가기 위한 코루틴 실행
            if (_autoTransitionCoroutine != null)
            {
                StopCoroutine(_autoTransitionCoroutine);
            }
            _autoTransitionCoroutine = StartCoroutine(AutoTransitionRoutine());
        }

        /// <summary>
        /// 페이지 퇴장 시 실행 중인 자동 전환 코루틴을 안전하게 중단함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();

            if (_autoTransitionCoroutine != null)
            {
                StopCoroutine(_autoTransitionCoroutine);
                _autoTransitionCoroutine = null;
            }
        }

        /// <summary>
        /// 설정된 시간(3초) 대기 후 단계 완료 이벤트를 호출함.
        /// </summary>
        private IEnumerator AutoTransitionRoutine()
        {
            yield return new WaitForSeconds(_autoTransitionDelay);
            
            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }
    }
}