using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Network;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._07_Ending.Pages
{
    /// <summary>
    /// JSON에서 로드되는 엔딩 페이지 2의 데이터 구조체.
    /// </summary>
    [Serializable]
    public class EndingPage2Data
    {
        public TextSetting descriptionText1; 
        public TextSetting descriptionText2; 
    }

    /// <summary>
    /// 최종 보상인 조각 획득을 시각적으로 연출하는 엔딩의 두 번째 페이지 컨트롤러.
    /// 조각 이미지가 순차적으로 나타난 뒤 최종 획득 개수 텍스트를 노출함.
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

        /// <summary>
        /// 전달받은 데이터를 캐싱하고 획득한 총 조각 수를 계산하여 텍스트를 치환함.
        /// 기존 누적 조각 수에 이번 모듈 보상인 5개를 더해 실시간으로 결과를 반영하기 위함.
        /// </summary>
        /// <param name="data">EndingPage2Data 타입의 데이터 객체.</param>
        protected override void SetupData(EndingPage2Data data)
        {
            _cachedData = data;
            
            if (text1) 
            {
                SetUIText(text1, _cachedData.descriptionText1);
            }
            
            if (text2) 
            {
                SetUIText(text2, _cachedData.descriptionText2);
                
                int currentTotal = 0;
                if (SessionManager.Instance)
                {
                    currentTotal = SessionManager.Instance.TotalPieces;
                }
                
                int finalPieceCount = currentTotal + 5;
                
                if (_cachedData.descriptionText2 != null && !string.IsNullOrEmpty(_cachedData.descriptionText2.text))
                {
                    text2.text = _cachedData.descriptionText2.text.Replace("{0}", finalPieceCount.ToString());
                }
            }
        }

        /// <summary>
        /// 페이지 진입 시 연출 요소들을 초기화하고 시퀀스를 시작함.
        /// 연출 전 모든 UI의 투명도를 시작 상태로 리셋하여 시각적 오류를 방지함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            if (textCanvasGroup) textCanvasGroup.alpha = 0f;
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

        /// <summary>
        /// 페이지 이탈 시 코루틴을 중단하고 서버 PC에서 세션 종료 API를 호출함.
        /// DB 트랜잭션 경합 방지를 위해 한 대의 기기에서만 전담하여 세션 시간을 업데이트함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();

            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }

            if (GameManager.Instance)
            {
                if (TcpManager.Instance && TcpManager.Instance.IsServer)
                {
                    GameManager.Instance.SendTimeUpdateAPI();
                }
            }
        }

        /// <summary>
        /// 조각 이미지를 하나씩 순차적으로 노출한 후 설명 텍스트를 페이드 인 시킴.
        /// 유저에게 보상 획득에 대한 몰입감 있는 피드백을 제공하기 위함.
        /// </summary>
        private IEnumerator SequenceRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(0.5f);
            
            if (pieceImages != null && pieceImages.Length > 0)
            {
                foreach (Image pieceImg in pieceImages)
                {
                    if (pieceImg)
                    {
                        if (SoundManager.Instance)
                        {
                            SoundManager.Instance.PlaySFX("공통_6");    
                        }
                        yield return StartCoroutine(FadeImage(pieceImg, 0f, 1f, 0.8f));
                    }
                }
            }
            
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            
            yield return StartCoroutine(FadeCanvasGroup(textCanvasGroup, 0f, 1f, 0.5f));
            yield return CoroutineData.GetWaitForSeconds(3.0f);

            CompleteStep();
        }

        /// <summary>
        /// 캔버스 그룹의 알파값을 선형 보간하여 페이드 연출을 수행함.
        /// </summary>
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

        /// <summary>
        /// 개별 이미지 컴포넌트의 알파값을 선형 보간하여 시각적 전환을 수행함.
        /// </summary>
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

        /// <summary>
        /// 이미지의 알파 채널 값을 직접 갱신함.
        /// </summary>
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