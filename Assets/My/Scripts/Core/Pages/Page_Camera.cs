using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Network;
using UnityEngine;
using UnityEngine.SceneManagement; 
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 웹캠 제어 및 사진 저장/합성을 담당하는 페이지 컨트롤러.
    /// 활성화된 씬(Step2, Step3)에 따라 웹캠 하드웨어 가동과 이미지 합성 로직을 명확히 분기함.
    /// </summary>
    public class Page_Camera : GamePage
    {
        [Header("Canvas Groups")]
        [SerializeField] private CanvasGroup textAnswerCompleteCg;
        [SerializeField] private CanvasGroup textMySceneCg;
        [SerializeField] private CanvasGroup imageCg;
        [SerializeField] private CanvasGroup errorCg;

        [Header("Dynamic UI Components")]
        [SerializeField] private Text textAnswerCompleteUI;
        [SerializeField] private Text textMySceneUI;

        [Header("Save Settings")]
        [SerializeField] private string questionId;

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private CommonResultPageData _cachedData; 
        private bool _isCompleted;
        private Coroutine _sequenceCoroutine;
        private int _selectedAnswerIndex;

        private const int CamWidth = 1920;
        private const int CamHeight = 1080;
        private const int SaveWidth = 960;
        private const int SaveHeight = 1080;
        
        /// <summary>
        /// 동기화 명령어 설정.
        /// 매니저 클래스(Step1~3Manager) 호출 호환성을 위해 인터페이스를 제공함.
        /// </summary>
        /// <param name="command">동기화 명령어 문자열.</param>
        public void SetSyncCommand(string command) { }

        /// <summary>
        /// 합성 시 필요한 답변 인덱스를 외부 매니저로부터 전달받음.
        /// </summary>
        /// <param name="index">1~5 사이의 답변 번호.</param>
        public void SetAnswerIndex(int index)
        {
            _selectedAnswerIndex = index;
        }

        /// <summary>
        /// 외부에서 문항 식별 코드를 설정함.
        /// 촬영 시 파일명 생성을 위한 구분자로 사용하기 위함.
        /// </summary>
        /// <param name="id">문항 식별자 문자열.</param>
        public void SetQuestionId(string id)
        {
            questionId = id;
        }

        /// <summary>
        /// 외부로부터 전달받은 결과 페이지 데이터를 메모리에 캐싱함.
        /// </summary>
        /// <param name="data">CommonResultPageData 타입의 데이터 객체.</param>
        public override void SetupData(object data)
        {
            CommonResultPageData pageData = data as CommonResultPageData;
            if (pageData != null) _cachedData = pageData;
        }

        /// <summary>
        /// 페이지 진입 시 연출을 초기화하고 Step2인 경우에만 카메라를 가동함.
        /// 저장이 필요 없는 구간(Step1)에서 불필요한 하드웨어 호출을 완벽히 차단하기 위함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            if (textAnswerCompleteCg) textAnswerCompleteCg.alpha = 0f;
            if (textMySceneCg) textMySceneCg.alpha = 0f;
            if (imageCg) imageCg.alpha = 0f;
            if (errorCg) errorCg.alpha = 0f;

            if (_cachedData == null)
            {
                if (errorCg) errorCg.alpha = 1f;
                return;
            }

            ApplyDataToUI();

            bool isStep2 = SceneManager.GetActiveScene().name == GameConstants.Scene.Step2;

            if (isStep2 && CameraManager.Instance)
            {
                CameraManager.Instance.StartCamera();
            }

            if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = StartCoroutine(SequenceRoutine());
        }
        
        /// <summary>
        /// 페이지 이탈 시 실행 중인 코루틴을 중단하고 카메라를 정지함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }
            
            if (CameraManager.Instance) CameraManager.Instance.StopCamera();
        }
        
        /// <summary>
        /// 캐싱된 데이터를 UI 텍스트 컴포넌트에 적용함.
        /// </summary>
        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;
            SetUIText(textAnswerCompleteUI, _cachedData.textAnswerComplete);
            SetUIText(textMySceneUI, _cachedData.textMyScene);
        }

        /// <summary>
        /// 답변 완료 안내, 촬영/합성 대기, 저장 연출을 순차적으로 수행함.
        /// 씬 이름(Step2, Step3)을 기준으로 캡처와 합성 로직을 명확하게 분기 처리하기 위함.
        /// </summary>
        private IEnumerator SequenceRoutine()
        {
            if (textAnswerCompleteCg) yield return StartCoroutine(FadeCanvasGroupRoutine(textAnswerCompleteCg, 0f, 1f, fadeDuration));
            if (imageCg) yield return StartCoroutine(FadeCanvasGroupRoutine(imageCg, 0f, 1f, fadeDuration));
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("레고_4");
            yield return CoroutineData.GetWaitForSeconds(2.5f);
            
            if (textMySceneCg) yield return StartCoroutine(FadeCanvasGroupRoutine(textMySceneCg, 0f, 1f, fadeDuration));
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            bool isSuccess = true;
            string currentScene = SceneManager.GetActiveScene().name;
            bool isStep2 = currentScene == GameConstants.Scene.Step2;
            bool isStep3 = currentScene == GameConstants.Scene.Step3;

            if (isStep3)
            {
                My.Scripts.Utils.PhotoCompositor compositor = FindFirstObjectByType<My.Scripts.Utils.PhotoCompositor>();
                if (compositor)
                {
                    compositor.ProcessAndSave(_selectedAnswerIndex, false);
                    while (compositor.IsProcessing) yield return null;
                }
                else
                {
                    isSuccess = false;
                }
            }
            else if (isStep2)
            {
                isSuccess = false;
                yield return UniTask.ToCoroutine(async () => {
                    isSuccess = await CapturePhotoAsync();
                });
            }

            if (!isSuccess)
            {
                if (errorCg) yield return StartCoroutine(FadeCanvasGroupRoutine(errorCg, 0f, 1f, fadeDuration));
                yield break;
            }
            
            yield return CoroutineData.GetWaitForSeconds(0.5f);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                if (textAnswerCompleteCg) textAnswerCompleteCg.alpha = Mathf.Lerp(1f, 0f, t);
                if (textMySceneCg) textMySceneCg.alpha = Mathf.Lerp(1f, 0f, t);
                if (imageCg) imageCg.alpha = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }

            // Step2(촬영)와 Step3(합성)에서 실제 저장 로직을 거쳤을 때만 안내 텍스트를 노출함.
            if (isStep2 || isStep3)
            {
                SetUIText(textAnswerCompleteUI, _cachedData.textPhotoSaved);
                if (textAnswerCompleteCg) yield return StartCoroutine(FadeCanvasGroupRoutine(textAnswerCompleteCg, 0f, 1f, fadeDuration));
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_12");
                yield return CoroutineData.GetWaitForSeconds(1.5f);
            }

            if (!_isCompleted) CompletePage();
        }

        /// <summary>
        /// 공유 웹캠 텍스처를 이용하여 실제 사진을 캡처하고 저장을 요청함.
        /// </summary>
        private async UniTask<bool> CapturePhotoAsync()
        {
            if (!CameraManager.Instance) return false;
            
            WebCamTexture cam = CameraManager.Instance.GetWebCamTexture();
            if (!cam || !cam.isPlaying) return false;

            RenderTexture rt = null;
            try
            {
                rt = RenderTexture.GetTemporary(CamWidth, CamHeight, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(cam, rt);
                
                Texture2D photo = CameraManager.Instance.GetSharedCapturedPhoto();
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                
                int startX = (CamWidth - SaveWidth) / 2;
                int startY = (CamHeight - SaveHeight) / 2;
                
                photo.ReadPixels(new Rect(startX, startY, SaveWidth, SaveHeight), 0, 0);
                photo.Apply();
                RenderTexture.active = prev;
                
                return await SavePhotoAsync(photo);
            }
            catch 
            { 
                return false; 
            }
            finally 
            { 
                if (rt) RenderTexture.ReleaseTemporary(rt); 
            }
        }

        /// <summary>
        /// 텍스처를 PNG로 인코딩하여 로컬에 저장하고 서버 업로드를 시도함.
        /// </summary>
        private async UniTask<bool> SavePhotoAsync(Texture2D photo)
        {
            if (!photo) return false;
            
            byte[] rawData = photo.GetRawTextureData();
            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;
            
            string roleString = isServer ? "Left" : "Right";
            int userIdx = SessionManager.Instance ? SessionManager.Instance.CurrentUserIdx : 0;
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            string relativePath = $"{dateStr}/{userIdx}/{roleString}/{userIdx}_{roleString}_{questionId}.png";

            try
            {
                int width = photo.width;
                int height = photo.height;
                UnityEngine.Experimental.Rendering.GraphicsFormat format = photo.graphicsFormat;
                
                byte[] bytes = await UniTask.RunOnThreadPool(() => ImageConversion.EncodeArrayToPNG(rawData, format, (uint)width, (uint)height));
                
                if (FileTransferManager.Instance) 
                {
                    return await FileTransferManager.Instance.UploadPhotoAsync(bytes, relativePath);
                }
                return false;
            }
            catch 
            { 
                return false; 
            }
        }

        /// <summary>
        /// 캔버스 그룹의 알파값을 조절하여 페이드 효과를 연출함.
        /// </summary>
        private IEnumerator FadeCanvasGroupRoutine(CanvasGroup target, float start, float end, float duration)
        {
            if (!target) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                target.alpha = Mathf.Lerp(start, end, elapsed / duration);
                yield return null;
            }
            target.alpha = end;
        }

        /// <summary>
        /// 완료 플래그를 세우고 매니저에게 다음 흐름으로 전환할 것을 알림.
        /// </summary>
        private void CompletePage()
        {
            if (_isCompleted) return;
            _isCompleted = true;
            if (onStepComplete != null) onStepComplete.Invoke(0);
        }
    }
}