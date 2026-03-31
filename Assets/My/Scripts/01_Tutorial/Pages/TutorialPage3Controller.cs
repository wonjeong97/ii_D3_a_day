using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using My.Scripts.Network;
using My.Scripts.Global;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial.Pages
{   
    /// <summary>
    /// JSON에서 로드되는 튜토리얼 3페이지 데이터 구조체.
    /// </summary>
    [Serializable]
    public class TutorialPage3Data
    {
        public TextSetting descriptionText; 
        public TextSetting playerNameA;     
        public TextSetting playerNameB;     
    }
    
    /// <summary>
    /// 세 번째 튜토리얼 페이지 컨트롤러.
    /// 네트워크 통신 역할에 따라 이름과 색상 스프라이트를 동적으로 치환하여 렌더링함.
    /// </summary>
    public class TutorialPage3Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] private Text descriptionUI;
        [SerializeField] private Text nameUI;

        [Header("Player Color")]
        [Tooltip("유저의 색상 스프라이트(컬러 볼)를 표시할 이미지 컴포넌트")]
        [SerializeField] private Image playerColorIcon;

        private readonly float _autoTransitionDelay = 10.0f;
        private TutorialPage3Data _cachedData;
        private Coroutine _autoTransitionCoroutine;

        /// <summary>
        /// 매니저로부터 전달받은 페이지 데이터를 메모리에 캐싱함.
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
        /// 페이지 진입 시 이름 텍스트와 색상 이미지를 설정하고 전환 타이머를 시작함.
        /// </summary>
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

            bool isServer = false;
            
            if (TcpManager.Instance)
            {
                isServer = TcpManager.Instance.IsServer;
            }

            if (nameUI)
            {
                string rawText = "이름 정보 없음";

                // 현재 PC의 네트워크 역할에 맞게 노출할 텍스트 템플릿을 분기함.
                if (isServer)
                {
                    if (_cachedData.playerNameA != null)
                    {
                        rawText = _cachedData.playerNameA.text;
                    }
                }
                else
                {
                    if (_cachedData.playerNameB != null)
                    {
                        rawText = _cachedData.playerNameB.text;
                    }
                }

                nameUI.text = UI.UIUtils.ReplacePlayerNamePlaceholders(rawText);
            }

            // 세션에 기록된 현재 플레이어의 색상값을 참조해 전역 매니저의 스프라이트 풀에서 적절한 이미지를 렌더링함.
            if (playerColorIcon && SessionManager.Instance && GameManager.Instance)
            {
                ColorData myColor = isServer ? SessionManager.Instance.PlayerAColor : SessionManager.Instance.PlayerBColor;
                Sprite colorSprite = GameManager.Instance.GetColorSprite(myColor);

                if (colorSprite)
                {
                    playerColorIcon.sprite = colorSprite;
                    playerColorIcon.gameObject.SetActive(true);
                }
                else
                {
                    playerColorIcon.gameObject.SetActive(false);
                }
            }

            if (_autoTransitionCoroutine != null)
            {
                StopCoroutine(_autoTransitionCoroutine);
            }
            _autoTransitionCoroutine = StartCoroutine(AutoTransitionRoutine());
        }

        /// <summary>
        /// 페이지 이탈 시 실행 중인 타이머 코루틴을 중단하여 누수를 방지함.
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
        /// 설정된 대기 시간 경과 후 다음 페이지로 자동 전환 이벤트를 호출함.
        /// </summary>
        private IEnumerator AutoTransitionRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(_autoTransitionDelay);
            
            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }
    }
}