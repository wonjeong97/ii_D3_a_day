using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using My.Scripts.Network; // TCP 매니저에 접근하기 위한 네임스페이스 추가

namespace My.Scripts._01_Tutorial.Pages
{   
    /// <summary>
    /// JSON에서 로드되는 튜토리얼 3페이지 데이터 구조체.
    /// </summary>
    [Serializable]
    public class TutorialPage3Data
    {
        public TextSetting descriptionText; // 양쪽 공통 설명
        public TextSetting playerNameA;     // 서버(P1)용 이름
        public TextSetting playerNameB;     // 클라이언트(P2)용 이름
    }
    
    /// <summary>
    /// 세 번째 튜토리얼 페이지 컨트롤러.
    /// 현재 PC의 네트워크 통신 역할(Server/Client)에 따라 이름을 자동으로 분기하여 출력함.
    /// </summary>
    public class TutorialPage3Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private Text descriptionUI;
        [SerializeField] private Text nameUI;

        // Why: TCP 네트워크 상태에 따라 동적으로 이름을 할당하므로 기존 isPlayer1 인스펙터 변수를 삭제함

        private readonly float _autoTransitionDelay = 10.0f;
        private TutorialPage3Data _cachedData;
        private Coroutine _autoTransitionCoroutine;

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

        public override void OnEnter()
        {
            base.OnEnter();

            if (_cachedData == null)
            {
                Debug.LogError("[TutorialPage3Controller] OnEnter: 캐싱된 데이터가 없습니다.");
                return;
            }

            if (descriptionUI) 
            {
                descriptionUI.text = _cachedData.descriptionText != null 
                    ? _cachedData.descriptionText.text 
                    : "설명 데이터가 없습니다.";
            }

            if (nameUI)
            {
                string targetName = "이름 정보 없음";

                // Why: 유니티 객체(싱글톤)의 존재 여부를 암시적으로 확인한 뒤 서버 역할을 판별함
                bool isServer = false;
                if (TcpManager.Instance)
                {
                    isServer = TcpManager.Instance.IsServer;
                }

                // Why: 서버면 A의 이름을, 클라이언트면 B의 이름을 할당함 (일반 C# 객체이므로 명시적 Null 검사 수행)
                if (isServer)
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

            if (_autoTransitionCoroutine != null)
            {
                StopCoroutine(_autoTransitionCoroutine);
            }
            _autoTransitionCoroutine = StartCoroutine(AutoTransitionRoutine());
        }

        public override void OnExit()
        {
            base.OnExit();

            if (_autoTransitionCoroutine != null)
            {
                StopCoroutine(_autoTransitionCoroutine);
                _autoTransitionCoroutine = null;
            }
        }

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