using System;
using System.Collections;
using System.IO;
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
        [SerializeField] private string sharedFolderPath = "";
        [SerializeField] private bool savePhoto = true;
        [SerializeField] private string questionId = "Q1";

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private CommonResultPageData _cachedData; 
        private bool _isCompleted;
        private Coroutine _sequenceCoroutine;

        private WebCamTexture _webCamTexture;
        private Texture2D _capturedPhoto;
        private const int CamWidth = 1920;
        private const int CamHeight = 1080;
        private const int SaveWidth = 960;
        private const int SaveHeight = 1080;
        
        private static WebCamTexture _sharedWebCamTexture;
        private static Texture2D _sharedCapturedPhoto;
        private static int _instanceCount = 0;
        

        public void SetSyncCommand(string command) { }

        protected override void Awake()
        {
            base.Awake();
            _instanceCount++;
        }

        public override void SetupData(object data)
        {
            CommonResultPageData pageData = data as CommonResultPageData;
            if (pageData != null) _cachedData = pageData;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            if (_cachedData == null || _cachedData.textPhotoSaved == null)
            {
                UnityEngine.Debug.LogWarning("[Page_Camera] 데이터가 누락되었습니다. 에러 화면을 출력합니다.");
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

            if (object.ReferenceEquals(_sequenceCoroutine, null) == false) StopCoroutine(_sequenceCoroutine);
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
            StopWebCam();
        }

       

        private void ApplyDataToUI()
        {
            if (_cachedData == null) return;
            SetUIText(textAnswerCompleteUI, _cachedData.textAnswerComplete);
            SetUIText(textMySceneUI, _cachedData.textMyScene);
        }

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

private async UniTask<bool> CapturePhotoAsync()
        {
            RenderTexture rt = null;
            RenderTexture prev = null;

            try
            {
                if (!_sharedWebCamTexture || !_sharedWebCamTexture.isPlaying) return false; 

                float timeout = 2.0f;
                float elapsed = 0f;
                
                while (elapsed < timeout)
                {
                    if (!_sharedWebCamTexture) return false; 
                    if (_sharedWebCamTexture.width > 16 && _sharedWebCamTexture.didUpdateThisFrame) break;
                    elapsed += Time.deltaTime;
                    await UniTask.Yield();
                }

                if (!_sharedWebCamTexture || _sharedWebCamTexture.width <= 16) return false;

                // 카메라 원본 해상도만큼 RenderTexture 생성
                rt = RenderTexture.GetTemporary(CamWidth, CamHeight, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(_sharedWebCamTexture, rt);

                // 저장할 크기(960x1080)로 Texture2D를 정적으로 한 번만 생성하여 재사용함
                if (!_sharedCapturedPhoto || _sharedCapturedPhoto.width != SaveWidth || _sharedCapturedPhoto.height != SaveHeight)
                {
                    if (_sharedCapturedPhoto) Destroy(_sharedCapturedPhoto);
                    _sharedCapturedPhoto = new Texture2D(SaveWidth, SaveHeight, TextureFormat.RGBA32, false);
                }

                prev = RenderTexture.active;
                RenderTexture.active = rt;
                
                // 가운데 크롭을 위한 X 좌표 계산: (1920 - 960) / 2 = 480
                int startX = (CamWidth - SaveWidth) / 2;
                int startY = (CamHeight - SaveHeight) / 2; 

                // 화면 가운데 영역만 잘라서 읽어오기
                _sharedCapturedPhoto.ReadPixels(new Rect(startX, startY, SaveWidth, SaveHeight), 0, 0);
                _sharedCapturedPhoto.Apply();

                bool result = true;
                string currentSceneName = SceneManager.GetActiveScene().name;
                bool canSaveScene = currentSceneName == GameConstants.Scene.Step2 || currentSceneName == GameConstants.Scene.Step3;

                if (savePhoto && canSaveScene)
                {
                    result = await SavePhotoAsync(_sharedCapturedPhoto);
                }

                return result;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[Page_Camera] 촬영 중 예외 발생: {e.Message}");
                return false;
            }
            finally
            {
                if (prev) RenderTexture.active = prev;
                if (rt) RenderTexture.ReleaseTemporary(rt);
                StopWebCam();
            }
        }

        private async UniTask<bool> SavePhotoAsync(Texture2D photo)
        {
            if (!photo) return false;

            byte[] rawData = photo.GetRawTextureData();
            int width = photo.width;
            int height = photo.height;
            UnityEngine.Experimental.Rendering.GraphicsFormat format = photo.graphicsFormat;

            bool isServer = (TcpManager.Instance && TcpManager.Instance.IsServer);
            string roleString = isServer ? "Left" : "Right";
            
            // SessionManager에 저장된 실제 유저 인덱스 사용
            int userIdx = SessionManager.Instance ? SessionManager.Instance.CurrentUserIdx : 0;
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            string photoName = $"{userIdx}_{roleString}_{questionId}.png"; 
            string relativePath = $"{dateStr}/{userIdx}/{roleString}/{photoName}";

            try
            {
                byte[] bytes = await UniTask.RunOnThreadPool(() => UnityEngine.ImageConversion.EncodeArrayToPNG(rawData, format, (uint)width, (uint)height));

                // Why: Step3의 최종 결과물인 현재 모듈 사진인 경우에만 API 업로드 메서드 실행
                string currentModule = SessionManager.Instance ? SessionManager.Instance.CurrentModuleCode : "D3";
                
                if (questionId.ToUpper() == currentModule.ToUpper() && SessionManager.Instance)
                {
                    string uid = isServer ? SessionManager.Instance.PlayerAUid : SessionManager.Instance.PlayerBUid;

                    if (APIManager.Instance)
                    {
                        APIManager.Instance.UploadImageAsync(bytes, userIdx, uid, currentModule).Forget();
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
        
        private void StartWebCam()
        {
            if (_sharedWebCamTexture && _sharedWebCamTexture.isPlaying) return;

            if (!_sharedWebCamTexture)
            {
                WebCamDevice[] devices = WebCamTexture.devices;
                if (devices.Length == 0) return;
                _sharedWebCamTexture = new WebCamTexture(devices[0].name, CamWidth, CamHeight);
            }

            try
            {
                _sharedWebCamTexture.Play();
            }
            catch (Exception) { }
        }

        private void StopWebCam()
        {
            if (_sharedWebCamTexture && _sharedWebCamTexture.isPlaying) _sharedWebCamTexture.Stop();
        }

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
        
        private void OnDestroy()
        {
            StopWebCam();
            _instanceCount--;

            if (_instanceCount <= 0)
            {
                if (_sharedWebCamTexture)
                {
                    Destroy(_sharedWebCamTexture);
                    _sharedWebCamTexture = null;
                }
                if (_sharedCapturedPhoto)
                {
                    Destroy(_sharedCapturedPhoto);
                    _sharedCapturedPhoto = null;
                }
                _instanceCount = 0;
            }
        }
    }
}