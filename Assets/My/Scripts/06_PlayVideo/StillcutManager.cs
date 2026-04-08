using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Network;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Utils;

namespace My.Scripts._06_PlayVideo
{
    /// <summary>
    /// 촬영된 스틸컷 이미지를 로드하여 화면에 시뮬레이션하고 백그라운드에서 병합 영상을 생성하는 매니저.
    /// 플레이어에게 시각적 피드백을 제공하는 동시에 서버 전송용 결과물을 백그라운드에서 준비함.
    /// </summary>
    public class StillcutManager : MonoBehaviour
    {
        [Header("Simulation UI")]
        [SerializeField] private RawImage displayUI;
        [SerializeField] private CanvasGroup fadeGroup;

        [Header("Encoding Settings")]
        [SerializeField] private int totalFrames = 15;
        [SerializeField] private float frameDuration = 1.0f;

        private readonly List<Texture2D> _loadedTextures = new List<Texture2D>();
        private string _sourceFolderPath;

        /// <summary>
        /// 씬 진입 시 비동기 초기화 로직을 트리거함.
        /// </summary>
        private void Start()
        {
            SetupPaths();
            InitializeAsync().Forget();
        }

        /// <summary>
        /// 이미지 로드 및 시뮬레이션을 비동기로 준비함.
        /// async void 사용으로 인한 앱 크래시를 방지하고 모든 예외를 안전하게 포착하기 위함.
        /// </summary>
        private async UniTaskVoid InitializeAsync()
        {
            try
            {
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
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[StillcutManager] 초기화 중 예외 발생: {e.Message}");
                MoveToEnding();
            }
        }

        /// <summary>
        /// 로컬 저장 경로, 현재 날짜, 유저 인덱스, 네트워크 역할을 조합하여 대상 폴더 경로를 생성함.
        /// 양쪽 PC가 각자의 역할에 맞는 사진 데이터를 정확히 바라보게 함.
        /// </summary>
        private void SetupPaths()
        {
            string role = (TcpManager.Instance && TcpManager.Instance.IsServer) ? "Left" : "Right";
            
            string baseFolder = FileTransferManager.Instance ? FileTransferManager.Instance.localSaveRoot : @"C:\UnitySharedPicture";
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            string userIdx = SessionManager.Instance ? SessionManager.Instance.CurrentUserIdx.ToString() : "0";
            
            _sourceFolderPath = Path.Combine(baseFolder, dateStr, userIdx, role);
        }

        /// <summary>
        /// 생성된 경로에서 사진 파일들을 읽어와 텍스처로 변환함.
        /// 저장된 파일명 규칙을 기반으로 순차적으로 접근하여 메모리에 적재함.
        /// </summary>
        private async UniTask LoadPhotosAsync()
        {
            string role = (TcpManager.Instance && TcpManager.Instance.IsServer) ? "Left" : "Right";
            string userIdx = SessionManager.Instance ? SessionManager.Instance.CurrentUserIdx.ToString() : "0";

            for (int i = 1; i <= totalFrames; i++)
            {
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

        /// <summary>
        /// 로드된 텍스처를 지정된 시간 간격으로 교체하며 슬라이드쇼를 재생함.
        /// 영상 인코딩이 진행되는 동안 유저에게 보여줄 임시 시각 연출을 수행함.
        /// </summary>
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

        /// <summary>
        /// 시뮬레이션 종료 또는 오류 발생 시 엔딩 씬으로 흐름을 넘김.
        /// </summary>
        private void MoveToEnding()
        {
            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.Ending, true);
            }
        }

        /// <summary>
        /// 외부 FFmpeg 프로세스를 백그라운드에서 실행하여 양쪽 기기의 사진을 하나로 병합한 영상을 생성함.
        /// API 업로드에 필요한 인덱스와 UID 정보를 세션에서 추출하여 콜백으로 전달할 준비를 함께 수행함.
        /// 입력 예시: C:\UnitySharedPicture\2026-03-31\123\Left\123_Left_Q%d.png
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
    
