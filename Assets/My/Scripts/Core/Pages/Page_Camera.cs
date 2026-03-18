using System;
using System.Collections;
using System.IO;
using Cysharp.Threading.Tasks;
using My.Scripts.Data;
using My.Scripts.Global;
using My.Scripts.Network;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 사진 촬영 결과 페이지.
    /// Why: 백그라운드에서 카메라를 구동하여 캡처하고, 성공 여부에 따라 UI 연출 및 다음 단계 전환을 제어함.
    /// </summary>
    public class Page_Camera : GamePage
    {
        [Header("Canvas Groups")]
        [SerializeField] private CanvasGroup textAnswerCompleteCg;
        [SerializeField] private CanvasGroup textMySceneCg;
        [SerializeField] private CanvasGroup imageCg;
        [Tooltip("캡처 또는 저장 실패 시 표시할 UI 그룹")]
        [SerializeField] private CanvasGroup errorCg;

        [Header("Dynamic UI Components")]
        [SerializeField] private Text textAnswerCompleteUI;
        [SerializeField] private Text textMySceneUI;

        [Header("Save Settings")]
        [Tooltip("사진을 저장할 최상위 경로. 비워두면 로컬 Pictures 폴더 사용.")]
        [SerializeField] private string sharedFolderPath = "";
        [SerializeField] private bool savePhoto = true;
        [SerializeField] private string questionId = "Q1";

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private CommonResultPageData _cachedData; 
        private string _syncCommand = "DEFAULT_RESULT_COMPLETE";
        private bool _isCompleted;
        private Coroutine _sequenceCoroutine;

        private WebCamTexture _webCamTexture;
        private WebCamDevice _selectedDevice;
        private Texture2D _capturedPhoto;
        private const int PhotoWidth = 1920;
        private const int PhotoHeight = 1080;

        public void SetSyncCommand(string command)
        {
            _syncCommand = command;
        }

        public override void SetupData(object data)
        {
            CommonResultPageData pageData = data as CommonResultPageData;
            if (pageData != null) _cachedData = pageData;
            else Debug.LogWarning("[Page_Camera] SetupData: 전달된 데이터가 null이거나 형식이 잘못되었습니다.");
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            ApplyDataToUI();

            if (textAnswerCompleteCg != null) textAnswerCompleteCg.alpha = 0f;
            if (textMySceneCg != null) textMySceneCg.alpha = 0f;
            if (imageCg != null) imageCg.alpha = 0f;
            if (errorCg != null) errorCg.alpha = 0f;

            StartWebCam();

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
            if (textAnswerCompleteCg != null) yield return StartCoroutine(FadeCanvasGroupRoutine(textAnswerCompleteCg, 0f, 1f, fadeDuration));
            if (imageCg != null) yield return StartCoroutine(FadeCanvasGroupRoutine(imageCg, 0f, 1f, fadeDuration));
            
            if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX("레고_4");
            
            yield return CoroutineData.GetWaitForSeconds(2.5f);
            
            if (textMySceneCg != null) yield return StartCoroutine(FadeCanvasGroupRoutine(textMySceneCg, 0f, 1f, fadeDuration));
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            // 수정됨: 캡처 및 저장 결과 확인 로직 도입
            var captureTask = CapturePhotoAsync();
            yield return captureTask.ToCoroutine();
            bool isSuccess = captureTask.GetAwaiter().GetResult();

            if (!isSuccess)
            {
                // 실패 시: 에러 UI 노출 및 시퀀스 중단
                if (textAnswerCompleteCg != null) textAnswerCompleteCg.alpha = 0f;
                if (textMySceneCg != null) textMySceneCg.alpha = 0f;
                if (errorCg != null) yield return StartCoroutine(FadeCanvasGroupRoutine(errorCg, 0f, 1f, fadeDuration));
                
                Debug.LogError("[Page_Camera] 사진 촬영 또는 저장 실패로 시퀀스를 중단합니다.");
                yield break;
            }
            
            yield return CoroutineData.GetWaitForSeconds(0.5f);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                if (textAnswerCompleteCg != null) textAnswerCompleteCg.alpha = Mathf.Lerp(1f, 0f, t);
                if (textMySceneCg != null) textMySceneCg.alpha = Mathf.Lerp(1f, 0f, t);
                if (imageCg != null) imageCg.alpha = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }

            if (textAnswerCompleteCg != null) textAnswerCompleteCg.alpha = 0f;
            if (textMySceneCg != null) textMySceneCg.alpha = 0f;

            // 성공 시에만 완료 텍스트 세팅 및 완료 처리
            SetUIText(textAnswerCompleteUI, _cachedData.textPhotoSaved);
            if (textAnswerCompleteCg != null) yield return StartCoroutine(FadeCanvasGroupRoutine(textAnswerCompleteCg, 0f, 1f, fadeDuration));
            
            if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX("공통_12");
            
            yield return CoroutineData.GetWaitForSeconds(1.5f);
            if (!_isCompleted) CompletePage();
        }

        /// <summary> 사진을 캡처하고 설정에 따라 비동기 저장을 수행한 뒤 결과(성공/실패)를 반환함 </summary>
        private async UniTask<bool> CapturePhotoAsync()
        {
            if (_webCamTexture == null || !_webCamTexture.isPlaying)
            {
                Debug.LogError("[Page_Camera] 웹캠이 작동 중이 아니어서 사진을 찍을 수 없습니다.");
                return false; 
            }

            try
            {
                RenderTexture rt = RenderTexture.GetTemporary(PhotoWidth, PhotoHeight, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(_webCamTexture, rt);

                if (_capturedPhoto == null || _capturedPhoto.width != PhotoWidth || _capturedPhoto.height != PhotoHeight)
                {
                    if (_capturedPhoto != null) Destroy(_capturedPhoto);
                    _capturedPhoto = new Texture2D(PhotoWidth, PhotoHeight, TextureFormat.RGBA32, false);
                }

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                _capturedPhoto.ReadPixels(new Rect(0, 0, PhotoWidth, PhotoHeight), 0, 0);
                _capturedPhoto.Apply();

                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                bool result = true;
                if (savePhoto)
                {
                    result = await SavePhotoAsync(_capturedPhoto);
                }

                StopWebCam();
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Page_Camera] 캡처 프로세스 중 예외 발생: {e.Message}");
                return false;
            }
        }

        /// <summary> UniTask를 이용해 스레드 풀에서 파일을 저장하고 성공 여부를 반환함 </summary>
        private async UniTask<bool> SavePhotoAsync(Texture2D photo)
        {
            if (photo == null) return false;

            byte[] rawData = photo.GetRawTextureData();
            int width = photo.width;
            int height = photo.height;
            UnityEngine.Experimental.Rendering.GraphicsFormat format = photo.graphicsFormat;

            bool isServer = TcpManager.Instance != null && TcpManager.Instance.IsServer;
            string roleString = isServer ? "Server" : "Client";
            string photoName = $"0_{roleString}_{questionId}"; 

            string dataPath = Application.dataPath;
            string customSharedPath = sharedFolderPath;

            try
            {
                return await UniTask.RunOnThreadPool(() =>
                {
                    byte[] bytes = UnityEngine.ImageConversion.EncodeArrayToPNG(rawData, format, (uint)width, (uint)height);
                    string rootPath = customSharedPath;
                    
                    if (string.IsNullOrEmpty(rootPath))
                    {
                        DirectoryInfo parentDir = Directory.GetParent(dataPath);
                        rootPath = Path.Combine(parentDir != null ? parentDir.FullName : dataPath, "Pictures");
                    }

                    string dateFolder = DateTime.Now.ToString("yy-MM-dd");
                    string folder = Path.Combine(rootPath, dateFolder, roleString);

                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    string path = Path.Combine(folder, $"{photoName}.png");
                    File.WriteAllBytes(path, bytes);

                    Debug.Log($"[Page_Camera] 사진 저장 완료: {path}");
                    return true;
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[Page_Camera] 사진 비동기 저장 실패: {e.Message}");
                return false;
            }
        }

        private void StartWebCam()
        {
            if (_webCamTexture != null && _webCamTexture.isPlaying) return;

            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Debug.LogError("[Page_Camera] 카메라 장비를 찾을 수 없습니다.");
                return;
            }

            try
            {
                _webCamTexture = new WebCamTexture(devices[0].name, PhotoWidth, PhotoHeight);
                _webCamTexture.Play();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Page_Camera] 카메라 구동 실패: {e.Message}");
            }
        }

        private void StopWebCam()
        {
            if (_webCamTexture != null && _webCamTexture.isPlaying) _webCamTexture.Stop();
            _webCamTexture = null;
        }

        private IEnumerator FadeCanvasGroupRoutine(CanvasGroup target, float start, float end, float duration)
        {
            if (target == null) yield break;
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

            if (TcpManager.Instance != null && TcpManager.Instance.IsServer)
            {
                TcpManager.Instance.SendMessageToTarget(_syncCommand, "");
            }

            if (onStepComplete != null) onStepComplete.Invoke(0);
        }

        private void OnEnable()
        {
            if (TcpManager.Instance != null) TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
        }

        private void OnDisable()
        {
            if (TcpManager.Instance != null) TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
        }

        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == _syncCommand && !_isCompleted)
            {
                _isCompleted = true;
                if (onStepComplete != null) onStepComplete.Invoke(0);
            }
        }
    }
}