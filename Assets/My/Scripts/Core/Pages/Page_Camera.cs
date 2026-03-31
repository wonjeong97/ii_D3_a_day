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
    /// 웹캠을 제어하여 사용자의 사진을 촬영하고 서버 및 로컬에 저장하는 페이지 컨트롤러.
    /// 답변 완료 연출 후 실시간 촬영 데이터를 처리하여 결과물로 보존하기 위함.
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
        [SerializeField] private string sharedFolderPath;
        [SerializeField] private bool savePhoto;
        [SerializeField] private string questionId;

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private CommonResultPageData _cachedData; 
        private bool _isCompleted;
        private Coroutine _sequenceCoroutine;

        private WebCamTexture _webCamTexture;
        private Texture2D _capturedPhoto;
        private const int PhotoWidth = 1920;
        private const int PhotoHeight = 1080;

        public void SetSyncCommand(string command) { }

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
        /// 페이지 진입 시 초기화 및 웹캠 가동, 촬영 시퀀스를 시작함.
        /// 데이터 누락 시 에러 화면을 출력하여 비정상 흐름을 차단함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            if (_cachedData == null || _cachedData.textPhotoSaved == null)
            {
                Debug.LogWarning("[Page_Camera] 데이터가 누락되었습니다. 에러 화면을 출력합니다.");
                if (errorCg) errorCg.alpha = 1f;
                if (textMySceneCg) textMySceneCg.alpha = 0f;
                return;
            }

            ApplyDataToUI();

            if (textAnswerCompleteCg) textAnswerCompleteCg.alpha = 0f;
            if (textMySceneCg) textMySceneCg.alpha = 0f;
            if (imageCg) imageCg.alpha = 0f;
            if (errorCg) errorCg.alpha = 0f;

            StartWebCam();

            if (ReferenceEquals(_sequenceCoroutine, null) == false) StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = StartCoroutine(SequenceRoutine());
        }

        /// <summary>
        /// 페이지 이탈 시 웹캠을 정지하고 진행 중인 시퀀스를 중단함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }
            StopWebCam();
        }

        /// <summary>
        /// 객체 파괴 시 웹캠을 정지하고 캡처된 텍스처 메모리를 해제함.
        /// </summary>
        private void OnDestroy()
        {
            StopWebCam();
            if (_capturedPhoto != null)
            {
                Destroy(_capturedPhoto);
                _capturedPhoto = null;
            }
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
        /// 답변 완료 안내, 촬영, 저장 완료 안내를 순차적으로 제어함.
        /// 지정된 간격에 맞춰 효과음과 시각 연출을 동기화하기 위함.
        /// </summary>
        private IEnumerator SequenceRoutine()
        {
            if (textAnswerCompleteCg) yield return StartCoroutine(FadeCanvasGroupRoutine(textAnswerCompleteCg, 0f, 1f, fadeDuration));
            if (imageCg) yield return StartCoroutine(FadeCanvasGroupRoutine(imageCg, 0f, 1f, fadeDuration));
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("레고_4");
            
            yield return CoroutineData.GetWaitForSeconds(2.5f);
            
            if (textMySceneCg) yield return StartCoroutine(FadeCanvasGroupRoutine(textMySceneCg, 0f, 1f, fadeDuration));
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            bool isSuccess = false;
            yield return UniTask.ToCoroutine(async () => 
            {
                isSuccess = await CapturePhotoAsync();
            });

            if (!isSuccess)
            {
                if (textAnswerCompleteCg) textAnswerCompleteCg.alpha = 0f;
                if (textMySceneCg) textMySceneCg.alpha = 0f;
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

            if (textAnswerCompleteCg) textAnswerCompleteCg.alpha = 0f;
            if (textMySceneCg) textMySceneCg.alpha = 0f;

            SetUIText(textAnswerCompleteUI, _cachedData.textPhotoSaved);
            if (textAnswerCompleteCg) yield return StartCoroutine(FadeCanvasGroupRoutine(textAnswerCompleteCg, 0f, 1f, fadeDuration));
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_12");
            
            yield return CoroutineData.GetWaitForSeconds(1.5f);
            if (!_isCompleted) CompletePage();
        }

        /// <summary>
        /// 웹캠의 현재 프레임을 읽어 Texture2D로 변환하고 저장을 트리거함.
        /// 비동기 처리를 통해 촬영 중 메인 스레드 멈춤 현상을 방지함.
        /// </summary>
        /// <returns>촬영 및 저장 성공 여부.</returns>
       private async UniTask<bool> CapturePhotoAsync()
        {
            RenderTexture rt = null;
            RenderTexture prev = null;

            try
            {
                if (!_webCamTexture || !_webCamTexture.isPlaying) return false; 

                float timeout = 2.0f;
                float elapsed = 0f;
                
                while (elapsed < timeout)
                {
                    if (!_webCamTexture) return false; 
                    if (_webCamTexture.width > 16 && _webCamTexture.didUpdateThisFrame) break;
                    elapsed += Time.deltaTime;
                    await UniTask.Yield();
                }

                if (!_webCamTexture || _webCamTexture.width <= 16) return false;

                rt = RenderTexture.GetTemporary(PhotoWidth, PhotoHeight, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(_webCamTexture, rt);

                if (!_capturedPhoto || _capturedPhoto.width != PhotoWidth || _capturedPhoto.height != PhotoHeight)
                {
                    if (_capturedPhoto) Destroy(_capturedPhoto);
                    _capturedPhoto = new Texture2D(PhotoWidth, PhotoHeight, TextureFormat.RGBA32, false);
                }

                prev = RenderTexture.active;
                RenderTexture.active = rt;
                _capturedPhoto.ReadPixels(new Rect(0, 0, PhotoWidth, PhotoHeight), 0, 0);
                _capturedPhoto.Apply();

                bool result = true;
                string currentSceneName = SceneManager.GetActiveScene().name;
                bool canSaveScene = currentSceneName == GameConstants.Scene.Step2 || currentSceneName == GameConstants.Scene.Step3;

                if (savePhoto && canSaveScene)
                {
                    result = await SavePhotoAsync(_capturedPhoto);
                }

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Page_Camera] 촬영 중 예외 발생: {e.Message}");
                return false;
            }
            finally
            {
                if (prev) RenderTexture.active = prev;
                if (rt) RenderTexture.ReleaseTemporary(rt);
                StopWebCam();
            }
        }

        /// <summary>
        /// 촬영된 텍스처를 PNG로 인코딩하여 로컬 저장소 및 서버에 업로드함.
        /// Step3의 최종 결과물(D3)인 경우에만 관리자용 API 업로드를 병행 수행함.
        /// </summary>
        /// <param name="photo">인코딩할 원본 텍스처 객체.</param>
        /// <returns>업로드 성공 여부.</returns>
        private async UniTask<bool> SavePhotoAsync(Texture2D photo)
        {
            if (!photo) return false;

            byte[] rawData = photo.GetRawTextureData();
            int width = photo.width;
            int height = photo.height;
            UnityEngine.Experimental.Rendering.GraphicsFormat format = photo.graphicsFormat;

            bool isServer = (TcpManager.Instance && TcpManager.Instance.IsServer);
            string roleString = isServer ? "Left" : "Right";
            
            int userIdx = SessionManager.Instance ? SessionManager.Instance.CurrentUserIdx : 0;
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            string photoName = $"{userIdx}_{roleString}_{questionId}.png"; 
            string relativePath = $"{dateStr}/{userIdx}/{roleString}/{photoName}";

            try
            {
                byte[] bytes = await UniTask.RunOnThreadPool(() => ImageConversion.EncodeArrayToPNG(rawData, format, (uint)width, (uint)height));

                if (questionId.ToUpper() == "D3" && SessionManager.Instance)
                {
                    string uid = isServer ? SessionManager.Instance.PlayerAUid : SessionManager.Instance.PlayerBUid;
                    string module = "d3"; 

                    if (APIManager.Instance)
                    {
                        APIManager.Instance.UploadImageAsync(bytes, userIdx, uid, module).Forget();
                    }
                }

                if (FileTransferManager.Instance)
                {
                    return await FileTransferManager.Instance.UploadPhotoAsync(bytes, relativePath);
                }
                return false;
            }
            catch (Exception) { return false; }
        }
        
        // # TODO: WebCamTexture의 잦은 할당 및 해제가 메모리 단편화를 유발할 수 있으므로, 글로벌 매니저에서 풀링하여 재사용하는 구조 검토 필요.

        /// <summary>
        /// 시스템에 연결된 카메라 장치를 찾아 웹캠 텍스처 재생을 시작함.
        /// </summary>
        private void StartWebCam()
        {
            if (_webCamTexture && _webCamTexture.isPlaying) return;

            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0) return;

            try
            {
                _webCamTexture = new WebCamTexture(devices[0].name, PhotoWidth, PhotoHeight);
                _webCamTexture.Play();
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 활성화된 웹캠 재생을 정지하고 리소스를 해제함.
        /// </summary>
        private void StopWebCam()
        {
            if (_webCamTexture && _webCamTexture.isPlaying) _webCamTexture.Stop();
            _webCamTexture = null;
        }

        /// <summary>
        /// 캔버스 그룹의 알파값을 시간에 따라 선형 보간하여 페이드 효과를 구현함.
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
        /// 페이지 완료 플래그를 설정하고 매니저에게 시퀀스 종료를 알림.
        /// </summary>
        private void CompletePage()
        {
            if (_isCompleted) return;
            _isCompleted = true;

            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }

            if (onStepComplete != null) onStepComplete.Invoke(0);
        }
    }
}