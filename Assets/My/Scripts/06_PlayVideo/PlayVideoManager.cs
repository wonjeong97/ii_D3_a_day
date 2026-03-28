using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using My.Scripts.Network;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks; 

namespace My.Scripts._06_PlayVideo
{
    public class PlayVideoManager : MonoBehaviour
    {   
        public static PlayVideoManager Instance;
        
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

        private readonly List<Sprite> _myLoadedSprites = new List<Sprite>();
        private readonly List<Sprite> _otherLoadedSprites = new List<Sprite>();
        private readonly static int FilmTrigger = Animator.StringToHash("Film");
        private bool _isAnimationStarted;
        
        // 동기화를 위한 변수 추가
        private bool _isLocalVideoFinished = false;
        private bool _isRemoteVideoFinished = false;

        private void Awake()
        {
            if (!Instance) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void Start()
        {
            if (uiAnimator) uiAnimator.enabled = false;

            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
            }

            SetupImagesUI();
            LoadAndPrepareAsync().Forget();
        }

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

        private async UniTask LoadPhotosAsSpritesAsync()
        {
            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;

            string myRole = isServer ? "Left" : "Right";
            string otherRole = isServer ? "Right" : "Left";
            
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            string userIdx = SessionManager.Instance ? SessionManager.Instance.CurrentUserIdx.ToString() : "0";
            
            // 날짜 폴더가 포함된 상대 경로
            string myRelativePath = $"{dateStr}/{userIdx}/{myRole}";
            string otherRelativePath = $"{dateStr}/{userIdx}/{otherRole}";

            await LoadRolePhotosAsync(myRole, myRelativePath, _myLoadedSprites);
            await LoadRolePhotosAsync(otherRole, otherRelativePath, _otherLoadedSprites);
        }

        private async UniTask LoadRolePhotosAsync(string role, string relativeFolder, List<Sprite> targetList)
        {
            string userIdx = SessionManager.Instance ? SessionManager.Instance.CurrentUserIdx.ToString() : "0";

            for (int i = 1; i <= totalPhotos; i++)
            {
                // Why: 로드할 파일명 형식을 {유저인덱스}_{역할}_Q{번호}.png 로 일치시킴
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
        /// 애니메이션 타임라인 종료 시 호출되며, 양쪽 PC의 종료를 동기화함.
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

        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == "PLAYVIDEO_COMPLETE")
            {
                _isRemoteVideoFinished = true;
                CheckSyncAndChangeScene();
            }
        }

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

        private void OnDestroy()
        {
            if (TcpManager.Instance) TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;

            ClearSprites(_myLoadedSprites);
            ClearSprites(_otherLoadedSprites);
        }

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
    }
}