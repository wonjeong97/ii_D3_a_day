using System.Collections;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts.UI
{
    /// <summary>
    /// UI 연출 및 텍스트 치환을 보조하는 정적 유틸리티 클래스.
    /// 공통적으로 사용되는 페이드 효과와 플레이어 이름 치환 로직을 일괄 관리하기 위함.
    /// </summary>
    public static class UIUtils
    {
        /// <summary>
        /// 캔버스 그룹의 알파값을 조절하여 페이드 인/아웃 연출을 수행함.
        /// 연출 종료 후 알파값이 0일 경우 오브젝트를 비활성화하여 드로우콜을 최적화함.
        /// </summary>
        /// <param name="cg">대상 CanvasGroup 컴포넌트.</param>
        /// <param name="start">시작 알파값.</param>
        /// <param name="end">목표 알파값.</param>
        /// <param name="duration">변화 지속 시간.</param>
        public static IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float duration)
        {
            if (!cg) yield break;

            if (duration <= 0f)
            {
                cg.alpha = end;
                if (end <= 0f) cg.gameObject.SetActive(false);
                yield break;
            }

            float t = 0f;
            cg.alpha = start;
            while (t < duration)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }
            cg.alpha = end;
            if (end <= 0f) cg.gameObject.SetActive(false);
        }

        /// <summary>
        /// 두 플레이어의 이름과 서식 설정을 UI 텍스트에 적용함.
        /// 런타임에 결정되는 사용자 이름을 JSON 설정 내 플레이스홀더에 주입하기 위함.
        /// </summary>
        /// <param name="p1Text">플레이어 1 표시용 텍스트.</param>
        /// <param name="p2Text">플레이어 2 표시용 텍스트.</param>
        /// <param name="nameA">플레이어 A의 실제 이름.</param>
        /// <param name="nameB">플레이어 B의 실제 이름.</param>
        /// <param name="settingA">플레이어 A 텍스트 서식 데이터.</param>
        /// <param name="settingB">플레이어 B 텍스트 서식 데이터.</param>
        public static void ApplyPlayerNames(Text p1Text, Text p2Text, string nameA, string nameB, TextSetting settingA, TextSetting settingB)
        {
            if (p1Text)
            {
                if (settingA != null)
                {
                    if (UIManager.Instance) UIManager.Instance.SetText(p1Text.gameObject, settingA);
                    p1Text.text = settingA.text.Replace("{nameA}", nameA);
                }
                else
                {
                    p1Text.text = $"{nameA}님의 위치";
                }
            }

            if (p2Text)
            {
                if (settingB != null)
                {
                    if (UIManager.Instance) UIManager.Instance.SetText(p2Text.gameObject, settingB);
                    p2Text.text = settingB.text.Replace("{nameB}", nameB);
                }
                else
                {
                    p2Text.text = $"{nameB}님의 위치";
                }
            }
        }

        /// <summary>
        /// 문자열 내 이름 치환자({nameA}, {nameB})를 세션 데이터 기반으로 실제 이름으로 변경함.
        /// 서버에서 수신한 유저 정보를 UI에 실시간 반영하기 위함.
        /// </summary>
        /// <param name="text">치환자가 포함된 원본 문자열.</param>
        /// <returns>치환 완료된 문자열.</returns>
        public static string ReplacePlayerNamePlaceholders(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            string nameA = "사용자A";
            string nameB = "사용자B";

            if (SessionManager.Instance)
            {
                if (!string.IsNullOrEmpty(SessionManager.Instance.PlayerAFirstName)) 
                    nameA = SessionManager.Instance.PlayerAFirstName;
                if (!string.IsNullOrEmpty(SessionManager.Instance.PlayerBFirstName)) 
                    nameB = SessionManager.Instance.PlayerBFirstName;
            }
            
            return text.Replace("{nameA}", nameA).Replace("{nameB}", nameB);
        }
    }
}