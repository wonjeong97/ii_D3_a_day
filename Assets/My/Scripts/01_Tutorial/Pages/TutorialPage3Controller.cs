using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using My.Scripts.Network;
using Wonjeong.Utils; 
using My.Scripts.Global;

namespace My.Scripts._01_Tutorial.Pages
{   
    [Serializable]
    public class TutorialPage3Data
    {
        public TextSetting descriptionText; 
        public TextSetting playerNameA;     
        public TextSetting playerNameB;     
    }
    
    /// <summary>
    /// 세 번째 튜토리얼 페이지 컨트롤러.
    /// 네트워크 통신 역할(Server/Client)에 따라 세션에 저장된 이름과 색상 스프라이트를 동적으로 치환하여 출력함.
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

            bool isServer = false;
            
            if (TcpManager.Instance)
            {
                isServer = TcpManager.Instance.IsServer;
            }

            if (nameUI)
            {
                string rawText = "이름 정보 없음";

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

                string processedName = rawText;
                
                if (SessionManager.Instance)
                {
                    string nameA = !string.IsNullOrEmpty(SessionManager.Instance.PlayerAFirstName) 
                        ? SessionManager.Instance.PlayerAFirstName 
                        : "사용자A";
                        
                    string nameB = !string.IsNullOrEmpty(SessionManager.Instance.PlayerBFirstName) 
                        ? SessionManager.Instance.PlayerBFirstName 
                        : "사용자B";

                    processedName = rawText.Replace("{nameA}", nameA).Replace("{nameB}", nameB);
                }
                
                nameUI.text = processedName;
            }

            // Why: SessionManager에 등록된 현재 플레이어의 색상값을 읽어와 GameManager의 스프라이트 배열과 매칭하여 이미지를 교체함.
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
                    // 설정된 색상이 없거나 배열 매칭 실패 시 이미지를 숨김 처리
                    playerColorIcon.gameObject.SetActive(false);
                }
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
            yield return CoroutineData.GetWaitForSeconds(_autoTransitionDelay);
            
            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }
    }
}