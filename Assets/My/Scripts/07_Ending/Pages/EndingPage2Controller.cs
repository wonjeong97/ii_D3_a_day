using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.Utils;

namespace My.Scripts._07_Ending.Pages
{
    /// <summary>
    /// 엔딩 페이지 2의 JSON 데이터를 담는 모델.
    /// </summary>
    [Serializable]
    public class EndingPage2Data
    {
        public TextSetting descriptionText1; 
        public TextSetting descriptionText2; 
    }

    /// <summary>
    /// 보상(조각)을 연출과 함께 보여주는 엔딩의 두 번째 페이지 컨트롤러.
    /// Why: 조각 이미지가 순차적으로 페이드 인 된 후, 텍스트가 나타나는 애니메이션을 수행함.
    /// </summary>
    public class EndingPage2Controller : GamePage<EndingPage2Data>
    {
        [Header("UI References")]
        [SerializeField] private Text text1; 
        [SerializeField] private Text text2; 
        [SerializeField] private CanvasGroup imageCanvasGroup;
        [SerializeField] private CanvasGroup textCanvasGroup;
        
        [Header("Piece Animation")]
        [Tooltip("순차적으로 나타날 5개의 조각 이미지를 할당해 주세요.")]
        [SerializeField] private Image[] pieceImages; 
        
        private EndingPage2Data _cachedData; 
        private Coroutine _sequenceCoroutine;

        protected override void SetupData(EndingPage2Data data)
        {
            _cachedData = data;
            
            // 데이터 할당 시 래퍼 메서드를 통해 위치, 폰트 등의 서식 일괄 적용
            if (text1) SetUIText(text1, _cachedData.descriptionText1);
            if (text2) SetUIText(text2, _cachedData.descriptionText2);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            // 텍스트 그룹은 처음에 투명하게 숨김
            if (textCanvasGroup) textCanvasGroup.alpha = 0f;
            
            // 배경 등의 요소를 위해 부모 캔버스 그룹은 켜두고, 내부 조각 이미지들을 투명하게 설정
            if (imageCanvasGroup) imageCanvasGroup.alpha = 1f;
            
            if (pieceImages != null)
            {
                foreach (Image img in pieceImages)
                {
                    SetImageAlpha(img, 0f);
                }
            }
            
            if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = StartCoroutine(SequenceRoutine());
        }

        public override void OnExit()
        {
            base.OnExit();

            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }
        }

        private IEnumerator SequenceRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(0.5f);
            
            // 5개의 조각 이미지를 각각 0.8초 동안 순차적으로 나타내며 사운드 재생
            if (pieceImages != null && pieceImages.Length > 0)
            {
                foreach (Image pieceImg in pieceImages)
                {
                    if (pieceImg)
                    {
                        // # TODO: 이 프로젝트의 사운드 매니저 구현 방식에 맞게 주석 해제 및 수정
                        // SoundManager.Instance?.PlaySFX("공통_6"); 
                        yield return StartCoroutine(FadeImage(pieceImg, 0f, 1f, 0.8f));
                    }
                }
            }
            else
            {
                // 배열에 이미지가 등록되지 않았을 때를 대비한 안전 장치(기존처럼 사운드 1회 재생)
                // SoundManager.Instance?.PlaySFX("공통_6");
            }
            
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            
            // 텍스트 그룹 등장
            yield return StartCoroutine(FadeCanvasGroup(textCanvasGroup, 0f, 1f, 0.5f));
            yield return CoroutineData.GetWaitForSeconds(3.0f);

            CompleteStep();
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float s, float e, float d)
        {
            if (!cg) yield break;
            float time = 0f;
            cg.alpha = s;
            
            while(time < d) 
            { 
                time += Time.deltaTime; 
                cg.alpha = Mathf.Lerp(s, e, time/d); 
                yield return null; 
            }
            cg.alpha = e;
        }

        /// <summary> 개별 이미지(Image)의 투명도를 선형 보간하여 시각적 전환 수행 </summary>
        private IEnumerator FadeImage(Image img, float s, float e, float d)
        {
            if (!img) yield break;
            float time = 0f;
            SetImageAlpha(img, s);
            
            while(time < d) 
            { 
                time += Time.deltaTime; 
                SetImageAlpha(img, Mathf.Lerp(s, e, time/d)); 
                yield return null; 
            }
            SetImageAlpha(img, e);
        }

        /// <summary> 이미지의 투명도 직접 갱신 </summary>
        private void SetImageAlpha(Image img, float a)
        {
            if (img)
            {
                Color c = img.color;
                c.a = a;
                img.color = c;
            }
        }
    }
}