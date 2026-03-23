using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Cysharp.Threading.Tasks;
using My.Scripts.Global;
using My.Scripts.Network;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Utils;

namespace My.Scripts._06_PlayVideo
{
    /// <summary>
    /// Step2에서 촬영한 스틸컷들을 활용해 시뮬레이션 재생을 담당하며,
    /// 외부(Step3)에서 백그라운드 MP4 인코딩을 지시할 수 있는 유틸리티를 제공하는 매니저.
    /// </summary>
    public class StillcutManager : MonoBehaviour
    {
        [Header("Simulation UI")]
        [SerializeField] private RawImage displayUI;
        [SerializeField] private CanvasGroup fadeGroup;

        [Header("Encoding Settings")]
        [SerializeField] private int totalFrames = 15;
        [SerializeField] private float frameDuration = 1.0f;

        private List<Texture2D> _loadedTextures = new List<Texture2D>();
        private string _sourceFolderPath;

        private async void Start()
        {
            SetupPaths();
            
            // 시뮬레이션 재생을 위해 촬영된 사진들을 메모리로 로드함.
            await LoadPhotosAsync();

            if (_loadedTextures.Count > 0)
            {
                // 화면상에서 사진들을 1초씩 보여주는 시뮬레이션 시작
                // Why: 영상 인코딩은 Step3 씬 진입 시 이미 백그라운드에서 실행되었으므로 여기서는 재생만 담당함.
                StartCoroutine(PlaySimulationRoutine());
            }
            else
            {
                UnityEngine.Debug.LogError("[StillcutManager] 로드된 사진이 없습니다. 경로를 확인하세요.");
                MoveToEnding();
            }
        }

        private void SetupPaths()
        {
            string role = (TcpManager.Instance && TcpManager.Instance.IsServer) ? "Server" : "Client";
            string dateFolder = DateTime.Now.ToString("yy-MM-dd");
            
            string rootPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            _sourceFolderPath = Path.Combine(rootPath, dateFolder, role);
        }

        private async UniTask LoadPhotosAsync()
        {
            string role = (TcpManager.Instance && TcpManager.Instance.IsServer) ? "Server" : "Client";

            for (int i = 1; i <= totalFrames; i++)
            {
                string fileName = $"0_{role}_Q{i}.png"; 
                string fullPath = Path.Combine(_sourceFolderPath, fileName);

                if (File.Exists(fullPath))
                {
                    byte[] fileData = await File.ReadAllBytesAsync(fullPath);
                    Texture2D tex = new Texture2D(2, 2);
                    if (tex.LoadImage(fileData))
                    {
                        _loadedTextures.Add(tex);
                    }
                }
            }
        }

        private IEnumerator PlaySimulationRoutine()
        {
            if (fadeGroup) fadeGroup.alpha = 1f;

            foreach (Texture2D frame in _loadedTextures)
            {
                if (displayUI) displayUI.texture = frame;
                yield return CoroutineData.GetWaitForSeconds(frameDuration);
            }

            MoveToEnding();
        }

        private void MoveToEnding()
        {
            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.Ending, true);
            }
        }

        /// <summary>
        /// Step3 진입 시 호출되어 백그라운드에서 영상을 미리 생성하는 정적 함수.
        /// Why: 인스턴스화되지 않은 상태에서도 호출 가능하도록 static으로 선언함.
        /// </summary>
        public static void GenerateVideoInBackground()
        {
            string role = (TcpManager.Instance && TcpManager.Instance.IsServer) ? "Server" : "Client";
            string dateFolder = DateTime.Now.ToString("yy-MM-dd");
            
            string rootPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string sourceFolderPath = Path.Combine(rootPath, dateFolder, role);
            string outputVideoPath = Path.Combine(sourceFolderPath, $"Result_{role}.mp4");
            
            string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");

            string args = $"-y -framerate 1 -i \"{sourceFolderPath}\\0_{role}_Q%d.png\" -c:v libx264 -pix_fmt yuv420p \"{outputVideoPath}\"";

            RunFFmpegAsync(ffmpegPath, args, outputVideoPath).Forget();
        }

        /// <summary>
        /// FFmpeg 프로세스를 실행하고 스레드 풀에서 대기하는 비동기 함수.
        /// </summary>
        private static async UniTaskVoid RunFFmpegAsync(string ffmpegPath, string args, string outputPath)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                Process p = Process.Start(psi);
                
                await UniTask.RunOnThreadPool(() => p.WaitForExit());

                UnityEngine.Debug.Log($"[StillcutManager] 백그라운드 로컬 영상 생성 완료: {outputPath}");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[StillcutManager] 영상 인코딩 중 오류 발생: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            foreach (Texture2D tex in _loadedTextures)
            {
                if (tex) Destroy(tex);
            }
            _loadedTextures.Clear();
        }
    }
}