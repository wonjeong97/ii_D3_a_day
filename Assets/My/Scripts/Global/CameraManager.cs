using UnityEngine;

namespace My.Scripts.Global
{
    /// <summary>
    /// 웹캠 리소스를 전역에서 풀링하고 관리하는 매니저.
    /// WebCamTexture의 빈번한 생성과 파괴로 인한 메모리 단편화를 방지하기 위함.
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        public static CameraManager Instance { get; private set; }

        private WebCamTexture _sharedWebCamTexture;
        private Texture2D _sharedCapturedPhoto;

        private const int CamWidth = 1920;
        private const int CamHeight = 1080;
        private const int SaveWidth = 960;
        private const int SaveHeight = 1080;

        /// <summary>
        /// 싱글톤 인스턴스를 초기화함.
        /// </summary>
        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
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

            _sharedWebCamTexture = new WebCamTexture(devices[0].name, CamWidth, CamHeight);
            return _sharedWebCamTexture;
        }

        /// <summary>
        /// 촬영 데이터를 담을 전역 텍스처를 반환함.
        /// 캡처 시마다 새로운 Texture2D를 생성하지 않고 재사용하여 GC 부하를 줄이기 위함.
        /// </summary>
        public Texture2D GetSharedCapturedPhoto()
        {
            if (!_sharedCapturedPhoto || _sharedCapturedPhoto.width != SaveWidth || _sharedCapturedPhoto.height != SaveHeight)
            {
                if (_sharedCapturedPhoto) Destroy(_sharedCapturedPhoto);
                _sharedCapturedPhoto = new Texture2D(SaveWidth, SaveHeight, TextureFormat.RGBA32, false);
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