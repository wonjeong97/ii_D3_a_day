using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;

namespace My.Scripts.UI
{
    public static class UIUtils
    {
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

        public static void ApplyPlayerNames(Text p1Text, Text p2Text, string nameA, string nameB, TextSetting settingA, TextSetting settingB)
        {
            if (p1Text)
            {
                if (settingA != null)
                {
                    if (Wonjeong.UI.UIManager.Instance) Wonjeong.UI.UIManager.Instance.SetText(p1Text.gameObject, settingA);
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
                    if (Wonjeong.UI.UIManager.Instance) Wonjeong.UI.UIManager.Instance.SetText(p2Text.gameObject, settingB);
                    p2Text.text = settingB.text.Replace("{nameB}", nameB);
                }
                else
                {
                    p2Text.text = $"{nameB}님의 위치";
                }
            }
        }

        public static string ReplacePlayerNamePlaceholders(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string nameA = "사용자A";
            string nameB = "사용자B";

            if (Global.SessionManager.Instance)
            {
                if (!string.IsNullOrEmpty(Global.SessionManager.Instance.PlayerAFirstName)) 
                    nameA = Global.SessionManager.Instance.PlayerAFirstName;
                if (!string.IsNullOrEmpty(Global.SessionManager.Instance.PlayerBFirstName)) 
                    nameB = Global.SessionManager.Instance.PlayerBFirstName;
            }
            
            return text.Replace("{nameA}", nameA).Replace("{nameB}", nameB);
        }
    }
}