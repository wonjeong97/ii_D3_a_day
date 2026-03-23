using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using My.Scripts.Network;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks; 

namespace My.Scripts._06_PlayVideo
{
    /// <summary>
    /// 좌우로 분리된 메인 뷰와 스틸컷 배열을 최적화된 그룹 방식으로 슬라이드 재생하는 매니저.
    /// Why: 오른쪽 영역에 배치된 모든 이미지를 일괄 반전시키고, 메인 뷰(인덱스 0 시작)와 모자이크(그룹화 갱신)의 렌더링을 제어함.
    /// </summary>
    public class PlayVideoManager : MonoBehaviour
    {
        [Header("Main View Components")]
        [SerializeField] private Image leftMainImage;
        [SerializeField] private Image rightMainImage;

        [Header("Overlay UI")]
        [SerializeField] private CanvasGroup middleLineCg;

        [Header("Animation Settings")]
        [SerializeField] private float waitDuration = 3.0f;
        [SerializeField] private float fadeDuration = 1.0f;

        [Header("Mosaic Images (Still Cuts)")]
        [SerializeField] private Image[] leftTargetImages;
        [SerializeField] private Image[] rightTargetImages;
        [SerializeField] private float appearanceInterval = 0.05f;
        [SerializeField] private float photoChangeInterval = 0.5f;
        [SerializeField] private int totalPhotos = 15;

        private List<Sprite> _loadedSprites = new List<Sprite>();

        /// <summary>
        /// 씬 진입 시 초기화를 수행하고 비동기 로드 및 슬라이드 시퀀스를 시작함.
        /// </summary>
        private void Start()
        {
            SetupImagesUI();
            SetupMiddleLineUI();
            InitializeAndPlayAsync().Forget();
        }

        /// <summary>
        /// 오른쪽 메인 이미지 및 모자이크 배열의 좌우 반전을 설정함.
        /// Why: 오른쪽 화면용 이미지들은 X축 스케일을 -1로 설정하여 렌더링 시점에 뒤집혀 보이도록 함.
        /// </summary>
        private void SetupImagesUI()
        {
            if (rightMainImage)
            {
                rightMainImage.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            }

            if (rightTargetImages != null)
            {
                for (int i = 0; i < rightTargetImages.Length; i++)
                {
                    Image img = rightTargetImages[i];
                    if (img) img.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                }
            }
        }

        /// <summary>
        /// 동기화가 필요한 로드 작업을 먼저 수행한 뒤 메인 이미지와 모자이크 연출을 병렬로 시작함.
        /// </summary>
        private async UniTaskVoid InitializeAndPlayAsync()
        {
            await LoadPhotosAsSpritesAsync();
            
            CancellationToken token = this.GetCancellationTokenOnDestroy();

            UpdateMainImagesAsync(token).Forget(); 
            PlayMosaicSequence(token); 
            FadeOutMiddleLineSequenceAsync().Forget();
        }

        /// <summary>
        /// 로컬에 저장된 사진을 읽어 메모리에 캐싱함.
        /// </summary>
        private async UniTask LoadPhotosAsSpritesAsync()
        {
            string role = (TcpManager.Instance && TcpManager.Instance.IsServer) ? "Server" : "Client";
            string dateFolder = DateTime.Now.ToString("yy-MM-dd");
            string rootPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string sourceFolderPath = Path.Combine(rootPath, dateFolder, role);

            for (int i = 1; i <= totalPhotos; i++)
            {
                string fileName = $"0_{role}_Q{i}.png"; 
                string fullPath = Path.Combine(sourceFolderPath, fileName);

                if (File.Exists(fullPath))
                {
                    byte[] fileData = await File.ReadAllBytesAsync(fullPath);
                    Texture2D tex = new Texture2D(2, 2);
                    if (tex.LoadImage(fileData))
                    {
                        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                        _loadedSprites.Add(sprite);
                    }
                }
                else
                {
                    Debug.LogWarning($"[PlayVideoManager] 파일을 찾을 수 없음: {fullPath}");
                }
            }
        }

        /// <summary>
        /// 0번째 인덱스부터 시작하여 좌, 우 메인 이미지의 Sprite를 동일하게 주기적으로 갱신함.
        /// Why: 동일한 인덱스를 사용하여 양쪽 메인 뷰가 완벽하게 동기화된 상태로 사진을 순환하도록 함.
        /// </summary>
        private async UniTaskVoid UpdateMainImagesAsync(CancellationToken token)
        {
            if ((!leftMainImage && !rightMainImage) || _loadedSprites.Count == 0) return;

            int currentIndex = 0;

            while (!token.IsCancellationRequested)
            {
                Sprite currentSprite = _loadedSprites[currentIndex];

                if (leftMainImage) leftMainImage.sprite = currentSprite;
                if (rightMainImage) rightMainImage.sprite = currentSprite;
                
                currentIndex = (currentIndex + 1) % _loadedSprites.Count;

                bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(photoChangeInterval), cancellationToken: token).SuppressCancellationThrow();
                if (isCanceled) break;
            }
        }

