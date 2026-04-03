using System;
using System.IO;
using Cysharp.Threading.Tasks;
using My.Scripts.Global;
using My.Scripts.Hardware;
using My.Scripts.Network;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using Wonjeong.Utils;

namespace My.Scripts.Test
{
    /// <summary>
    /// 하드웨어(RFID) 인식 테스트 및 카메라 크롭 영역 조정을 실시간으로 테스트하기 위한 디버그 매니저.
    /// 런타임 중 설정값을 변경하고 즉시 캡처 결과를 확인하여 개발 및 세팅 시간을 단축하기 위함.
    /// </summary>
    public class DebugTestManager : MonoBehaviour
    {
        [Header("RFID UI")]
        [SerializeField] private Text textRfidLog;

        [Header("Camera Setting UI")]
        [SerializeField] private InputField inputCropTop;
        [SerializeField] private InputField inputCropBottom;
        [SerializeField] private InputField inputCropLeft;
        [SerializeField] private InputField inputCropRight;

        [Header("Buttons")]
        [SerializeField] private Button btnSaveAndLoad;
        [SerializeField] private Button btnCapture;

        [Header("Preview UI")]
        [SerializeField] private RawImage rawImagePreview;

        [Header("Random BG UI")]
        [SerializeField] private Image imgRandomBg;
        [SerializeField] private Button btnRandomBg;
        
        [Header("Arduino UI")]
        [SerializeField] private Button btnLEDOn;
        [SerializeField] private Button btnLEDOff;

        private AsyncOperationHandle<Sprite> _bgHandle;
        private readonly string[] _bgThemes = new string[] { "Sea", "Mt", "River", "Sky", "Forest" };

        /// <summary>
        /// 씬 진입 시 카메라를 가동하고 이벤트 리스너를 등록함.
        /// 기존 세팅값을 UI에 불러와 현재 상태를 시각화하기 위함.
        /// </summary>
        private void Start()
        {
            if (RfidManager.Instance)
            {
                RfidManager.Instance.onRfidReadResult += OnRfidReadResult;
                RfidManager.Instance.onRfidError += OnRfidError;
            }

            if (CameraManager.Instance && CameraManager.Instance.setting != null)
            {
                CameraSetting currentSetting = CameraManager.Instance.setting;
                if (inputCropTop) inputCropTop.text = currentSetting.cropTop.ToString();
                if (inputCropBottom) inputCropBottom.text = currentSetting.cropBottom.ToString();
                if (inputCropLeft) inputCropLeft.text = currentSetting.cropLeft.ToString();
                if (inputCropRight) inputCropRight.text = currentSetting.cropRight.ToString();

                CameraManager.Instance.StartCamera();

                if (rawImagePreview)
                {
                    rawImagePreview.texture = CameraManager.Instance.GetWebCamTexture();
                    UpdatePreviewRect();
                }
            }

            if (btnSaveAndLoad) btnSaveAndLoad.onClick.AddListener(SaveAndLoadSettings);
            if (btnCapture) btnCapture.onClick.AddListener(() => CaptureDebugPhotoAsync().Forget());
            if (btnRandomBg) btnRandomBg.onClick.AddListener(() => LoadRandomBackgroundAsync().Forget());
            
            if (btnLEDOn) btnLEDOn.onClick.AddListener(() => 
            {
                if (ArduinoManager.Instance)
                {
                    ArduinoManager.Instance.SendCommandToLight("LEDOn");
                }
                else
                {
                    Debug.LogError("아두이노가 연결되어있지 않음.");
                }
 
            });
            if (btnLEDOff) btnLEDOff.onClick.AddListener(() => 
            {
                if (ArduinoManager.Instance)
                {
                    ArduinoManager.Instance.SendCommandToLight("LEDOff");
                }
                else
                {
                    Debug.LogError("아두이노가 연결되어있지 않음.");
                }
            });
        }

        /// <summary>
        /// 씬 이탈 시 할당된 이벤트와 카메라 리소스를 해제함.
        /// 다른 씬으로 넘어갈 때 백그라운드 콜백이 실행되는 것을 방지하기 위함.
        /// </summary>
        private void OnDestroy()
        {
            if (RfidManager.Instance)
            {
                RfidManager.Instance.onRfidReadResult -= OnRfidReadResult;
                RfidManager.Instance.onRfidError -= OnRfidError;
            }

            if (CameraManager.Instance)
            {
                CameraManager.Instance.StopCamera();
            }

            if (btnSaveAndLoad) btnSaveAndLoad.onClick.RemoveAllListeners();
            if (btnCapture) btnCapture.onClick.RemoveAllListeners();
            if (btnRandomBg) btnRandomBg.onClick.RemoveAllListeners();
            
            if (btnLEDOn) btnLEDOn.onClick.RemoveAllListeners();
            if (btnLEDOff) btnLEDOff.onClick.RemoveAllListeners();

            if (_bgHandle.IsValid()) Addressables.Release(_bgHandle);
        }

