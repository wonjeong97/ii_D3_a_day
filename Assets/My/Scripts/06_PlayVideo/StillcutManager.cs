using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Cysharp.Threading.Tasks;
using My.Scripts.Core;
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
            string role = (TcpManager.Instance && TcpManager.Instance.IsServer) ? "Left" : "Right";
            
            string baseFolder = FileTransferManager.Instance ? FileTransferManager.Instance.localSaveRoot : @"C:\UnitySharedPicture";
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            string userIdx = SessionManager.Instance ? SessionManager.Instance.CurrentUserIdx.ToString() : "0";
            
            // 날짜 폴더 병합
            _sourceFolderPath = Path.Combine(baseFolder, dateStr, userIdx, role);
        }

        private async UniTask LoadPhotosAsync()
        {
            string role = (TcpManager.Instance && TcpManager.Instance.IsServer) ? "Left" : "Right";
            string userIdx = SessionManager.Instance ? SessionManager.Instance.CurrentUserIdx.ToString() : "0";

            for (int i = 1; i <= totalFrames; i++)
            {
                // Why: 저장된 파일명 규칙과 동일하게 로드 시에도 유저 인덱스를 앞에 붙임
                string fileName = $"{userIdx}_{role}_Q{i}.png"; 
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
        /// 외부 FFMPEG 프로세스를 사용해 스틸컷 이미지 병합 영상 생성.
        /// 75% 해상도로 축소하여 인코딩 속도 최적화.
        /// </summary>
        public static void GenerateVideoInBackground()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            bool isServer = false;
            if (TcpManager.Instance) isServer = TcpManager.Instance.IsServer;
            
            string myRole = isServer ? "Left" : "Right";
            string otherRole = isServer ? "Right" : "Left";

            string baseFolder = @"C:\UnitySharedPicture";
            if (FileTransferManager.Instance) baseFolder = FileTransferManager.Instance.localSaveRoot;
            
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            
            // Why: API 업로드에 필요한 인덱스와 UID 정보를 세션에서 추출하여 인코딩 완료 콜백으로 전달할 준비를 함.
            int userIdx = 0;
            string uid = string.Empty;
            
            if (SessionManager.Instance) 
            {
                userIdx = SessionManager.Instance.CurrentUserIdx;
                uid = isServer ? SessionManager.Instance.PlayerAUid : SessionManager.Instance.PlayerBUid;
            }
            
            string userFolder = Path.Combine(baseFolder, dateStr, userIdx.ToString());
            if (!Directory.Exists(userFolder)) Directory.CreateDirectory(userFolder);
            
            string myInputPath = Path.Combine(userFolder, myRole, $"{userIdx}_{myRole}_Q%d.png");
            string otherInputPath = Path.Combine(userFolder, otherRole, $"{userIdx}_{otherRole}_Q%d.png");
            string outputVideoPath = Path.Combine(userFolder, $"{userIdx}_{myRole}_D3.mp4");
    
            string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
            string countdownPath = Path.Combine(Application.streamingAssetsPath, "countdown.mp4"); 

            if (!File.Exists(ffmpegPath))
            {
                UnityEngine.Debug.LogError($"[StillcutManager] ffmpeg.exe 경로가 존재하지 않습니다: {ffmpegPath}");
                return; 
            }

            string args;

            if (File.Exists(countdownPath))
            {
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
                args = $"-y " +
                       $"-framerate 1 -i \"{myInputPath}\" " +
                       $"-framerate 1 -i \"{otherInputPath}\" " +
                       $"-filter_complex \"[0:v]scale=720:810[l];[1:v]scale=720:810,hflip[r];[l][r]hstack=inputs=2,format=yuv420p,fade=t=in:st=0:d=0.5[v]\" " +
                       $"-map \"[v]\" " +
                       $"-c:v libx264 \"{outputVideoPath}\"";
            }

            // 인코딩 스레드에 API 통신용 파라미터(인덱스, UID)를 함께 넘겨줍니다.
            RunFFmpegAsync(ffmpegPath, args, outputVideoPath, userIdx, uid).Forget();
#else
            UnityEngine.Debug.LogWarning("[StillcutManager] Windows 환경 전용 함수입니다.");
#endif
        }

       /// <summary>
        /// 비동기 스레드 풀에서 인코딩 프로세스 실행 후 완료 시 API 업로드를 호출함.
        /// </summary>
        private static async UniTaskVoid RunFFmpegAsync(string ffmpegPath, string args, string outputPath, int userIdx, string uid)
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
                        UnityEngine.Debug.Log($"[StillcutManager] 비디오 인코딩 완료: {outputPath}");
                        
                        // --- 영상 업로드 로직 ---
                        byte[] videoBytes = await File.ReadAllBytesAsync(outputPath);
                        
                        if (APIManager.Instance)
                        {
                            APIManager.Instance.UploadVideoAsync(videoBytes, userIdx, uid, "d3").Forget();
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning("[StillcutManager] APIManager 인스턴스를 찾을 수 없어 영상을 업로드하지 못했습니다.");
                        }
                        // ----------------------------
                    }
                    else
                    {
                        string stderr = process.StandardError != null ? process.StandardError.ReadToEnd() : "N/A";
                        UnityEngine.Debug.LogError($"[StillcutManager] 인코딩 실패 (ExitCode: {process.ExitCode}): {stderr}");
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[StillcutManager] 프로세스 실행 에러: {e.Message}");
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