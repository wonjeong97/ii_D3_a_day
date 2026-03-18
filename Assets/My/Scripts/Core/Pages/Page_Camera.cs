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
    /// Why: 사용자에게 웹캠 화면을 보여주지 않고, 백그라운드에서 카메라를 구동하여 캡처한 뒤
    /// TCP 역할(Server/Client)에 따라 지정된 공유 폴더에 비동기로 분리 저장하기 위함.
    /// </summary>
    public class Page_Camera : GamePage
    {
        [Header("Canvas Groups")]
        [SerializeField] private CanvasGroup textAnswerCompleteCg;
        [SerializeField] private CanvasGroup textMySceneCg;
        [SerializeField] private CanvasGroup imageCg;

        [Header("Dynamic UI Components")]
        [SerializeField] private Text textAnswerCompleteUI;
        [SerializeField] private Text textMySceneUI;

        [Header("Save Settings")]
        [Tooltip("사진을 저장할 최상위 경로 (예: C:\\SharedPictures 또는 \\\\192.168.0.xxx\\SharedPictures). 비워두면 기본 로컬 Pictures 폴더에 저장됩니다.")]
        [SerializeField] private string sharedFolderPath = "";
        [Tooltip("사진을 저장할지 여부")]
        [SerializeField] private bool savePhoto = true;
        [Tooltip("현재 질문 번호 (예: Q1, Q2, ..., Q15)")]
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

            if (textAnswerCompleteCg) textAnswerCompleteCg.alpha = 0f;
            if (textMySceneCg) textMySceneCg.alpha = 0f;
            if (imageCg) imageCg.alpha = 0f;

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
            if (_capturedPhoto)
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

            CapturePhoto();
            
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

        private void CapturePhoto()
        {
            if (!_webCamTexture || !_webCamTexture.isPlaying)
            {
                Debug.LogError("[Page_Camera] 웹캠이 작동 중이 아니어서 캡처할 수 없습니다.");
                return; 
            }

            RenderTexture rt = RenderTexture.GetTemporary(PhotoWidth, PhotoHeight, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(_webCamTexture, rt);

            if (!_capturedPhoto || _capturedPhoto.width != PhotoWidth || _capturedPhoto.height != PhotoHeight)
            {
                if (_capturedPhoto) Destroy(_capturedPhoto);
                _capturedPhoto = new Texture2D(PhotoWidth, PhotoHeight, TextureFormat.RGBA32, false);
            }

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            _capturedPhoto.ReadPixels(new Rect(0, 0, PhotoWidth, PhotoHeight), 0, 0);
            _capturedPhoto.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            if (savePhoto)
            {
                SavePhotoAsync(_capturedPhoto).Forget();
            }

            StopWebCam();
        }

        /// <summary>
        /// UniTask를 이용해 TCP 역할(Server/Client)에 따라 지정된 공유 폴더 경로에 비동기로 PNG 이미지를 저장함.
        /// </summary>
        private async UniTaskVoid SavePhotoAsync(Texture2D photo)
        {
            if (!photo) return;

            byte[] rawData = photo.GetRawTextureData();
            int width = photo.width;
            int height = photo.height;
            UnityEngine.Experimental.Rendering.GraphicsFormat format = photo.graphicsFormat;

            // 현재 PC가 서버인지 클라이언트인지 판별
            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

            string roleString = isServer ? "Server" : "Client";
            string photoName = $"0_{roleString}_{questionId}"; 

            // 메인 스레드에서만 접근 가능한 변수를 미리 캐싱
            string dataPath = Application.dataPath;
            string customSharedPath = sharedFolderPath;

            try
            {
                await UniTask.RunOnThreadPool(() =>
                {
                    byte[] bytes = UnityEngine.ImageConversion.EncodeArrayToPNG(rawData, format, (uint)width, (uint)height);

                    string rootPath = customSharedPath;
                    
                    // 인스펙터의 공유 폴더 경로가 비어있다면 기존 로컬 경로를 Fallback으로 사용
                    if (string.IsNullOrEmpty(rootPath))
                    {
                        DirectoryInfo parentDir = Directory.GetParent(dataPath);
                        rootPath = Path.Combine(parentDir != null ? parentDir.FullName : dataPath, "Pictures");
                    }

                    string dateFolder = DateTime.Now.ToString("yy-MM-dd");
                    
                    // 완성되는 구조: 최상위경로 / yy-MM-dd / Server(또는 Client)
                    string folder = Path.Combine(rootPath, dateFolder, roleString);

                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    string path = Path.Combine(folder, $"{photoName}.png");
                    File.WriteAllBytes(path, bytes);

                    Debug.Log($"[Page_Camera] 백그라운드 사진 저장 완료: {path}");
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[Page_Camera] 백그라운드 사진 저장 실패: {e.Message}");
            }
        }

        private void StartWebCam()
        {
            if (_webCamTexture && _webCamTexture.isPlaying) return;

            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Debug.LogError("[Page_Camera] PC에 인식된 카메라 장비가 없습니다.");
                return;
            }

            string selectedDeviceName = devices[0].name;
            _selectedDevice = devices[0];

            try
            {
                _webCamTexture = new WebCamTexture(selectedDeviceName, PhotoWidth, PhotoHeight);
                _webCamTexture.Play();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Page_Camera] 카메라 연결 예외 발생: {e.Message}");
            }
        }

        private void StopWebCam()
        {
            if (_webCamTexture && _webCamTexture.isPlaying) _webCamTexture.Stop();
            _webCamTexture = null;
        }

        private IEnumerator FadeCanvasGroupRoutine(CanvasGroup target, float start, float end, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (target) target.alpha = Mathf.Lerp(start, end, elapsed / duration);
                yield return null;
            }
            if (target) target.alpha = end;
        }

        private void CompletePage()
        {
            if (_isCompleted) return;
            _isCompleted = true;

            if (TcpManager.Instance && TcpManager.Instance.IsServer)
            {
                TcpManager.Instance.SendMessageToTarget(_syncCommand, "");
            }

            if (onStepComplete != null) onStepComplete.Invoke(0);
        }

        private void OnEnable()
        {
            if (TcpManager.Instance) TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
        }

        private void OnDisable()
        {
            if (TcpManager.Instance) TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
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