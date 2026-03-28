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
        private const int PhotoWidth = 1920;
        private const int PhotoHeight = 1080;

        public void SetSyncCommand(string command) { }

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

        private void OnDestroy()
        {
            StopWebCam();
            if (_capturedPhoto != null)
            {
                Destroy(_capturedPhoto);
                _capturedPhoto = null;
            }
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

                // Why: Step3의 최종 결과물인 "D3" 사진인 경우에만 API 업로드 메서드 실행
                if (questionId.ToUpper() == "D3" && SessionManager.Instance)
                {
                    string uid = isServer ? SessionManager.Instance.PlayerAUid : SessionManager.Instance.PlayerBUid;
                    string module = "d3"; 

                    APIManager api = UnityEngine.Object.FindFirstObjectByType<APIManager>();
                    if (api)
                    {
                        // 새롭게 수정한 메서드 호출
                        api.UploadImageAsync(bytes, userIdx, uid, module).Forget();
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
        
        // # TODO: WebCamTexture의 잦은 할당 및 해제가 메모리 단편화를 유발할 수 있으므로, 글로벌 매니저에서 풀링하여 재사용하는 구조 검토 필요

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

        private void StopWebCam()
        {
            if (_webCamTexture && _webCamTexture.isPlaying) _webCamTexture.Stop();
            _webCamTexture = null;
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
    }
}