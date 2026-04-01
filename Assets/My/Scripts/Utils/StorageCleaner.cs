using System;
using System.Globalization;
using System.IO;
using Cysharp.Threading.Tasks;
using My.Scripts.Global;
using My.Scripts.Network;
using UnityEngine;
using Wonjeong.Utils;

namespace My.Scripts.Utils
{
    /// <summary>
    /// 앱 실행 시 백그라운드에서 오래된 데이터(사진, 영상)를 자동 삭제하여 디스크 용량을 관리하는 유틸리티.
    /// 장기 운용 시 저장 공간 부족으로 인한 크래시 및 성능 저하를 방지하기 위함.
    /// </summary>
    public static class StorageCleaner
    {
        private const int MaxKeepDays = 3; 

        /// <summary> 
        /// 첫 씬 로드 직후 자동으로 실행됨.
        /// 메인 스레드 프리징을 방지하기 위해 스레드 풀에서 백그라운드 삭제 작업을 수행하기 위함.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RunCleanupOnStartup()
        {
            try
            {
                UniTask.RunOnThreadPool(() =>
                {
                    Debug.Log($"[StorageCleaner] 백그라운드 자동 정리 시작 (보관 기준: {MaxKeepDays}일)");

                    string rootPath = @"C:\UnitySharedPicture";
                    
                    // 네트워크 설정 파일에 정의된 저장 경로를 최우선으로 가져와 하드코딩을 방지함.
                    TcpSetting loadedSetting = JsonLoader.Load<TcpSetting>(GameConstants.Path.TcpSetting);
                    if (loadedSetting != null && !string.IsNullOrWhiteSpace(loadedSetting.localSaveRoot))
                    {
                        rootPath = loadedSetting.localSaveRoot;
                    }

                    DateTime thresholdDate = DateTime.Now.AddDays(-MaxKeepDays);

                    CleanOldFolders(rootPath, thresholdDate);

                }).Forget();
            }
            catch (Exception e)
            {
                Debug.LogError($"[StorageCleaner] 정리 작업 중 예외 발생: {e.Message}");
            }
        }

        /// <summary> 
        /// 대상 폴더 내 하위 폴더들을 전수 조사하여 날짜 기준에 미달하는 데이터를 삭제함.
        /// </summary>
        /// <param name="targetPath">검사할 최상위 디렉토리 경로.</param>
        /// <param name="thresholdDate">보관 유효 기준 날짜.</param>
        private static void CleanOldFolders(string targetPath, DateTime thresholdDate)
        {
            if (!Directory.Exists(targetPath)) return;

            DirectoryInfo dirInfo = new DirectoryInfo(targetPath);
            DirectoryInfo[] subDirs = dirInfo.GetDirectories(); 

            foreach (DirectoryInfo subDir in subDirs)
            {
                // 폴더 명칭이 날짜 형식이 아닌 경우(예: 임시 폴더) 무시하여 의도치 않은 데이터 손실을 차단함.
                if (DateTime.TryParseExact(subDir.Name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime folderDate))
                {
                    if (folderDate.Date < thresholdDate.Date)
                    {
                        try
                        {
                            // 내부 파일 및 하위 디렉토리를 포함하여 강제 삭제함.
                            subDir.Delete(true); 
                            Debug.Log($"[StorageCleaner] 오래된 폴더 삭제 완료: {subDir.FullName}");
                        }
                        catch (IOException)
                        {
                            // 파일이 다른 프로세스(FFmpeg 인코딩 등)에 의해 사용 중일 때 발생하는 충돌을 안전하게 넘김.
                            Debug.LogWarning($"[StorageCleaner] 폴더가 사용 중임: {subDir.Name}");
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[StorageCleaner] 폴더 삭제 실패 ({subDir.Name}): {e.Message}");
                        }
                    }
                }
            }
        }
    }
}