        /// <summary>
        /// RFID 하드웨어로부터 UID를 읽었을 때 로그를 갱신함.
        /// 매핑 여부에 따라 성공 또는 미등록 상태를 모두 출력하여 카드의 유효성을 검증하기 위함.
        /// </summary>
        /// <param name="uid">인식된 카드 UID.</param>
        /// <param name="index">매핑된 답변 인덱스 (미등록 시 -1).</param>
        private void OnRfidReadResult(string uid, int index)
        {
            if (textRfidLog)
            {
                if (index > 0)
                {
                    textRfidLog.text = $"RFID: Answer_{index} + {uid}";
                }
                else
                {
                    textRfidLog.text = $"RFID: no answer + {uid}";
                }
            }
        }

        /// <summary>
        /// RFID 하드웨어 오류 또는 미연결 상태일 때 로그를 갱신함.
        /// </summary>
        /// <param name="errorMessage">발생한 오류 메시지.</param>
        private void OnRfidError(string errorMessage)
        {
            if (textRfidLog)
            {
                textRfidLog.text = $"[실패] {errorMessage}";
            }
        }

        /// <summary>
        /// UI 필드에 입력된 값을 읽어와 JSON 파일로 덮어쓰고 메모리 인스턴스를 갱신함.
        /// 에디터 재시작 없이 수정한 캡처 영역 설정을 즉시 반영하기 위함.
        /// </summary>
        private void SaveAndLoadSettings()
        {
            if (!CameraManager.Instance || CameraManager.Instance.setting == null)
            {
                Debug.LogError("[DebugTestManager] CameraManager 또는 Setting 인스턴스가 없습니다.");
                return;
            }

            CameraSetting setting = CameraManager.Instance.setting;

            if (inputCropTop && int.TryParse(inputCropTop.text, out int top)) setting.cropTop = top;
            if (inputCropBottom && int.TryParse(inputCropBottom.text, out int bottom)) setting.cropBottom = bottom;
            if (inputCropLeft && int.TryParse(inputCropLeft.text, out int left)) setting.cropLeft = left;
            if (inputCropRight && int.TryParse(inputCropRight.text, out int right)) setting.cropRight = right;

            setting.ValidateOrFix();

            string jsonContent = JsonUtility.ToJson(setting, true);
            string jsonFilePath = Path.Combine(Application.streamingAssetsPath, GameConstants.Path.CameraSetting + ".json");

            try
            {
                string directory = Path.GetDirectoryName(jsonFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(jsonFilePath, jsonContent);
                Debug.Log($"[DebugTestManager] CameraSetting 저장 완료 및 로드: {jsonFilePath}");
                
                UpdatePreviewRect();
            }
            catch (Exception e)
            {
                Debug.LogError($"[DebugTestManager] JSON 파일 저장 중 예외 발생: {e.Message}");
            }
        }
        
        /// <summary>
        /// CameraSetting의 크롭 오프셋을 기반으로 RawImage의 uvRect를 재계산하여
        /// 전체 웹캠 텍스처 중 실제 저장될 영역만 UI에 렌더링하도록 갱신함.
        /// </summary>
        private void UpdatePreviewRect()
        {
            if (!rawImagePreview || !CameraManager.Instance) return;

            CameraSetting camSet = CameraManager.Instance.setting;
            if (camSet == null) return;
            
            camSet.ValidateOrFix();
            
            if (camSet.camWidth <= 0 || camSet.camHeight <= 0) return;

            float fCamWidth = camSet.camWidth;
            float fCamHeight = camSet.camHeight;

            // 유니티 UV 좌표계는 좌하단(0,0) ~ 우상단(1,1)을 사용함
            float startX = (fCamWidth / 2f) - camSet.cropLeft;
            float startY = (fCamHeight / 2f) - camSet.cropBottom;

            float uvX = startX / fCamWidth;
            float uvY = startY / fCamHeight;
            float uvW = camSet.SaveWidth / fCamWidth;
            float uvH = camSet.SaveHeight / fCamHeight;

            rawImagePreview.uvRect = new Rect(uvX, uvY, uvW, uvH);

            // 프리뷰 이미지가 찌그러지지 않도록 UI의 종횡비를 실제 저장 비율과 일치시킴
            AspectRatioFitter fitter = rawImagePreview.GetComponent<AspectRatioFitter>();
            if (!fitter)
            {
                fitter = rawImagePreview.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            }
            fitter.aspectRatio = (float)camSet.SaveWidth / (float)camSet.SaveHeight;
        }

       /// <summary>
        /// 현재 CameraSetting의 크롭 오프셋을 기반으로 웹캠 화면을 잘라내어 로컬 폴더에 디버그용으로 저장함.
        /// 사용자가 설정한 상하좌우 여백이 의도대로 동작하는지 확인하기 위함.
        /// </summary>
        private async UniTaskVoid CaptureDebugPhotoAsync()
        {
            if (!CameraManager.Instance) return;

            WebCamTexture cam = CameraManager.Instance.GetWebCamTexture();
            if (!cam || !cam.isPlaying)
            {
                Debug.LogWarning("[DebugTestManager] 카메라가 작동 중이지 않습니다.");
                return;
            }

            CameraSetting camSet = CameraManager.Instance.setting;
            if (camSet == null) return;
            
            camSet.ValidateOrFix();

            RenderTexture rt = null;
            RenderTexture prev = RenderTexture.active;

            try
            {
                rt = RenderTexture.GetTemporary(camSet.camWidth, camSet.camHeight, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(cam, rt);

                Texture2D photo = CameraManager.Instance.GetSharedCapturedPhoto();
                RenderTexture.active = rt;

                int centerX = camSet.camWidth / 2;
                int centerY = camSet.camHeight / 2;

                int startX = centerX - camSet.cropLeft;
                int startY = centerY - camSet.cropBottom;

                photo.ReadPixels(new Rect(startX, startY, camSet.SaveWidth, camSet.SaveHeight), 0, 0);
                photo.Apply();

                byte[] rawData = photo.GetRawTextureData();
                int width = photo.width;
                int height = photo.height;
                UnityEngine.Experimental.Rendering.GraphicsFormat format = photo.graphicsFormat;

                byte[] pngBytes = await UniTask.RunOnThreadPool(() => UnityEngine.ImageConversion.EncodeArrayToPNG(rawData, format, (uint)width, (uint)height));

                string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
                string timeStr = DateTime.Now.ToString("HHmmss");
                
                string rootPath = @"C:\UnitySharedPicture";
                TcpSetting loadedSetting = JsonLoader.Load<TcpSetting>(GameConstants.Path.TcpSetting);
                if (loadedSetting != null && !string.IsNullOrWhiteSpace(loadedSetting.localSaveRoot))
                {
                    rootPath = loadedSetting.localSaveRoot;
                }
                
                string saveDirectory = Path.Combine(rootPath, dateStr, "debug");

                if (!Directory.Exists(saveDirectory))
                {
                    Directory.CreateDirectory(saveDirectory);
                }

                string filePath = Path.Combine(saveDirectory, $"debug_capture_{timeStr}.png");
                await File.WriteAllBytesAsync(filePath, pngBytes);

                Debug.Log($"[DebugTestManager] 디버그 사진 저장 완료: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DebugTestManager] 캡처 중 예외 발생: {e.Message}");
            }
            finally
            {
                RenderTexture.active = prev;
                if (rt) RenderTexture.ReleaseTemporary(rt);
            }
        }
        
        /// <summary>
        /// 테마, 서브테마, 인덱스를 무작위로 조합하여 어드레서블에서 배경 이미지를 로드함.
        /// 리소스가 정상적으로 세팅되어 있는지 디버그 씬에서 즉각적으로 검증하기 위함.
        /// </summary>
        private async UniTaskVoid LoadRandomBackgroundAsync()
        {
            if (!imgRandomBg) return;

            string mainTheme = _bgThemes[UnityEngine.Random.Range(0, _bgThemes.Length)];
            int subTheme = UnityEngine.Random.Range(1, 6);
            int index = UnityEngine.Random.Range(1, 16);

            string bgKey = $"BG_Step2_{mainTheme}_{subTheme}_{index}";
            Debug.Log($"[DebugTestManager] 랜덤 배경 로드 시도: {bgKey}");

            if (_bgHandle.IsValid()) Addressables.Release(_bgHandle);

            try
            {
                _bgHandle = Addressables.LoadAssetAsync<Sprite>(bgKey);
                Sprite loadedSprite = await _bgHandle.ToUniTask();
                
                if (imgRandomBg && loadedSprite)
                {
                    imgRandomBg.sprite = loadedSprite;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DebugTestManager] 배경 로드 실패 ({bgKey}): {e.Message}");
                if (imgRandomBg) imgRandomBg.sprite = null;
            }
        }
    }
}