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
        [Tooltip("사진을 저장할 최상위 경로. 비워두면 시스템 사진 폴더를 사용합니다.")]
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

            // Finding 4: 시퀀스 시작 전 필수 데이터 검증
            if (_cachedData == null || _cachedData.textPhotoSaved == null)
            {
                Debug.LogError("[Page_Camera] 필수 데이터(_cachedData)가 누락되어 페이지를 시작할 수 없습니다.");
                if (errorCg != null) errorCg.alpha = 1f;
                if (textMySceneCg != null) textMySceneCg.alpha = 0f;
                return;
            }

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

            var captureTask = CapturePhotoAsync();
            yield return captureTask.ToCoroutine();
            bool isSuccess = captureTask.GetAwaiter().GetResult();

            if (!isSuccess)
            {
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

            SetUIText(textAnswerCompleteUI, _cachedData.textPhotoSaved);
            if (textAnswerCompleteCg != null) yield return StartCoroutine(FadeCanvasGroupRoutine(textAnswerCompleteCg, 0f, 1f, fadeDuration));
            
            if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX("공통_12");
            
            yield return CoroutineData.GetWaitForSeconds(1.5f);
            if (!_isCompleted) CompletePage();
        }

        /// <summary> 사진을 캡처하고 설정에 따라 비동기 저장을 수행한 뒤 결과(성공/실패)를 반환함 </summary>
        private async UniTask<bool> CapturePhotoAsync()
        {
            // Finding 3: 재생 상태 및 프레임 유효성 검사 강화
            if (_webCamTexture == null || !_webCamTexture.isPlaying)
            {
                Debug.LogError("[Page_Camera] 웹캠이 작동 중이 아니어서 사진을 찍을 수 없습니다.");
                return false; 
            }

            // 프레임이 준비될 때까지 대기 (최대 2초)
            float timeout = 2.0f;
            float elapsed = 0f;
            while ((_webCamTexture.width <= 16 || !_webCamTexture.didUpdateThisFrame) && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                await UniTask.Yield();
            }

            if (_webCamTexture.width <= 16)
            {
                Debug.LogError("[Page_Camera] 웹캠 프레임 초기화 실패(해상도 0).");
                return false;
            }

            RenderTexture rt = null;
            RenderTexture prev = null;

            try
            {
                rt = RenderTexture.GetTemporary(PhotoWidth, PhotoHeight, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(_webCamTexture, rt);

                if (_capturedPhoto == null || _capturedPhoto.width != PhotoWidth || _capturedPhoto.height != PhotoHeight)
                {
                    if (_capturedPhoto != null) Destroy(_capturedPhoto);
                    _capturedPhoto = new Texture2D(PhotoWidth, PhotoHeight, TextureFormat.RGBA32, false);
                }

                prev = RenderTexture.active;
                RenderTexture.active = rt;
                _capturedPhoto.ReadPixels(new Rect(0, 0, PhotoWidth, PhotoHeight), 0, 0);
                _capturedPhoto.Apply();

                bool result = true;
                if (savePhoto)
                {
                    result = await SavePhotoAsync(_capturedPhoto);
                }

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Page_Camera] 캡처 프로세스 중 예외 발생: {e.Message}");
                return false;
            }
            finally
            {
                // Finding 1: 예외 발생 시에도 자원 해제 및 웹캠 정지 보장
                if (prev != null) RenderTexture.active = prev;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                StopWebCam();
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

            // 메인 스레드에서 접근 가능한 쓰기 전용 경로 미리 캡처
            string persistentPath = Application.persistentDataPath;
            string customSharedPath = sharedFolderPath;

            try
            {
                return await UniTask.RunOnThreadPool(() =>
                {
                    byte[] bytes = UnityEngine.ImageConversion.EncodeArrayToPNG(rawData, format, (uint)width, (uint)height);
                    string rootPath = customSharedPath;
                    
                    // Finding 2: 설치 폴더 대신 보장된 쓰기 가능 경로 로직 사용
                    if (string.IsNullOrEmpty(rootPath))
                    {
                        try 
                        {
                            rootPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
                            {
                                rootPath = persistentPath;
                            }
                        }
                        catch
                        {
                            rootPath = persistentPath;
                        }
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