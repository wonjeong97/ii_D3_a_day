using System;
using System.IO;
using Cysharp.Threading.Tasks;
using My.Scripts.Global;
using My.Scripts.Network;
using My.Scripts.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace My.Scripts.Utils
{
    /// <summary> 
    /// 지정된 답변 데이터(텍스트)와 어드레서블 이미지(얼굴)를 UI 캔버스에 구성한 뒤,
    /// 숨겨진 카메라로 렌더링(캡처)하여 로컬에 PNG로 저장하고 서버로 업로드하는 시퀀스를 관리합니다.
    /// 텍스트 렌더링 한계를 극복하기 위해 UI 캡처 방식과 비동기 파일 입출력 구조를 결합했습니다.
    /// </summary>
    public class PhotoCompositor : MonoBehaviour
    {
        [Header("Synthesis UI Components (Hidden Canvas)")]
        [Tooltip("실제 화면에는 보이지 않는 별도 레이어(예: Hidden)에 배치된 캔버스입니다.")]
        [SerializeField] private Canvas synthesisCanvas;
        [SerializeField] private Camera captureCamera;
        
        [Header("UI Elements")]
        [SerializeField] private Image baseFrameImage;
        [SerializeField] private Image faceImage;
        [SerializeField] private Text questionText; 
        [SerializeField] private Text answerText;   

        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries;
        [SerializeField] private float retryDelay;

        public bool IsProcessing { get; private set; }

        private AsyncOperationHandle<Sprite> _faceImageHandle;

        // 인덱스 1~5번에 매칭되는 Step3 응답 텍스트 배열
        private readonly string[] _answerStrings = new string[] 
        { 
            "", "행복", "불만가득", "신남", "모르겠음", "속상함" 
        };

        [ContextMenu("Execute Composite Now")] 
        public void DebugProcessAndSave()
        {
            // 인스펙터 테스트용 (디버그 모드로 로컬 저장만 수행)
            ProcessAndSave(1, true).Forget();
        }

        /// <summary> 
        /// Step3의 합성 로직을 외부(Page 등)에서 호출할 때 사용하는 진입점입니다.
        /// 세션 정보를 기반으로 파일명이 자동 완성되므로 인덱스와 디버그 여부만 전달받습니다.
        /// </summary>
        /// <param name="answerIndex">유저가 선택한 1~5번 사이의 응답 인덱스.</param>
        /// <param name="isDebug">true일 경우 서버 업로드를 생략함.</param>
        public async UniTask<bool> ProcessAndSave(int answerIndex, bool isDebug)
        {
            if (!captureCamera || !synthesisCanvas)
            {
                Debug.LogError("[PhotoCompositor] 합성 캔버스 또는 카메라 컴포넌트 누락");
                return false;
            }

            IsProcessing = true;
            return await ExecuteCompositeAsync(answerIndex, isDebug);
        }

        /// <summary> 
        /// UI 세팅, 렌더링, 인코딩, 파일 저장을 비동기로 순차 처리합니다. 
        /// 메인 스레드 프리징을 최소화하기 위함.
        /// </summary>
        private async UniTask<bool> ExecuteCompositeAsync(int answerIndex, bool isDebug)
        {
            bool isServer = false;
            if (TcpManager.Instance)
            {
                isServer = TcpManager.Instance.IsServer;
            }

            int idxUser = 0;
            string uid = "UnknownUID";
            string moduleCode = "D3";

            if (SessionManager.Instance)
            {
                idxUser = SessionManager.Instance.CurrentUserIdx;
                uid = isServer ? SessionManager.Instance.PlayerAUid : SessionManager.Instance.PlayerBUid;
                
                if (!string.IsNullOrEmpty(SessionManager.Instance.CurrentModuleCode))
                {
                    moduleCode = SessionManager.Instance.CurrentModuleCode;
                }
            }

            string rootPath = GetRootPath(idxUser, isServer);
            string finalFileName = $"{idxUser}_{uid}_{moduleCode}.png";

            RenderTexture rt = null;
            Texture2D resultTex = null;

            try
            {
                // 1. 텍스트 세팅
                SetupTexts(isServer, answerIndex);

                // 2. 얼굴 이미지 비동기 로드
                bool isImageLoaded = await LoadFaceImageAsync(isServer, answerIndex);
                if (!isImageLoaded)
                {
                    Debug.LogWarning("[PhotoCompositor] 얼굴 이미지를 불러오지 못해 기본 상태로 합성을 진행합니다.");
                }

                // 3. UI 변경사항 강제 렌더링 대기
                Canvas.ForceUpdateCanvases();
                await UniTask.Yield(PlayerLoopTiming.Update);

                // 4. RenderTexture를 이용한 UI 캡처 (고정 해상도 1000x1367)
                int targetWidth = 1000;
                int targetHeight = 1367;

                rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 24, RenderTextureFormat.ARGB32);
                RenderTexture prevActive = RenderTexture.active;
                
                captureCamera.targetTexture = rt;
                captureCamera.Render();
                captureCamera.targetTexture = null;

                RenderTexture.active = rt;

                resultTex = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
                resultTex.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
                resultTex.Apply();

                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                rt = null;

                // 5. 텍스처 데이터를 백그라운드 스레드에서 PNG로 무손실 인코딩
                byte[] rawData = resultTex.GetRawTextureData();
                int texWidth = resultTex.width;
                int texHeight = resultTex.height;
                UnityEngine.Experimental.Rendering.GraphicsFormat format = resultTex.graphicsFormat;

                byte[] pngBytes = await UniTask.RunOnThreadPool(() => 
                {
                    return ImageConversion.EncodeArrayToPNG(rawData, format, (uint)texWidth, (uint)texHeight);
                });

                if (pngBytes == null || pngBytes.Length == 0)
                {
                    Debug.LogError("[PhotoCompositor] PNG 인코딩 실패");
                    return false;
                }

                // 6. 로컬 디스크 비동기 쓰기 (Step2 사진들과 동일한 폴더 경로에 저장)
                if (!Directory.Exists(rootPath))
                {
                    Directory.CreateDirectory(rootPath);
                }

                await File.WriteAllBytesAsync(Path.Combine(rootPath, finalFileName), pngBytes);

                // 7. 서버 업로드
                if (!isDebug)
                {
                    return await UploadImageAsync(pngBytes, idxUser, uid, moduleCode);
                }
                else
                {
                    Debug.Log($"<color=cyan>[PhotoCompositor] 디버그 모드 완료: {finalFileName} 생성됨.</color>");
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PhotoCompositor] 합성 중 예외: {e.Message}");
                return false;
            }
            finally
            {
                if (resultTex) Destroy(resultTex);
                if (rt) RenderTexture.ReleaseTemporary(rt);
                if (_faceImageHandle.IsValid()) Addressables.Release(_faceImageHandle);
                
                IsProcessing = false; 
            }
        }

        /// <summary>
        /// 서버/클라이언트 역할에 맞게 질문 텍스트의 이름을 치환하고 선택한 답변을 기입함.
        /// </summary>
        private void SetupTexts(bool isServer, int answerIndex)
        {
            if (questionText)
            {
                string rawText = isServer 
                    ? "Q. 오늘 {nameB}님의 기분은 어때 보이나요?" 
                    : "Q. 오늘 {nameA}님의 기분은 어때 보이나요?";
                
                questionText.text = UIUtils.ReplacePlayerNamePlaceholders(rawText);
            }

            if (answerText)
            {
                if (answerIndex >= 1 && answerIndex <= 5)
                {
                    answerText.text = _answerStrings[answerIndex];
                }
                else
                {
                    answerText.text = "알 수 없음";
                }
            }
        }

        /// <summary>
        /// 어드레서블 시스템에서 네트워크 역할에 맞는 레고 결과 이미지를 비동기로 불러옴.
        /// 불러온 이미지는 SetNativeSize()를 통해 원래 해상도 크기로 자동 맞춤됨.
        /// </summary>
        private async UniTask<bool> LoadFaceImageAsync(bool isServer, int answerIndex)
        {
            string roleStr = isServer ? "Server" : "Client";
            int safeIndex = Mathf.Clamp(answerIndex, 1, 5);
            string key = $"Lego_Result_{roleStr}_{safeIndex}";

            try
            {
                _faceImageHandle = Addressables.LoadAssetAsync<Sprite>(key);
                Sprite faceSprite = await _faceImageHandle.Task.AsUniTask();

                if (faceImage && faceSprite)
                {
                    faceImage.sprite = faceSprite;
                    faceImage.SetNativeSize(); 
                    return true;
                }
                
                if (faceImage)
                {
                    faceImage.sprite = null;
                }
                if (_faceImageHandle.IsValid()) Addressables.Release(_faceImageHandle);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PhotoCompositor] 어드레서블 로드 에러 ({key}): {e.Message}");
                if (faceImage)
                {
                    faceImage.sprite = null;
                }
                if (_faceImageHandle.IsValid()) Addressables.Release(_faceImageHandle);
            }
            return false;
        }

        /// <summary> 
        /// 합성된 PNG 바이트 데이터를 서버 API를 통해 업로드함.
        /// </summary>
        private async UniTask<bool> UploadImageAsync(byte[] imageBytes, int idxUser, string uid, string moduleCode)
        {
            string baseUrl = string.Empty;

            if (GameManager.Instance && GameManager.Instance.ApiConfig != null)
            {
                baseUrl = GameManager.Instance.ApiConfig.UploadFileUrl;
            }

            if (string.IsNullOrEmpty(baseUrl) || idxUser <= 0 || string.IsNullOrWhiteSpace(uid)) return false;
            
            string encodedUid = UnityWebRequest.EscapeURL(uid);
            string url = $"{baseUrl}?idx_user={idxUser}&uid={encodedUid}&code={moduleCode}&type=png";
            
            int attempts = Mathf.Max(1, maxRetries);
            float delaySeconds = Mathf.Max(0f, retryDelay);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                using (UnityWebRequest webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(imageBytes);
                    webRequest.uploadHandler.contentType = "image/png"; 
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.timeout = 15;

                    await webRequest.SendWebRequest().ToUniTask();

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log($"[PhotoCompositor] 업로드 성공: {webRequest.responseCode}");
                        return true; 
                    }

                    if (attempt < attempts - 1)
                    {
                        Debug.LogWarning($"[PhotoCompositor] 업로드 실패 ({attempt + 1}/{attempts}): {webRequest.error}. {delaySeconds}초 후 재시도...");
                        await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds));
                    }
                    else
                    {
                        Debug.LogError($"[PhotoCompositor] 업로드 최종 실패: {webRequest.error}");
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 파일 저장을 위한 루트 디렉토리 경로를 생성함.
        /// 다른 Step2 사진들이 저장되는 곳과 완벽히 동일한 구조(날짜/유저인덱스/역할)를 따르도록 구성함.
        /// </summary>
        private string GetRootPath(int userIdx, bool isServer)
        {
            string baseFolder = @"C:\UnitySharedPicture";
            
            if (FileTransferManager.Instance && !string.IsNullOrWhiteSpace(FileTransferManager.Instance.localSaveRoot))
            {
                baseFolder = FileTransferManager.Instance.localSaveRoot;
            }
            
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            string roleStr = isServer ? "Left" : "Right";
            
            return Path.Combine(baseFolder, dateStr, userIdx.ToString(), roleStr);
        }
    }
}