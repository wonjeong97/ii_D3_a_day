using System;
using UnityEngine;

namespace My.Scripts.Global
{   
   [Serializable]
    public class CameraSetting
    {
        [Header("WebCam Resolution")]
        public int camWidth = 1920;
        public int camHeight = 1080;

        [Header("Crop Offsets (From Center)")]
        public int cropLeft = 480;
        public int cropRight = 480;
        public int cropTop = 540;
        public int cropBottom = 540;

        public int SaveWidth => cropLeft + cropRight;
        public int SaveHeight => cropTop + cropBottom;

        /// <summary>
        /// 설정된 해상도 및 크롭 영역의 유효성을 검사하고 잘못된 값을 교정함.
        /// Out of bounds 에러에 의한 캡처 실패를 방지하기 위함.
        /// 입력 예: (camWidth: 1920, cropLeft: 2000) -> 결과 cropLeft = 960
        /// </summary>
        public void ValidateOrFix()
        {
            if (camWidth <= 0)
            {
                camWidth = 1920;
                Debug.LogWarning("[CameraSetting] camWidth 제한 교정");
            }
            if (camHeight <= 0)
            {
                camHeight = 1080;
                Debug.LogWarning("[CameraSetting] camHeight 제한 교정");
            }

            if (cropLeft < 0) cropLeft = 0;
            if (cropRight < 0) cropRight = 0;
            if (cropTop < 0) cropTop = 0;
            if (cropBottom < 0) cropBottom = 0;

            int halfWidth = camWidth / 2;
            int halfHeight = camHeight / 2;

            if (cropLeft > halfWidth)
            {
                cropLeft = halfWidth;
                Debug.LogWarning("[CameraSetting] cropLeft 제한 교정");
            }
            if (cropRight > halfWidth)
            {
                cropRight = halfWidth;
                Debug.LogWarning("[CameraSetting] cropRight 제한 교정");
            }
            if (cropTop > halfHeight)
            {
                cropTop = halfHeight;
                Debug.LogWarning("[CameraSetting] cropTop 제한 교정");
            }
            if (cropBottom > halfHeight)
            {
                cropBottom = halfHeight;
                Debug.LogWarning("[CameraSetting] cropBottom 제한 교정");
            }
        }
    }
    
    /// <summary>
    /// 웹캠 리소스를 전역에서 풀링하고 관리하는 매니저.
    /// WebCamTexture의 빈번한 생성과 파괴로 인한 메모리 단편화를 방지하기 위함.
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        public static CameraManager Instance { get; private set; }

        [Header("Camera Settings")]
        public CameraSetting setting;

        private WebCamTexture _sharedWebCamTexture;
        private Texture2D _sharedCapturedPhoto;

        /// <summary>
        /// 싱글톤 인스턴스를 초기화하고 카메라 설정 데이터를 로드함.
        /// </summary>
        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadSettings();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 외부 JSON 파일로부터 카메라 해상도 및 크롭 설정값을 로드함.
        /// 파일 로드에 실패할 경우 널 참조 예외를 방지하기 위해 빈 객체를 생성함.
        /// </summary>
        private void LoadSettings()
        {
            CameraSetting loadedSetting = Wonjeong.Utils.JsonLoader.Load<CameraSetting>(GameConstants.Path.CameraSetting);
            
            if (loadedSetting != null)
            {
                setting = loadedSetting;
            }
            else 
            {
                setting = new CameraSetting();
                Debug.LogWarning("[CameraManager] CameraSetting.json 로드 실패. 기본값을 사용합니다.");
            }
            
            setting.ValidateOrFix();
        }

        /// <summary>
        /// 공유 웹캠 텍스처를 반환하거나 없으면 새로 생성함.
        /// 장치 연결 상태를 확인하여 유효한 텍스처만 제공하기 위함.
        /// </summary>
        public WebCamTexture GetWebCamTexture()
        {
            if (_sharedWebCamTexture) return _sharedWebCamTexture;

            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Debug.LogWarning("[CameraManager] 연결된 카메라 장치를 찾을 수 없음.");
                return null;
            }

            _sharedWebCamTexture = new WebCamTexture(devices[0].name, setting.camWidth, setting.camHeight);
            return _sharedWebCamTexture;
        }

        /// <summary>
        /// 촬영 데이터를 담을 전역 텍스처를 반환함.
        /// 동적으로 계산된 저장 해상도와 현재 텍스처 크기가 일치하지 않으면 새로 생성하여 규격을 맞춤.
        /// </summary>
        public Texture2D GetSharedCapturedPhoto()
        {
            if (!_sharedCapturedPhoto || _sharedCapturedPhoto.width != setting.SaveWidth || _sharedCapturedPhoto.height != setting.SaveHeight)
            {
                if (_sharedCapturedPhoto) Destroy(_sharedCapturedPhoto);
                _sharedCapturedPhoto = new Texture2D(setting.SaveWidth, setting.SaveHeight, TextureFormat.RGBA32, false);
            }
            return _sharedCapturedPhoto;
        }

        /// <summary>
        /// 웹캠 작동을 시작함.
        /// </summary>
        public void StartCamera()
        {
            WebCamTexture cam = GetWebCamTexture();
            if (cam && !cam.isPlaying)
            {
                cam.Play();
            }
        }

        /// <summary>
        /// 웹캠 작동을 정지함. 
        /// 텍스트 메모리는 해제하지 않고 상태만 정지하여 다음 호출 시 즉시 재사용함.
        /// </summary>
        public void StopCamera()
        {
            if (_sharedWebCamTexture && _sharedWebCamTexture.isPlaying)
            {
                _sharedWebCamTexture.Stop();
            }
        }

        /// <summary>
        /// 앱 종료 시 할당된 모든 카메라 리소스를 명시적으로 파괴함.
        /// </summary>
        private void OnDestroy()
        {
            if (_sharedWebCamTexture)
            {
                _sharedWebCamTexture.Stop();
                Destroy(_sharedWebCamTexture);
            }
            if (_sharedCapturedPhoto)
            {
                Destroy(_sharedCapturedPhoto);
            }
        }
    }
}