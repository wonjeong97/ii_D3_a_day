using System;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts.Core
{
    /// <summary> 모든 페이지 컨트롤러의 최상위 부모 </summary>
    public abstract class GamePage : MonoBehaviour
    {
        public Action<int> onStepComplete; 
        protected CanvasGroup canvasGroup; 

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public abstract void SetupData(object data);

        public virtual void OnEnter() 
        { 
            gameObject.SetActive(true);
            SetAlpha(1f);
        }

        public virtual void OnExit() 
        {
            gameObject.SetActive(false);
        }

        public void SetAlpha(float alpha)
        {
            if (canvasGroup) canvasGroup.alpha = alpha;
        }

        protected void CompleteStep(int triggerInfo = 0)
        {
            onStepComplete?.Invoke(triggerInfo);
        }

        /// <summary> 
        /// JSON 데이터의 텍스트, 위치, 크기, 폰트, 정렬, 색상 등을 일괄 적용하는 메서드
        /// </summary>
        protected void SetUIText(Text uiText, TextSetting setting)
        {
            if (uiText == null || setting == null) return;
            UIManager.Instance.SetText(uiText.gameObject, setting);
        }
    }

    /// <summary> 제네릭 데이터 페이지 부모 (타입 안전) </summary>
    public abstract class GamePage<T> : GamePage where T : class
    {
        public sealed override void SetupData(object data)
        {
            if (data is T typedData)
            {
                SetupData(typedData);
            }
            else
            {
                Debug.LogError($"[{GetType().Name}] SetupData 타입 불일치: expected={typeof(T).Name}, actual={data?.GetType().Name ?? "null"}");
            }
        }

        protected abstract void SetupData(T data);
    }
}