        /// <summary>
        /// 좌우 스틸컷 이미지를 3그룹으로 묶어 단일 루프에서 일괄 갱신할 수 있도록 준비함.
        /// </summary>
        private void PlayMosaicSequence(CancellationToken token)
        {
            if (_loadedSprites.Count == 0) return;

            List<Image>[] imageGroups = new List<Image>[3];
            for (int i = 0; i < 3; i++)
            {
                imageGroups[i] = new List<Image>();
            }

            int globalIndex = 0;

            if (leftTargetImages != null)
            {
                for (int i = 0; i < leftTargetImages.Length; i++)
                {
                    Image img = leftTargetImages[i];
                    if (!img) continue;

                    img.color = new Color(img.color.r, img.color.g, img.color.b, 0f);
                    
                    // 예시 입력값: globalIndex=4 -> 4 % 3 = 그룹 1에 할당
                    imageGroups[globalIndex % 3].Add(img);
                    globalIndex++;
                }
            }

            if (rightTargetImages != null)
            {
                for (int i = 0; i < rightTargetImages.Length; i++)
                {
                    Image img = rightTargetImages[i];
                    if (!img) continue;

                    img.color = new Color(img.color.r, img.color.g, img.color.b, 0f);
                    imageGroups[globalIndex % 3].Add(img);
                    globalIndex++;
                }
            }

            TurnOnImagesSequentiallyAsync(token).Forget();
            UpdateMosaicSpritesAsync(imageGroups, token).Forget();
        }

        /// <summary>
        /// 좌, 우 배열의 모든 이미지를 하나로 합쳐 지정된 간격으로 순차적으로 화면에 표시함.
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
        /// 시작 인덱스가 2, 7, 12로 고정된 3개의 그룹을 주기적으로 갱신함.
        /// </summary>
        private async UniTaskVoid UpdateMosaicSpritesAsync(List<Image>[] imageGroups, CancellationToken token)
        {
            int spriteCount = _loadedSprites.Count;
            
            int[] currentIndices = new int[3];
            currentIndices[0] = 2 % spriteCount;
            currentIndices[1] = 7 % spriteCount;
            currentIndices[2] = 12 % spriteCount;

            while (!token.IsCancellationRequested)
            {
                for (int g = 0; g < 3; g++)
                {
                    Sprite targetSprite = _loadedSprites[currentIndices[g]];
                    List<Image> group = imageGroups[g];

                    for (int i = 0; i < group.Count; i++)
                    {
                        Image img = group[i];
                        if (img && img.color.a > 0f)
                        {
                            img.sprite = targetSprite;
                        }
                    }

                    currentIndices[g] = (currentIndices[g] + 1) % spriteCount;
                }

                // # TODO: 만약 이미지 개수가 더 많아져 프레임 드랍이 발생하면, Graphic.CrossFadeAlpha 최적화 혹은 배칭 구조 검토
                bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(photoChangeInterval), cancellationToken: token).SuppressCancellationThrow();
                if (isCanceled) break;
            }
        }

        /// <summary>
        /// 중간 라인 UI의 초기 상태를 설정함.
        /// </summary>
        private void SetupMiddleLineUI()
        {
            if (!middleLineCg) return;

            middleLineCg.alpha = 1f;
            middleLineCg.gameObject.SetActive(true);
        }

        /// <summary>
        /// 설정된 대기시간이 지나면 중간 라인 캔버스 그룹을 투명하게 만듦.
        /// </summary>
        private async UniTaskVoid FadeOutMiddleLineSequenceAsync()
        {
            if (!middleLineCg) return;

            await UniTask.Delay(TimeSpan.FromSeconds(waitDuration), delayTiming: PlayerLoopTiming.Update);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;

                middleLineCg.alpha = Mathf.Lerp(1f, 0f, t);
                await UniTask.Yield(PlayerLoopTiming.Update); 
            }

            middleLineCg.alpha = 0f;
            middleLineCg.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (Sprite sprite in _loadedSprites)
            {
                if (sprite && sprite.texture)
                {
                    Destroy(sprite.texture);
                    Destroy(sprite);
                }
            }
            _loadedSprites.Clear();
        }
    }
}