            // FFmpeg가 누락된 시퀀스 번호에서 멈추는 것을 방지하기 위해 빈자리를 복사본으로 채움
            EnsureSequentialImages(Path.Combine(userFolder, myRole), $"{userIdx}_{myRole}", 15);
            EnsureSequentialImages(Path.Combine(userFolder, otherRole), $"{userIdx}_{otherRole}", 15);

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

            RunFFmpegAsync(ffmpegPath, args, outputVideoPath, userIdx, uid).Forget();
#else
            UnityEngine.Debug.LogWarning("[StillcutManager] Windows 환경 전용 함수입니다.");
#endif
        }

        /// <summary>
        /// FFmpeg의 %d 시퀀스 읽기가 중간에 파일 누락으로 인해 중단되는 것을 방지함.
        /// 빈자리가 있다면 사용 가능한 이전(또는 다음) 사진을 복사하여 15장의 구성을 강제함.
        /// </summary>
        private static void EnsureSequentialImages(string folderPath, string prefix, int totalFrames)
        {
            if (!Directory.Exists(folderPath)) return;
            
            string fallbackPath = null;
            for (int i = 1; i <= totalFrames; i++)
            {
                string path = Path.Combine(folderPath, $"{prefix}_Q{i}.png");
                if (File.Exists(path))
                {
                    fallbackPath = path;
                    break;
                }
            }

            // 쓸 수 있는 이미지가 아예 없다면 무시 (FFmpeg가 실패하겠지만 진행에는 문제 없음)
            if (fallbackPath == null) return;

            string lastValid = fallbackPath;
            for (int i = 1; i <= totalFrames; i++)
            {
                string path = Path.Combine(folderPath, $"{prefix}_Q{i}.png");
                if (File.Exists(path))
                {
                    lastValid = path;
                }
                else
                {
                    try
                    {
                        File.Copy(lastValid, path);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning($"[StillcutManager] 임시 이미지 복사 실패: {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 메인 스레드 병목을 막기 위해 비동기 스레드 풀에서 인코딩 대기 작업을 수행함.
        /// 인코딩이 정상 종료되면 생성된 결과물 경로를 전달하여 서버 업로드 API를 호출함.
        /// </summary>
        /// <param name="ffmpegPath">FFmpeg 실행 파일의 전체 시스템 경로.</param>
        /// <param name="args">FFmpeg 실행에 사용될 커맨드라인 포맷 인자열.</param>
        /// <param name="outputPath">인코딩 결과물이 최종 저장될 로컬 파일 경로.</param>
        /// <param name="userIdx">API 전송 시 식별자로 사용될 현재 유저의 고유 인덱스 번호.</param>
        /// <param name="uid">API 전송 시 식별자로 사용될 현재 유저의 고유 아이디.</param>
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
                    Task<string> readErrorTask = process.StandardError.ReadToEndAsync();

                    await UniTask.RunOnThreadPool(() => process.WaitForExit());
                    
                    string stderr = await readErrorTask;

                    if (process.ExitCode == 0 && File.Exists(outputPath))
                    {
                        UnityEngine.Debug.Log($"[StillcutManager] 비디오 인코딩 완료: {outputPath}");
                        
                        if (APIManager.Instance)
                        {
                            string module = SessionManager.Instance ? SessionManager.Instance.CurrentModuleCode : "D3";
                            APIManager.Instance.UploadVideoAsync(outputPath, userIdx, uid, module).Forget();
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning("[StillcutManager] APIManager 인스턴스를 찾을 수 없어 영상을 업로드하지 못했습니다.");
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.LogError($"[StillcutManager] 인코딩 실패 (ExitCode: {process.ExitCode}): {stderr}");
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[StillcutManager] 프로세스 실행 에러: {e.Message}");
            }
        }

        /// <summary>
        /// 동적 생성된 텍스처 리소스를 파괴함.
        /// 씬 종료 시 대용량 이미지로 인한 비디오 메모리 누수를 방지하기 위함.
        /// </summary>
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