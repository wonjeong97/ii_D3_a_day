using System;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;

namespace My.Scripts._01_Tutorial.Pages
{   
    /// <summary>
    /// JSON에서 로드되는 튜토리얼 1페이지 데이터 구조체.
    /// </summary>
    [Serializable]
    public class TutorialPage1Data
    {
        public TextSetting descriptionText; // JSON의 descriptionText 필드와 매핑
    }
    
    /// <summary>
    /// 첫 번째 튜토리얼 페이지를 제어하는 컨트롤러.
    /// 하나의 텍스트 컴포넌트를 관리하며 엔터 키 입력을 통해 다음 단계로 진행함.
    /// </summary>
    public class TutorialPage1Controller : GamePage
    {
        [Header("UI Components")]
        [SerializeField] 
        private Text descriptionText;

        private string cachedMessage = string.Empty;
        private bool isPageActive;

        /// <summary>
        /// 매 프레임 엔터 키 입력을 확인하여 페이지 완료 여부를 결정함.
        /// </summary>
        private void Update()
        {   
            if (!isPageActive) return;
            
            // 사용자가 엔터(일반/키패드)를 눌러 확인했는지 감지
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OnConfirmInput();
            }
        }

        /// <summary>
        /// TutorialManager로부터 전달받은 JSON 데이터를 텍스트 변수에 저장함.
        /// </summary>
        /// <param name="data">TutorialPage1Data 타입의 데이터 객체.</param>
        public override void SetupData(object data)
        {
            TutorialPage1Data pageData = data as TutorialPage1Data;
            
            if (pageData != null && pageData.descriptionText != null)
            {
                cachedMessage = pageData.descriptionText.text;
            }
            else
            {
                // 데이터 로드 실패 시 디버깅을 위해 로그를 남김
                Debug.LogError("[TutorialPage1Controller] 전달된 데이터가 비어있거나 형식이 잘못되었습니다.");
            }
        }

        /// <summary>
        /// 페이지가 활성화될 때 캐싱된 메시지를 텍스트 컴포넌트에 적용함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            isPageActive = true;
            if (descriptionText)
            {
                // 데이터가 없을 경우를 대비해 기본 문구를 할당함
                descriptionText.text = !string.IsNullOrEmpty(cachedMessage) 
                    ? cachedMessage 
                    : "엔터 키를 눌러 진행하세요.";
            }
        }

        /// <summary>
        /// 확인 입력 발생 시 상위 매니저에게 현재 페이지 세트가 완료되었음을 알림.
        /// </summary>
        private void OnConfirmInput()
        {
            if (onStepComplete != null)
            {
                onStepComplete.Invoke(0);
            }
        }

        public override void OnExit()
        {
            base.OnExit();
            isPageActive = false;
        }
    }
}