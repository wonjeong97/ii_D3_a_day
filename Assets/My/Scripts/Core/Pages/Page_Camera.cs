using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Hardware;
using My.Scripts.Network;
using My.Scripts.Utils;
using UnityEngine;
using UnityEngine.SceneManagement; 
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 웹캠 제어 및 사진 저장/합성을 담당하는 페이지 컨트롤러.
    /// 활성화된 씬(Step2, Step3)에 따라 웹캠 하드웨어 가동과 이미지 합성 로직을 명확히 분기함.
    /// </summary>
    public class Page_Camera : GamePage
    {
        [Header("Canvas Groups")]
        [Tooltip("안내 텍스트, 씬 텍스트, RawImage 프리뷰를 모두 포함하는 통합 CanvasGroup")]
        [SerializeField] private CanvasGroup mainContentCg;

        [Header("Dynamic UI Components")]
        [SerializeField] private Text textAnswerCompleteUI;
        [SerializeField] private Text textMySceneUI;
        [Tooltip("카메라 화면을 실시간으로 렌더링할 RawImage 컴포넌트")]
        [SerializeField] private RawImage previewRawImage;
        [Tooltip("3초 카운트다운을 표시할 텍스트 컴포넌트")]
        [SerializeField] private Text countdownTextUI;
        
        [Header("Save Settings")]
        [SerializeField] private string questionId;

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private CommonResultPageData _cachedData; 
        private bool _isCompleted;
        private Coroutine _sequenceCoroutine;
        private int _selectedAnswerIndex;
        
        /// <summary>
        /// 동기화 명령어 설정.
        /// 매니저 클래스(Step1~3Manager) 호출 호환성을 위해 인터페이스를 제공함.
        /// </summary>
        /// <param name="command">동기화 명령어 문자열.</param>
        public void SetSyncCommand(string command) { }

        /// <summary>
        /// 합성 시 필요한 답변 인덱스를 외부 매니저로부터 전달받음.
        /// </summary>
        /// <param name="index">1~5 사이의 답변 번호.</param>
        public void SetAnswerIndex(int index)
        {
            _selectedAnswerIndex = index;
        }

        /// <summary>
        /// 외부에서 문항 식별 코드를 설정함.
        /// 촬영 시 파일명 생성을 위한 구분자로 사용하기 위함.
        /// </summary>
        /// <param name="id">문항 식별자 문자열.</param>
        public void SetQuestionId(string id)
        {
            questionId = NormalizeQuestionId(id);
        }

        /// <summary>
        /// 식별자를 경로에 안전한 문자열로 정규화함.
        /// 경로 이탈 문자 및 유효하지 않은 파일명 문자를 제거하기 위함.
        /// </summary>
        private string NormalizeQuestionId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return string.Empty;
            string clean = id.Trim().Replace("..", "");
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                clean = clean.Replace(c.ToString(), "");
            }
            return clean;
        }
        
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
        /// 페이지 진입 시 연출을 초기화하고 Step2인 경우에만 카메라를 가동함.
        /// 저장이 필요 없는 구간(Step1)에서 불필요한 하드웨어 호출을 완벽히 차단하기 위함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;

            if (mainContentCg) mainContentCg.alpha = 0f;

            // 재진입 시 꺼져있던 씬 텍스트와 프리뷰 이미지를 다시 켜줌
            if (textMySceneUI) textMySceneUI.gameObject.SetActive(true);
            if (previewRawImage) previewRawImage.gameObject.SetActive(true);

            if (_cachedData == null)
            {
                return;
            }

            ApplyDataToUI();

            // Step1, 2, 3 모든 구간에서 카메라를 가동함
            if (CameraManager.Instance)
            {
                CameraManager.Instance.StartCamera();
                SetupCameraPreview();
            }
            
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.SendCommandToLight("LEDOn");
            }

            if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = StartCoroutine(SequenceRoutine());
        }
        
        /// <summary>
        /// 페이지 이탈 시 실행 중인 코루틴을 중단하고 카메라를 정지함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }

            if (CameraManager.Instance) CameraManager.Instance.StopCamera();
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
        /// CameraSetting에 저장된 크롭 영역 오프셋을 기반으로 RawImage의 uvRect를 설정하여 
        /// 화면에 실제 저장될 영역과 동일한 화면을 렌더링함.
        /// </summary>
        private void SetupCameraPreview()
        {
            if (!previewRawImage || !CameraManager.Instance) return;

            WebCamTexture camTex = CameraManager.Instance.GetWebCamTexture();
            if (camTex) previewRawImage.texture = camTex;

            CameraSetting camSet = CameraManager.Instance.setting;
            if (camSet == null) return;
            
            camSet.ValidateOrFix();
            if (camSet.camWidth <= 0 || camSet.camHeight <= 0) return;
            if (camSet.SaveWidth <= 0 || camSet.SaveHeight <= 0)
            {
                Debug.LogWarning($"[Page_Camera] 크롭 영역의 Width 또는 Height가 0 이하입니다. (SaveWidth: {camSet.SaveWidth}, SaveHeight: {camSet.SaveHeight})");
                return;
            }

            float fCamWidth = camSet.camWidth;
            float fCamHeight = camSet.camHeight;

            float startX = (fCamWidth / 2f) - camSet.cropLeft;
            float startY = (fCamHeight / 2f) - camSet.cropBottom;

            float uvX = startX / fCamWidth;
            float uvY = startY / fCamHeight;
            float uvW = camSet.SaveWidth / fCamWidth;
            float uvH = camSet.SaveHeight / fCamHeight;

            previewRawImage.uvRect = new Rect(uvX, uvY, uvW, uvH);

            // AspectRatioFitter가 남아있다면 제거하여 충돌을 방지함
            AspectRatioFitter fitter = previewRawImage.GetComponent<AspectRatioFitter>();
            if (fitter)
            {
                Destroy(fitter);
            }

            // Height를 580에 고정하고 설정된 크롭 비율에 맞춰 Width를 동적으로 설정함
            RectTransform rt = previewRawImage.rectTransform;
            float targetHeight = 580f;
            float targetWidth = targetHeight * ((float)camSet.SaveWidth / camSet.SaveHeight);
            
            rt.sizeDelta = new Vector2(targetWidth, targetHeight);
        }

        /// <summary>
        /// 답변 완료 안내, 촬영/합성 대기, 저장 연출을 순차적으로 수행함.
        /// 씬 이름(Step2, Step3)을 기준으로 캡처와 합성 로직을 명확하게 분기 처리하고, 어떠한 예외 상황이나 촬영 외의 스텝에서도 조명을 확실히 소등하기 위함.
        /// </summary>
        private IEnumerator SequenceRoutine()
        {
            WebCamTexture camTex = null;
            if (CameraManager.Instance)
            {
                camTex = CameraManager.Instance.GetWebCamTexture();
                if (camTex)
                {
                    // 카메라가 켜지고 화면 픽셀이 정상적으로 올라올 때까지 대기하여 페이드 인 시 깜빡임을 방지함
                    float waitTimeout = 2.0f;
                    while (camTex.width < 100 && waitTimeout > 0f)
                    {
                        waitTimeout -= Time.deltaTime;
                        yield return null;
                    }
                    // 프레임 안정화를 위한 추가 대기
                    yield return CoroutineData.GetWaitForSeconds(0.2f);
                }
            }

            if (countdownTextUI) countdownTextUI.gameObject.SetActive(false);

            // 텍스트와 프리뷰 화면을 한 번에 페이드 인
            if (mainContentCg) yield return StartCoroutine(FadeCanvasGroupRoutine(mainContentCg, 0f, 1f, fadeDuration));
            
            // 페이드 인 완료 후 2초 대기
            yield return CoroutineData.GetWaitForSeconds(2.0f);

            // 3초 카운트다운 연출
            if (countdownTextUI)
            {   
                countdownTextUI.gameObject.SetActive(true);
                for (int i = 3; i >= 1; i--)
                {
                    countdownTextUI.text = i.ToString();
                    
                    // 카운트가 2초 남았을 때 캡처 효과음 재생
                    if (i == 2 && SoundManager.Instance) 
                    {
                        SoundManager.Instance.PlaySFX("레고_4");
                    }
                    
                    yield return CoroutineData.GetWaitForSeconds(1.0f);
                }
                countdownTextUI.gameObject.SetActive(false);
            }

            // 카운트다운 완료 후 0.7초 대기 후 캡처
            yield return CoroutineData.GetWaitForSeconds(0.7f);

            bool isSuccess = true;
            string currentScene = SceneManager.GetActiveScene().name;
            bool isStep2 = currentScene == GameConstants.Scene.Step2;
            bool isStep3 = currentScene == GameConstants.Scene.Step3;

            if (isStep3)
            {
                PhotoCompositor compositor = FindFirstObjectByType<PhotoCompositor>();
                if (compositor)
                {
                    isSuccess = false;
                    yield return UniTask.ToCoroutine(async () => {
                        try
                        {
                            isSuccess = await compositor.ProcessAndSave(_selectedAnswerIndex, false);
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                            isSuccess = false;
                        }
                        finally
                        {
                            if (ArduinoManager.Instance)
                            {
                                ArduinoManager.Instance.SendCommandToLight("LEDOff");
                            }
                        }
                    });
                }
                else
                {
                    isSuccess = false;
                    if (ArduinoManager.Instance)
                    {
                        ArduinoManager.Instance.SendCommandToLight("LEDOff");
                    }
                }
            }
            else if (isStep2)
            {
                isSuccess = false;
                yield return UniTask.ToCoroutine(async () => {
                    isSuccess = await CapturePhotoAsync();
                });
            }
            else
            {
                if (ArduinoManager.Instance)
                {
                    ArduinoManager.Instance.SendCommandToLight("LEDOff");
                }
            }

            // 캡처가 완료된 순간 카메라 화면을 일시정지하여 텍스처를 고정시킴
            if (camTex && camTex.isPlaying)
            {
                camTex.Pause();
            }

            if (!isSuccess)
            {
                Debug.LogWarning("[Page_Camera] 캡처 또는 업로드에 실패했으나, 진행을 위해 무시합니다.");
                if (ArduinoManager.Instance)
                {
                    ArduinoManager.Instance.SendCommandToLight("LEDOff");
                }
            }
            
            yield return CoroutineData.GetWaitForSeconds(0.5f);

            // Step2일 경우, 캡처가 완료된 이 시점에 서브 캔버스 배경을 미리 페이드 아웃 시킴
            if (isStep2 && _04_Step2.Step2Manager.Instance)
            {
                _04_Step2.Step2Manager.Instance.FadeOutBackground();
            }

            if (mainContentCg) yield return StartCoroutine(FadeCanvasGroupRoutine(mainContentCg, 1f, 0f, fadeDuration));
            else yield return CoroutineData.GetWaitForSeconds(fadeDuration);

            // 페이드 아웃되어 안 보이는 틈을 타서 불필요해진 오브젝트 비활성화
            if (textMySceneUI) textMySceneUI.gameObject.SetActive(false);
            if (previewRawImage) previewRawImage.gameObject.SetActive(false);

            SetUIText(textAnswerCompleteUI, _cachedData.textPhotoSaved);
            
            if (mainContentCg) yield return StartCoroutine(FadeCanvasGroupRoutine(mainContentCg, 0f, 1f, fadeDuration));
            else yield return CoroutineData.GetWaitForSeconds(fadeDuration);

            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_12");
            yield return CoroutineData.GetWaitForSeconds(2.5f);

            if (!_isCompleted) CompletePage();
        }

        /// <summary>
        /// 공유 웹캠 텍스처를 이용하여 실제 사진을 캡처하고 저장을 요청함.
        /// 예외 발생이나 조기 반환 시에도 켜져 있는 조명을 반드시 끄도록 보장하기 위함.
        /// </summary>
        private async UniTask<bool> CapturePhotoAsync()
        {
            bool isLedTurnedOff = false;
            try
            {
                if (!CameraManager.Instance) return false;
                
                WebCamTexture cam = CameraManager.Instance.GetWebCamTexture();
                if (!cam || !cam.isPlaying) return false;

                CameraSetting camSet = CameraManager.Instance.setting;
                if (camSet == null) return false;
                
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

                    if (ArduinoManager.Instance)
                    {
                        ArduinoManager.Instance.SendCommandToLight("LEDOff");
                        isLedTurnedOff = true;
                    }
                    
                    return await SavePhotoAsync(photo);
                }
                finally 
                { 
                    RenderTexture.active = prev;
                    if (rt) RenderTexture.ReleaseTemporary(rt); 
                }
            }
            catch 
            { 
                return false; 
            }
            finally
            {
                if (!isLedTurnedOff && ArduinoManager.Instance)
                {
                    ArduinoManager.Instance.SendCommandToLight("LEDOff");
                }
            }
        }

        /// <summary>
        /// 텍스처를 PNG로 인코딩하여 로컬에 저장하고 서버 업로드를 시도함.
        /// </summary>
        private async UniTask<bool> SavePhotoAsync(Texture2D photo)
        {
            if (!photo) return false;
            
            byte[] rawData = photo.GetRawTextureData();
            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;
            
            string roleString = isServer ? "Left" : "Right";
            int userIdx = SessionManager.Instance ? SessionManager.Instance.CurrentUserIdx : 0;
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");

            string safeQuestionId = NormalizeQuestionId(questionId);
            if (string.IsNullOrWhiteSpace(safeQuestionId))
            {
                Debug.LogError($"{nameof(Page_Camera)}: questionId is not set or invalid.");
                return false;
            }
            string relativePath = $"{dateStr}/{userIdx}/{roleString}/{userIdx}_{roleString}_{safeQuestionId}.png";

            try
            {
                int width = photo.width;
                int height = photo.height;
                UnityEngine.Experimental.Rendering.GraphicsFormat format = photo.graphicsFormat;
                
                byte[] bytes = await UniTask.RunOnThreadPool(() => ImageConversion.EncodeArrayToPNG(rawData, format, (uint)width, (uint)height));
                
                if (FileTransferManager.Instance) 
                {
                    return await FileTransferManager.Instance.UploadPhotoAsync(bytes, relativePath);
                }
                return false;
            }
            catch 
            { 
                return false; 
            }
        }

        /// <summary>
        /// 캔버스 그룹의 알파값을 조절하여 페이드 효과를 연출함.
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
        /// 완료 플래그를 세우고 매니저에게 다음 흐름으로 전환할 것을 알림.
        /// </summary>
        private void CompletePage()
        {
            if (_isCompleted) return;
            _isCompleted = true;
            if (onStepComplete != null) onStepComplete.Invoke(0);
        }
    }
}