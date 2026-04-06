using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using My.Scripts.Network;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._06_PlayVideo
{   
    /// <summary>
    /// JSON에서 로드되는 PlayVideo 씬의 텍스트 데이터 구조체.
    /// </summary>
    [Serializable]
    public class PlayVideoSetting
    {
        public TextSetting mainText;
        public TextSetting leftText;
        public TextSetting rightText;
    }
    
    /// <summary>
    /// 양쪽 PC에서 촬영된 사진들을 불러와 메인 화면 및 모자이크 연출을 수행하는 매니저.
    /// 애니메이션과 연동되어 사진을 주기적으로 교체하며 연출 종료 시 동기화 후 엔딩 씬으로 전환함.
    /// </summary>
    public class PlayVideoManager : MonoBehaviour
    {   
        public static PlayVideoManager Instance;
        
        [Header("UI Text Components")]
        [SerializeField] private Text mainTextUI;
        [SerializeField] private Text leftTextUI;
        [SerializeField] private Text rightTextUI;
        
        [Header("Main View Components")]
        [SerializeField] private Image leftMainImage;
        [SerializeField] private Image rightMainImage;

        [Header("Animation Settings")]
        [SerializeField] private float appearanceInterval = 0.05f;
        [SerializeField] private float photoChangeInterval = 0.5f;

        [Header("Mosaic Images (Still Cuts)")]
        [SerializeField] private Image[] leftTargetImages;
        [SerializeField] private Image[] rightTargetImages;
        [SerializeField] private int totalPhotos = 15;

        [Header("UI Animator")]
        [SerializeField] private Animator uiAnimator;
        
        [Header("Film Animation UI")]
        [SerializeField] private CanvasGroup filmCanvasGroup;

        private readonly List<Sprite> _myLoadedSprites = new List<Sprite>();
        private readonly List<Sprite> _otherLoadedSprites = new List<Sprite>();
        private readonly static int FilmTrigger = Animator.StringToHash("Film");
        
        private bool _isAnimationStarted;
        private bool _isLocalVideoFinished;
        private bool _isRemoteVideoFinished;
        
        private Coroutine _filmFadeCoroutine;

        /// <summary>
        /// 싱글톤 패턴 초기화 및 기존 인스턴스 파괴.
        /// </summary>
        private void Awake()
        {
            if (!Instance) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        /// <summary>
        /// 초기 UI 상태를 설정하고 비동기 사진 로드를 시작함.
        /// 로딩 완료 전 애니메이션이 재생되는 것을 막기 위해 애니메이터를 일시 정지함.
        /// </summary>
        private void Start()
        {   
            if (filmCanvasGroup) filmCanvasGroup.alpha = 0f;
            if (uiAnimator) uiAnimator.enabled = false;

            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
            }

            LoadSettings();
            SetupImagesUI();
            LoadAndPrepareAsync().Forget();
        }
        
        /// <summary>
        /// 외부 JSON 파일에서 설정 데이터를 로드하여 UI 텍스트 등에 할당함.
        /// </summary>
        private void LoadSettings()
        {
            PlayVideoSetting setting = JsonLoader.Load<PlayVideoSetting>(GameConstants.Path.PlayVideo);

            if (setting != null)
            {
                if (setting.mainText != null && mainTextUI)
                {
                    if (UIManager.Instance) UIManager.Instance.SetText(mainTextUI.gameObject, setting.mainText);
                    mainTextUI.text = setting.mainText.text;
                }

                if (setting.leftText != null && leftTextUI)
                {
                    if (UIManager.Instance) UIManager.Instance.SetText(leftTextUI.gameObject, setting.leftText);
                    leftTextUI.text = ProcessPlayerName(setting.leftText.text, true);
                }

                if (setting.rightText != null && rightTextUI)
                {
                    if (UIManager.Instance) UIManager.Instance.SetText(rightTextUI.gameObject, setting.rightText);
                    rightTextUI.text = ProcessPlayerName(setting.rightText.text, false);
                }
            }
            else
            {
                Debug.LogWarning("[PlayVideoManager] JSON/PlayVideo 로드 실패. 설정값을 찾을 수 없습니다.");
            }
        }
        
        /// <summary>
        /// 서버/클라이언트 역할에 따라 좌우 UI의 텍스트 플레이스홀더를 실제 이름으로 치환함.
        /// </summary>
        private string ProcessPlayerName(string rawText, bool isLeftUI)
        {
            if (string.IsNullOrEmpty(rawText)) return rawText;

            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

            string lastNameA = SessionManager.Instance ? SessionManager.Instance.PlayerALastName : "";
            string firstNameA = SessionManager.Instance ? SessionManager.Instance.PlayerAFirstName : "";
            string lastNameB = SessionManager.Instance ? SessionManager.Instance.PlayerBLastName : "";
            string firstNameB = SessionManager.Instance ? SessionManager.Instance.PlayerBFirstName : "";

            // 위치에 따라 본인과 상대방의 이름을 유동적으로 결정함
            string targetLastName = "";
            string targetFirstName = "";

            if (isServer)
            {
                targetLastName = isLeftUI ? lastNameA : lastNameB;
                targetFirstName = isLeftUI ? firstNameA : firstNameB;
            }
            else
            {
                targetLastName = isLeftUI ? lastNameB : lastNameA;
                targetFirstName = isLeftUI ? firstNameB : firstNameA;
            }

            // JSON에서 LEFT/RIGHT 중 어느 태그를 썼더라도 해당 UI 방향에 맞는 유저 데이터로 강제 덮어씌워 보장함
            return rawText
                .Replace("{RESERVATION_LAST_NAME_LEFT}", targetLastName)
                .Replace("{RESERVATION_FIRST_NAME_LEFT}", targetFirstName)
                .Replace("{RESERVATION_LAST_NAME_RIGHT}", targetLastName)
                .Replace("{RESERVATION_FIRST_NAME_RIGHT}", targetFirstName);
        }

        /// <summary>
        /// 우측 영역 이미지들의 X축 스케일을 반전시킴.
        /// 마주보는 물리적 기기 구조에서 좌우 대칭 연출을 구현하기 위함.
        /// </summary>
        private void SetupImagesUI()
        {
            if (rightMainImage)
            {
                Vector3 scale = rightMainImage.rectTransform.localScale;
                scale.x = -Mathf.Abs(scale.x);
                rightMainImage.rectTransform.localScale = scale;
            }

            if (rightTargetImages != null)
            {
                for (int i = 0; i < rightTargetImages.Length; i++)
                {
                    Image img = rightTargetImages[i];
                    if (img)
                    {
                        Vector3 scale = img.rectTransform.localScale;
                        scale.x = -Mathf.Abs(scale.x);
                        img.rectTransform.localScale = scale;
                    }
                }
            }
        }

        /// <summary>
        /// 사진 로드 및 스프라이트 할당 완료 후 애니메이션을 재생함.
        /// 리소스가 준비되지 않은 상태에서 빈 화면 연출이 시작되는 것을 방지함.
        /// </summary>
        private async UniTaskVoid LoadAndPrepareAsync()
        {
            await LoadPhotosAsSpritesAsync();
            AssignInitialSprites();

            if (uiAnimator)
            {
                uiAnimator.enabled = true;
                uiAnimator.SetTrigger(FilmTrigger);
            }
        }

        /// <summary>
        /// 서버 클라이언트 역할에 맞춰 본인과 상대방의 사진 경로를 파악하고 메모리에 로드함.
        /// </summary>
        private async UniTask LoadPhotosAsSpritesAsync()
        {
            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

            string myRole = isServer ? "Left" : "Right";
            string otherRole = isServer ? "Right" : "Left";
            
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            string userIdx = SessionManager.Instance ? SessionManager.Instance.CurrentUserIdx.ToString() : "0";
            
            string myRelativePath = $"{dateStr}/{userIdx}/{myRole}";
            string otherRelativePath = $"{dateStr}/{userIdx}/{otherRole}";

            await LoadRolePhotosAsync(myRole, myRelativePath, _myLoadedSprites);
            await LoadRolePhotosAsync(otherRole, otherRelativePath, _otherLoadedSprites);
        }

        /// <summary>
        /// 저장소 또는 통신을 통해 특정 역할의 사진 데이터를 스프라이트로 변환하여 리스트에 추가함.
        /// </summary>
        private async UniTask LoadRolePhotosAsync(string role, string relativeFolder, List<Sprite> targetList)
        {
            string userIdx = SessionManager.Instance ? SessionManager.Instance.CurrentUserIdx.ToString() : "0";

            for (int i = 1; i <= totalPhotos; i++)
            {
                string fileName = $"{userIdx}_{role}_Q{i}.png"; 
                string fileRelativePath = $"{relativeFolder}/{fileName}";

                if (FileTransferManager.Instance)
                {
                    byte[] fileData = await FileTransferManager.Instance.DownloadPhotoAsync(fileRelativePath);
                    if (fileData != null)
                    {
                        Texture2D tex = new Texture2D(2, 2);
                        if (tex.LoadImage(fileData))
                        {
                            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                            targetList.Add(sprite);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 애니메이션 시작 전 초기 화면에 보여질 기본 사진들을 각 UI 컴포넌트에 할당함.
        /// </summary>
        private void AssignInitialSprites()
        {
            if (_myLoadedSprites.Count > 0)
            {
                if (leftMainImage) leftMainImage.sprite = _myLoadedSprites[0];
                AssignSpritesToTargets(leftTargetImages, _myLoadedSprites);
            }

            if (_otherLoadedSprites.Count > 0)
            {
                if (rightMainImage) rightMainImage.sprite = _otherLoadedSprites[0];
                AssignSpritesToTargets(rightTargetImages, _otherLoadedSprites);
            }
        }

        /// <summary>
        /// 모자이크 그룹별로 다른 사진이 보이도록 인덱스를 분산하여 할당함.
        /// 인덱스 연산 예시 15장 기준 2, 7, 12번 인덱스가 각 그룹의 초기값으로 지정됨.
        /// </summary>
        private void AssignSpritesToTargets(Image[] targetImages, List<Sprite> sprites)
        {
            if (targetImages == null || sprites.Count == 0) return;

            int spriteCount = sprites.Count;
            int[] currentIndices = new int[3];
            currentIndices[0] = 2 % spriteCount;
            currentIndices[1] = 7 % spriteCount;
            currentIndices[2] = 12 % spriteCount;

            int globalIndex = 0;

            for (int i = 0; i < targetImages.Length; i++)
            {
                Image img = targetImages[i];
                if (img) img.sprite = sprites[currentIndices[globalIndex % 3]];
                globalIndex++;
            }
        }

        /// <summary>
        /// 애니메이션 타임라인 이벤트에 의해 호출되어 사진 교체 코루틴을 실행함.
        /// 중복 실행을 막고 씬 종료 시 자동으로 코루틴이 취소되도록 토큰을 주입함.
        /// </summary>
        public void StartVideoAnimation()
        {
            if (_isAnimationStarted) return;
            if (_myLoadedSprites.Count == 0 && _otherLoadedSprites.Count == 0) return;

            _isAnimationStarted = true;
            CancellationToken token = this.GetCancellationTokenOnDestroy();

            UpdateMainImagesAsync(token).Forget(); 
            PlayMosaicSequence(token); 
        }

        /// <summary>
        /// 애니메이션 타임라인 종료 시 호출되어 상대방 PC에 완료 신호를 발송함.
        /// </summary>
        public void MoveToEndingScene()
        {
            _isLocalVideoFinished = true;
            
            if (TcpManager.Instance)
            {
                TcpManager.Instance.SendMessageToTarget("PLAYVIDEO_COMPLETE", "");
            }
            
            CheckSyncAndChangeScene();
        }

        /// <summary>
        /// 상대방의 영상 재생 완료 신호를 수신하고 동기화 상태를 갱신함.
        /// </summary>
        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == "PLAYVIDEO_COMPLETE")
            {
                _isRemoteVideoFinished = true;
                CheckSyncAndChangeScene();
            }
        }

        /// <summary>
        /// 양쪽 PC 모두 재생을 완료했는지 확인하고 엔딩 씬으로 전환함.
        /// </summary>
        private void CheckSyncAndChangeScene()
        {
            if (_isLocalVideoFinished && _isRemoteVideoFinished)
            {
                if (TcpManager.Instance)
                {
                    TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
                }

                if (GameManager.Instance)
                {
                    GameManager.Instance.ChangeScene(GameConstants.Scene.Ending, true);
                }
            }
        }

        /// <summary>
        /// 메인 화면의 큰 이미지를 설정된 간격마다 다음 사진으로 교체함.
        /// 슬라이드쇼 형태의 시각적 피드백을 제공하기 위함.
        /// </summary>
        private async UniTaskVoid UpdateMainImagesAsync(CancellationToken token)
        {
            if (!leftMainImage && !rightMainImage) return;

            int myCurrentIndex = 0;
            int otherCurrentIndex = 0;

            while (!token.IsCancellationRequested)
            {
                if (_myLoadedSprites.Count > 0 && leftMainImage)
                {
                    leftMainImage.sprite = _myLoadedSprites[myCurrentIndex];
                    myCurrentIndex = (myCurrentIndex + 1) % _myLoadedSprites.Count;
                }

                if (_otherLoadedSprites.Count > 0 && rightMainImage)
                {
                    rightMainImage.sprite = _otherLoadedSprites[otherCurrentIndex];
                    otherCurrentIndex = (otherCurrentIndex + 1) % _otherLoadedSprites.Count;
                }
                
                bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(photoChangeInterval), cancellationToken: token).SuppressCancellationThrow();
                if (isCanceled) break;
            }
        }

        /// <summary>
        /// 다수의 작은 사진들을 3개의 그룹으로 나누어 순차적 노출 및 교체 연출을 시작함.
        /// </summary>
        private void PlayMosaicSequence(CancellationToken token)
        {
            List<Image>[] leftImageGroups = new List<Image>[3];
            List<Image>[] rightImageGroups = new List<Image>[3];

            for (int i = 0; i < 3; i++)
            {
                leftImageGroups[i] = new List<Image>();
                rightImageGroups[i] = new List<Image>();
            }

            PrepareMosaicGroups(leftTargetImages, leftImageGroups);
            PrepareMosaicGroups(rightTargetImages, rightImageGroups);

            TurnOnImagesSequentiallyAsync(token).Forget();

            if (_myLoadedSprites.Count > 0) UpdateMosaicSpritesAsync(leftImageGroups, _myLoadedSprites, token).Forget();
            if (_otherLoadedSprites.Count > 0) UpdateMosaicSpritesAsync(rightImageGroups, _otherLoadedSprites, token).Forget();
        }

        /// <summary>
        /// 초기 모자이크 이미지들의 투명도를 0으로 설정하고 순번에 맞춰 3개의 그룹으로 분배함.
        /// </summary>
        private void PrepareMosaicGroups(Image[] targetImages, List<Image>[] groups)
        {
            if (targetImages == null) return;

            int globalIndex = 0;
            for (int i = 0; i < targetImages.Length; i++)
            {
                Image img = targetImages[i];
                if (!img) continue;

                img.color = new Color(img.color.r, img.color.g, img.color.b, 0f);
                groups[globalIndex % 3].Add(img);
                globalIndex++;
            }
        }

        /// <summary>
        /// 투명하게 숨겨진 모자이크 이미지들을 짧은 간격으로 하나씩 나타나게 함.
        /// 단조로운 연출을 피하고 역동적인 화면 구성을 제공하기 위함.
        /// </summary>
        private async UniTaskVoid TurnOnImagesSequentiallyAsync(CancellationToken token)
        {
            List<Image> allTargets = new List<Image>();
            if (leftTargetImages != null) allTargets.AddRange(leftTargetImages);
            if (rightTargetImages != null) allTargets.AddRange(rightTargetImages);

            for (int i = 0; i < allTargets.Count; i++)
            {
                Image img = allTargets[i];
                if (!img) continue;

                img.color = new Color(img.color.r, img.color.g, img.color.b, 1f);

                if (appearanceInterval > 0f)
                {
                    bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(appearanceInterval), cancellationToken: token).SuppressCancellationThrow();
                    if (isCanceled) break;
                }
            }
        }

        /// <summary>
        /// 3개의 모자이크 그룹에 할당된 사진들을 설정된 주기마다 순환시킴.
        /// </summary>
        private async UniTaskVoid UpdateMosaicSpritesAsync(List<Image>[] imageGroups, List<Sprite> sprites, CancellationToken token)
        {
            if (sprites.Count == 0) return;

            int spriteCount = sprites.Count;
            int[] currentIndices = new int[3];
            currentIndices[0] = 2 % spriteCount;
            currentIndices[1] = 7 % spriteCount;
            currentIndices[2] = 12 % spriteCount;

            while (!token.IsCancellationRequested)
            {
                for (int g = 0; g < 3; g++)
                {
                    Sprite targetSprite = sprites[currentIndices[g]];
                    List<Image> group = imageGroups[g];

                    for (int i = 0; i < group.Count; i++)
                    {
                        Image img = group[i];
                        if (img) img.sprite = targetSprite;
                    }
                    currentIndices[g] = (currentIndices[g] + 1) % spriteCount;
                }
                bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(photoChangeInterval), cancellationToken: token).SuppressCancellationThrow();
                if (isCanceled) break;
            }
        }

        /// <summary>
        /// 씬 종료 시 네트워크 이벤트를 해제하고 텍스처 메모리를 완전히 반환함.
        /// </summary>
        private void OnDestroy()
        {
            if (TcpManager.Instance) TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;

            if (_filmFadeCoroutine != null)
            {
                StopCoroutine(_filmFadeCoroutine);
                _filmFadeCoroutine = null;
            }
            
            ClearSprites(_myLoadedSprites);
            ClearSprites(_otherLoadedSprites);
        }

        /// <summary>
        /// 메모리에 동적 생성된 스프라이트와 텍스처를 파괴함.
        /// 대용량 이미지 로드로 인한 메모리 릭을 방지하기 위함.
        /// </summary>
        private void ClearSprites(List<Sprite> sprites)
        {
            foreach (Sprite sprite in sprites)
            {
                if (sprite && sprite.texture)
                {
                    Destroy(sprite.texture);
                    Destroy(sprite);
                }
            }
            sprites.Clear();
        }
        
        /// <summary>
        /// 애니메이션 타임라인 이벤트에서 호출되어 캔버스 그룹을 페이드 인 시킴.
        /// 외부 타임라인의 흐름에 맞춰 시각적 요소를 부드럽게 노출하기 위함.
        /// </summary>
        /// <param name="duration">페이드 인에 소요되는 시간.</param>
        public void FadeInFilmTextCanvas(float duration)
        {
            if (!filmCanvasGroup) return;
            
            if (_filmFadeCoroutine != null) StopCoroutine(_filmFadeCoroutine);
            _filmFadeCoroutine = StartCoroutine(FadeCanvasGroupRoutine(filmCanvasGroup, 0f, 1f, duration));
        }

        /// <summary>
        /// 지정된 시간 동안 캔버스 그룹의 알파값을 선형 보간함.
        /// </summary>
        /// <param name="target">대상 캔버스 그룹.</param>
        /// <param name="start">시작 알파값.</param>
        /// <param name="end">목표 알파값.</param>
        /// <param name="duration">진행 시간.</param>
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
    }
}