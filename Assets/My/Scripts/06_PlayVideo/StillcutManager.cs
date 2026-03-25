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
            
            await LoadPhotosAsync();

            if (_loadedTextures.Count > 0)
            {
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
            
            string baseFolder = FileTransferManager.Instance ? FileTransferManager.Instance.localSaveRoot : @"C:\UnitySharedPicture";
            string userIdx = SessionManager.Instance ? SessionManager.Instance.CurrentUserId.ToString() : "0";
            
            _sourceFolderPath = Path.Combine(baseFolder, userIdx, role);
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
        /// 백그라운드에서 두 플레이어의 스틸컷 이미지를 하나의 영상으로 병합 및 인코딩함.
        /// 전체 해상도를 75%(1440x810)로 스케일링하여 인코딩 속도를 최적화합니다.
        /// </summary>
        public static void GenerateVideoInBackground()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            bool isServer = TcpManager.Instance && TcpManager.Instance.IsServer;
            string myRole = isServer ? "Server" : "Client";
            string otherRole = isServer ? "Client" : "Server";

            string baseFolder = FileTransferManager.Instance ? FileTransferManager.Instance.localSaveRoot : @"C:\UnitySharedPicture";
            string userIdx = SessionManager.Instance ? SessionManager.Instance.CurrentUserId.ToString() : "0";
            string userFolder = Path.Combine(baseFolder, userIdx);

            if (!Directory.Exists(userFolder)) Directory.CreateDirectory(userFolder);

            string myInputPath = Path.Combine(userFolder, myRole, $"0_{myRole}_Q%d.png");
            string otherInputPath = Path.Combine(userFolder, otherRole, $"0_{otherRole}_Q%d.png");
            string outputVideoPath = Path.Combine(userFolder, $"Result_{myRole}_Combined.mp4");
    
            string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
            string countdownPath = Path.Combine(Application.streamingAssetsPath, "countdown.mp4"); 

            if (!File.Exists(ffmpegPath))
            {
                UnityEngine.Debug.LogError($"[StillcutManager] ffmpeg.exe 경로 누락: {ffmpegPath}");
                return;
            }

            string args;

            if (File.Exists(countdownPath))
            {
                UnityEngine.Debug.Log("[StillcutManager] 카운트다운 영상을 포함하여 75% 해상도로 인코딩을 시작합니다.");
                
                // Why: 1920x1080 -> 1440x810 (75%)
                // 왼쪽/오른쪽 이미지 개별 크기는 960x1080 -> 720x810 으로 수정
                args = $"-y " +
                       $"-i \"{countdownPath}\" " +
                       $"-framerate 1 -i \"{myInputPath}\" " +
                       $"-framerate 1 -i \"{otherInputPath}\" " +
                       $"-filter_complex \"[1:v]scale=720:810[l];[2:v]scale=720:810,hflip[r];[l][r]hstack=inputs=2,fps=30,format=yuv420p,fade=t=in:st=0:d=0.5[v_main];[0:v]scale=1440:810,fps=30,format=yuv420p[v_intro];[v_intro][v_main]concat=n=2:v=1:a=0[v_out]\" " +
                       $"-map \"[v_out]\" " +
                       $"-c:v libx264 \"{outputVideoPath}\"";
            }
            else
            {
                UnityEngine.Debug.LogWarning("[StillcutManager] countdown.mp4 파일이 없습니다. 카운트다운 없이 75% 해상도로 영상을 생성합니다.");
                
                args = $"-y " +
                       $"-framerate 1 -i \"{myInputPath}\" " +
                       $"-framerate 1 -i \"{otherInputPath}\" " +
                       $"-filter_complex \"[0:v]scale=720:810[l];[1:v]scale=720:810,hflip[r];[l][r]hstack=inputs=2,format=yuv420p,fade=t=in:st=0:d=0.5[v]\" " +
                       $"-map \"[v]\" " +
                       $"-c:v libx264 \"{outputVideoPath}\"";
            }

            RunFFmpegAsync(ffmpegPath, args, outputVideoPath).Forget();
#else
            UnityEngine.Debug.LogWarning("[StillcutManager] Windows 환경 전용 함수임.");
#endif
        }

        private static async UniTaskVoid RunFFmpegAsync(string ffmpegPath, string args, string outputPath)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true 
                };

                Process process = Process.Start(psi);
                if (process != null)
                {
                    await UniTask.RunOnThreadPool(() => process.WaitForExit());
                    
                    if (process.ExitCode == 0 && File.Exists(outputPath))
                    {
                        UnityEngine.Debug.Log($"[StillcutManager] 백그라운드 영상 생성 완료: {outputPath}");
                    }
                    else
                    {
                        string stderr = process.StandardError?.ReadToEnd() ?? "N/A";
                        UnityEngine.Debug.LogError($"[StillcutManager] 인코딩 프로세스 실패 (ExitCode: {process.ExitCode}). 에러: {stderr}");
                    }
                }
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