using System;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts.Core
{
    /// <summary>
    /// 모든 페이지 컨트롤러의 최상위 부모 클래스.
    /// 페이지의 생명주기 관리와 공통 UI 설정 기능을 제공함.
    /// </summary>
    public abstract class GamePage : MonoBehaviour
    {
        public Action<int> onStepComplete; 
        protected CanvasGroup canvasGroup; 

        /// <summary>
        /// 해당 페이지의 비동기 로드(이미지 프리로드 등)가 완료되어 렌더링 준비가 되었는지 여부.
        /// BaseFlowManager가 페이드 인 연출을 시작하기 전에 이 값을 검사하여 깜빡임을 방지함.
        /// </summary>
        public virtual bool IsReady 
        { 
            get { return true; } 
        }

        /// <summary>
        /// 컴포넌트 초기화 시 CanvasGroup을 확보함.
        /// 알파값 제어를 통한 페이드 연출을 보장하기 위함.
        /// </summary>
        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (!canvasGroup) 
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        /// <summary>
        /// 외부로부터 전달받은 객체 데이터를 페이지 형식에 맞춰 할당함.
        /// </summary>
        /// <param name="data">매핑할 데이터 객체.</param>
        public abstract void SetupData(object data);

        /// <summary>
        /// 페이지 활성화 시 호출됨.
        /// 오브젝트를 가시화하고 초기 알파값을 설정함.
        /// </summary>
        public virtual void OnEnter() 
        { 
            Transform current = transform;
            while (current)
            {
                if (!current.gameObject.activeSelf)
                {
                    current.gameObject.SetActive(true);
                }
                current = current.parent;
            }
            
            SetAlpha(1f);
        }

        /// <summary>
        /// 페이지 비활성화 시 호출됨.
        /// </summary>
        public virtual void OnExit() 
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// CanvasGroup의 투명도를 조절함.
        /// </summary>
        /// <param name="alpha">설정할 알파값.</param>
        public void SetAlpha(float alpha)
        {
            if (canvasGroup) 
            {
                canvasGroup.alpha = alpha;
            }
        }

        /// <summary>
        /// 현재 페이지의 작업 완료를 매니저에 알림.
        /// </summary>
        /// <param name="triggerInfo">완료 시 전달할 추가 정보 값.</param>
        protected void CompleteStep(int triggerInfo = 0)
        {
            if (onStepComplete != null) 
            {
                onStepComplete.Invoke(triggerInfo);
            }
        }

        /// <summary> 
        /// JSON 설정 데이터를 기반으로 UI 텍스트의 서식과 내용을 일괄 적용함.
        /// 폰트, 크기, 색상, 정렬 등 시각적 일관성을 유지하기 위함.
        /// </summary>
        /// <param name="uiText">대상 UI 텍스트 컴포넌트.</param>
        /// <param name="setting">적용할 텍스트 설정 데이터.</param>
        protected void SetUIText(Text uiText, TextSetting setting)
        {
            if (!uiText || setting == null) return;
            
            if (UIManager.Instance) 
            {
                UIManager.Instance.SetText(uiText.gameObject, setting);
            }
        }
    }

    /// <summary>
    /// 특정 데이터 타입을 사용하는 제네릭 페이지 부모 클래스.
    /// 잘못된 데이터 모델 주입을 방지하고 타입 안정성을 확보하기 위함.
    /// </summary>
    /// <typeparam name="T">해당 페이지에서 사용하는 데이터 모델 타입.</typeparam>
    public abstract class GamePage<T> : GamePage where T : class
    {
        /// <summary>
        /// 전달받은 데이터를 제네릭 타입으로 캐스팅하여 하위 클래스에 전달함.
        /// 데이터 모델 불일치 시 에러 로그를 남겨 디버깅 편의성을 높임.
        /// </summary>
        /// <param name="data">입력 데이터 객체.</param>
        public sealed override void SetupData(object data)
        {
            T typedData = data as T;
            if (typedData != null)
            {
                SetupData(typedData);
            }
            else
            {
                string actualType = data != null ? data.GetType().Name : "null";
                Debug.LogError($"[{GetType().Name}] SetupData 타입 불일치: expected={typeof(T).Name}, actual={actualType}");
            }
        }

        /// <summary>
        /// 하위 클래스에서 타입이 확정된 데이터를 처리하도록 구현함.
        /// </summary>
        /// <param name="data">타입이 일치하는 데이터 객체.</param>
        protected abstract void SetupData(T data);
    }
}