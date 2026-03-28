using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks; 
using My.Scripts.Core.Data;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json; 
using My.Scripts.Global;
using Wonjeong.Utils;

namespace My.Scripts.Core
{
    public enum ColorData { NotSet = -1, Cyan = 0, Pink = 1, Orange = 2, Green = 3, Red = 4, Yellow = 5 }
    
    public struct UserData
    {
        public string CARTRIDGE;
        public int IDX_USER; 
        public string BLOCK_CODE;
        public string UID_LEFT;
        public string UID_RIGHT;
        public string LANG;
        public int RELATION;
        
        public ColorData COLOR_LEFT; 
        public ColorData COLOR_RIGHT;

        public string RESERVATION_FIRST_NAME_LEFT;
        public string RESERVATION_LAST_NAME_LEFT;
        public string RESERVATION_FIRST_NAME_RIGHT;
        public string RESERVATION_LAST_NAME_RIGHT;
        
        public int PIECE_A1; public int PIECE_A2; public int PIECE_A3;
        public int PIECE_B1; public int PIECE_B2; public int PIECE_B3;
        public int PIECE_C1; public int PIECE_C2; public int PIECE_C3;
        public int PIECE_D1; public int PIECE_D2; public int PIECE_D3;
    }

    public class ApiTableResponse
    {
        public List<string> COLUMNS { get; set; }
        public List<List<object>> DATA { get; set; } 
    }

    public class APIManager : MonoBehaviour
    {
        private string userUid;

        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries = 10;
        [SerializeField] private float retryDelay = 1.0f;

        public void FetchData(string uid) 
        { 
            FetchDataAsync(uid, CancellationToken.None).Forget(); 
        }
        
       [ContextMenu("Fetch API Data")]
        public async UniTask<bool> FetchDataAsync(string uid, CancellationToken cancellationToken = default)
        {
#if UNITY_EDITOR
            if (SessionManager.Instance)
            {
                if (!SessionManager.Instance.useEditorTestData)
                {
                    SessionManager.Instance.CurrentUserIdx = -1;
                    SessionManager.Instance.PlayerAUid = "TEST_A";
                    SessionManager.Instance.PlayerBUid = "TEST_B";
                    SessionManager.Instance.PlayerAFirstName = "에디터";
                    SessionManager.Instance.PlayerBFirstName = "테스터";
                    SessionManager.Instance.Cartridge = "A";
                    SessionManager.Instance.CurrentUserType = UserType.A1;
                }
                SessionManager.Instance.IsOtherCartridgeContentsCleared = true; 
            }
            Debug.Log("[APIManager] 에디터 모드: 유저 데이터 통신을 생략하고 가상 세션을 생성/유지했습니다.");
            return true;
#endif

            userUid = uid;
            ApiSettings config = GameManager.Instance ? GameManager.Instance.ApiConfig : null;

            if (config == null)
            {
                config = JsonLoader.Load<ApiSettings>(GameConstants.Path.ApiSetting);
                if (GameManager.Instance && config != null) 
                {
                    GameManager.Instance.ApiConfig = config;
                }
            }

            if (config == null)
            {
                Debug.LogError("[APIManager] API 설정을 찾을 수 없습니다.");
                return false;
            }

            string requestUrl = $"{config.GetUserUrl}?uid={userUid}";
            
            // Why: 서버 부하 또는 네트워크 지연으로 인한 1회성 통신 실패를 방지함
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
                {
                    webRequest.timeout = 10; 
                    
                    try
                    {
                        await webRequest.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

                        if (webRequest.result == UnityWebRequest.Result.Success)
                        {
                            return await ParseAndProcessDataAsync(webRequest.downloadHandler.text);
                        }

                        if (attempt < maxRetries - 1)
                        {
                            Debug.LogWarning($"[APIManager] 유저 데이터 조회 실패 ({attempt + 1}/{maxRetries}): {webRequest.error}. {retryDelay}초 후 재시도");
                            await UniTask.Delay(TimeSpan.FromSeconds(retryDelay), cancellationToken: cancellationToken);
                        }
                        else
                        {
                            Debug.LogError($"[APIManager] 유저 데이터 조회 최종 실패: {webRequest.error}");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.LogWarning("[APIManager] FetchDataAsync 작업이 취소되었습니다.");
                        throw;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[APIManager] FetchDataAsync 통신 예외 발생 ({attempt + 1}/{maxRetries}): {e.Message}");
                        if (attempt < maxRetries - 1) 
                        {
                            await UniTask.Delay(TimeSpan.FromSeconds(retryDelay), cancellationToken: cancellationToken);
                        }
                    }
                }
            }
            
            return false;
        }

        public async UniTask<bool> ParseAndProcessDataAsync(string jsonString)
        {
            try
            {
                ApiTableResponse response = await UniTask.RunOnThreadPool(() => JsonConvert.DeserializeObject<ApiTableResponse>(jsonString));

                if (response != null && response.DATA != null && response.DATA.Count > 0)
                {
                    List<object> firstRow = response.DATA[0];

                    Dictionary<string, int> colMap = new Dictionary<string, int>();
                    for (int i = 0; i < response.COLUMNS.Count; i++)
                    {
                        colMap[response.COLUMNS[i]] = i;
                    }

                    UserData userData = new UserData();
                    userData.IDX_USER = ParseIntSafe(colMap, firstRow, "IDX_USER");
                    userData.CARTRIDGE = ParseStringSafe(colMap, firstRow, "CARTRIDGE"); 
                    userData.UID_LEFT = ParseStringSafe(colMap, firstRow, "UID_LEFT");
                    userData.UID_RIGHT = ParseStringSafe(colMap, firstRow, "UID_RIGHT");
                    userData.LANG = ParseStringSafe(colMap, firstRow, "LANG");
                    userData.RELATION = ParseIntSafe(colMap, firstRow, "RELATION");
                    userData.RESERVATION_FIRST_NAME_LEFT = ParseStringSafe(colMap, firstRow, "RESERVATION_FIRST_NAME_LEFT");
                    userData.RESERVATION_FIRST_NAME_RIGHT = ParseStringSafe(colMap, firstRow, "RESERVATION_FIRST_NAME_RIGHT");
                    userData.COLOR_LEFT = ParseColorSafe(colMap, firstRow, "COLOR_LEFT");
                    userData.COLOR_RIGHT = ParseColorSafe(colMap, firstRow, "COLOR_RIGHT");
                    userData.BLOCK_CODE = ParseStringSafe(colMap, firstRow, "BLOCK_CODE");

                    if (SessionManager.Instance)
                    {   
                        SessionManager.Instance.CurrentUserIdx = userData.IDX_USER;
                        SessionManager.Instance.BlockCode = userData.BLOCK_CODE;
                        SessionManager.Instance.Cartridge = userData.CARTRIDGE; 
                        SessionManager.Instance.PlayerAUid = userData.UID_LEFT;
                        SessionManager.Instance.PlayerBUid = userData.UID_RIGHT;

                        if (!string.IsNullOrWhiteSpace(userData.LANG)) 
                        {
                            SessionManager.Instance.CurrentLanguage = userData.LANG.Trim();
                        }

                        if (!string.IsNullOrEmpty(userData.RESERVATION_FIRST_NAME_LEFT))
                        {
                            SessionManager.Instance.PlayerAFirstName = userData.RESERVATION_FIRST_NAME_LEFT;
                        }
                        
                        if (!string.IsNullOrEmpty(userData.RESERVATION_FIRST_NAME_RIGHT))
                        {
                            SessionManager.Instance.PlayerBFirstName = userData.RESERVATION_FIRST_NAME_RIGHT;
                        }
                        
                        SessionManager.Instance.PlayerAColor = userData.COLOR_LEFT;
                        SessionManager.Instance.PlayerBColor = userData.COLOR_RIGHT;

                        // Why: 카트리지 누락이나 유효하지 않은 관계 값(1~6 외)이 들어올 경우를 대비한 안전 장치
                        string cartridgeStr = string.IsNullOrWhiteSpace(userData.CARTRIDGE) ? "A" : userData.CARTRIDGE.Trim().ToUpper();
                        int relationNum = userData.RELATION;
                        if (relationNum < 1 || relationNum > 6) relationNum = 1; 

                        string combinedTypeStr = $"{cartridgeStr}{relationNum}"; 
                        
                        if (Enum.TryParse(combinedTypeStr, out UserType parsedType))
                        {
                            SessionManager.Instance.CurrentUserType = parsedType;
                        }
                        else
                        {
                            Debug.LogWarning($"[APIManager] 알 수 없는 타입 조합({combinedTypeStr})입니다. 기본값(A1)으로 설정합니다.");
                            SessionManager.Instance.CurrentUserType = UserType.A1;
                        }

                        userData.PIECE_A1 = ParseIntSafe(colMap, firstRow, "PIECE_A1");
                        userData.PIECE_A2 = ParseIntSafe(colMap, firstRow, "PIECE_A2");
                        userData.PIECE_A3 = ParseIntSafe(colMap, firstRow, "PIECE_A3");
                        userData.PIECE_B1 = ParseIntSafe(colMap, firstRow, "PIECE_B1");
                        userData.PIECE_B2 = ParseIntSafe(colMap, firstRow, "PIECE_B2");
                        userData.PIECE_B3 = ParseIntSafe(colMap, firstRow, "PIECE_B3");
                        userData.PIECE_C1 = ParseIntSafe(colMap, firstRow, "PIECE_C1");
                        userData.PIECE_C2 = ParseIntSafe(colMap, firstRow, "PIECE_C2");
                        userData.PIECE_C3 = ParseIntSafe(colMap, firstRow, "PIECE_C3");
                        userData.PIECE_D1 = ParseIntSafe(colMap, firstRow, "PIECE_D1");
                        userData.PIECE_D2 = ParseIntSafe(colMap, firstRow, "PIECE_D2");
                        userData.PIECE_D3 = ParseIntSafe(colMap, firstRow, "PIECE_D3");
                        
                        SessionManager.Instance.PieceA1 = Mathf.Max(0, userData.PIECE_A1);
                        SessionManager.Instance.PieceA2 = Mathf.Max(0, userData.PIECE_A2);
                        SessionManager.Instance.PieceA3 = Mathf.Max(0, userData.PIECE_A3);
                        SessionManager.Instance.PieceB1 = Mathf.Max(0, userData.PIECE_B1);
                        SessionManager.Instance.PieceB2 = Mathf.Max(0, userData.PIECE_B2);
                        SessionManager.Instance.PieceB3 = Mathf.Max(0, userData.PIECE_B3);
                        SessionManager.Instance.PieceC1 = Mathf.Max(0, userData.PIECE_C1);
                        SessionManager.Instance.PieceC2 = Mathf.Max(0, userData.PIECE_C2);
                        SessionManager.Instance.PieceC3 = Mathf.Max(0, userData.PIECE_C3);
                        SessionManager.Instance.PieceD1 = Mathf.Max(0, userData.PIECE_D1);
                        SessionManager.Instance.PieceD2 = Mathf.Max(0, userData.PIECE_D2);
                        SessionManager.Instance.PieceD3 = Mathf.Max(0, userData.PIECE_D3);

                        // Why: 현재 진행 중인 모듈 외 다른 컨텐츠의 완료 현황을 확인하기 위함
                        int endCount = 0;
                        string currentModuleEnd = $"END_{SessionManager.Instance.CurrentModuleCode.ToUpper()}"; 

                        foreach (string colName in response.COLUMNS)
                        {
                            if (colName.StartsWith("END_"))
                            {
                                if (colName.Equals(currentModuleEnd, StringComparison.OrdinalIgnoreCase) ||
                                    colName.StartsWith("END_Z", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                string endValue = ParseStringSafe(colMap, firstRow, colName);
                                
                                if (!string.IsNullOrWhiteSpace(endValue) && !endValue.Equals("null", StringComparison.OrdinalIgnoreCase))
                                {
                                    endCount++;
                                }
                            }
                        }

                        SessionManager.Instance.ClearedEndCount = endCount;
                        SessionManager.Instance.IsOtherCartridgeContentsCleared = (endCount >= 3);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log($"[APIManager] 유저 데이터 로드 완료!\n" +
                                  $"- 유저 인덱스(IDX_USER): {userData.IDX_USER}\n" +
                                  $"- 이름 (L/R): {userData.RESERVATION_FIRST_NAME_LEFT} / {userData.RESERVATION_FIRST_NAME_RIGHT}\n" +
                                  $"- 유저 타입 (카트리지+관계): {combinedTypeStr}\n" +
                                  $"- 컬러 (L/R): {userData.COLOR_LEFT} / {userData.COLOR_RIGHT}\n" +
                                  $"- 블록 코드: {userData.BLOCK_CODE}\n" +
                                  $"- 타 콘텐츠 완료 개수: {endCount}개");
#else
                         Debug.Log($"[APIManager] 유저 데이터 로드 완료!\n" +
                                  $"- 유저 인덱스(IDX_USER): {userData.IDX_USER}\n" +
                                  $"- 이름 (L/R): {userData.RESERVATION_FIRST_NAME_LEFT} / {userData.RESERVATION_FIRST_NAME_RIGHT}\n" +
                                  $"- 유저 타입 (카트리지+관계): {combinedTypeStr}\n" +
                                  $"- 컬러 (L/R): {userData.COLOR_LEFT} / {userData.COLOR_RIGHT}\n" +
                                  $"- 블록 코드: {userData.BLOCK_CODE}\n" +
                                  $"- 타 콘텐츠 완료 개수: {endCount}개");
#endif

                        return true; 
                    }
                }
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[APIManager] JSON 파싱 중 에러 발생: {e.Message}");
                return false;
            }
        }

        private int ParseIntSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null)
            {
                string valStr = row[idx].ToString().Trim();
                if (int.TryParse(valStr, out int val)) return val;
            }
            return 0; 
        }

        private string ParseStringSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null) 
            {
                return row[idx].ToString();
            }
            return string.Empty; 
        }

        private ColorData ParseColorSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null)
            {
                if (int.TryParse(row[idx].ToString(), out int val))
                {
                    if (val >= (int)ColorData.NotSet && val <= (int)ColorData.Yellow) 
                    {
                        return (ColorData)val;
                    }
                }
            }
            return ColorData.NotSet; 
        }
        
        /// <summary> 
        /// 서버 업로드를 UniTask 기반으로 처리하며, 제공해주신 A1 로직을 D3 프로젝트 규격에 맞게 적용함.
        /// Why: Step3의 결과물(D3)을 서버로 전송할 때 Raw 바이너리 POST 방식을 사용하기 위함.
        /// </summary>
        public async UniTask<bool> UploadImageAsync(byte[] imageBytes, int idxUser, string uid, string moduleCode)
        {
            if (imageBytes == null || imageBytes.Length == 0) return false;

            string baseUrl = string.Empty;
            if (GameManager.Instance && GameManager.Instance.ApiConfig != null)
            {
                // API 설정에서 uploadFile 경로를 가져옴
                baseUrl = GameManager.Instance.ApiConfig.UploadFileUrl;
            }

            if (string.IsNullOrEmpty(baseUrl) || idxUser <= 0 || string.IsNullOrWhiteSpace(uid))
            {
                Debug.LogWarning($"[APIManager] 업로드 중단: 필수 정보 부족 (idx_user: {idxUser}, uid: {uid})");
                return false;
            }

            string encodedUid = UnityWebRequest.EscapeURL(uid);
            // D3 프로젝트 규격에 따른 URL 파라미터 구성 (A1 로직 참조)
            string url = $"{baseUrl}?idx_user={idxUser}&uid={encodedUid}&code={moduleCode.ToLower()}&type=png";

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                using (UnityWebRequest webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    // Why: 제공해주신 예시와 같이 Raw 바이너리 데이터를 직접 업로드함
                    webRequest.uploadHandler = new UploadHandlerRaw(imageBytes);
                    webRequest.uploadHandler.contentType = "image/png"; 
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.timeout = 15;

                    try
                    {
                        await webRequest.SendWebRequest().ToUniTask();

                        if (webRequest.result == UnityWebRequest.Result.Success)
                        {
                            Debug.Log($"[APIManager] 업로드 성공: {webRequest.responseCode}");
                            return true;
                        }

                        if (attempt < maxRetries - 1)
                        {
                            Debug.LogWarning($"[APIManager] 업로드 실패 ({attempt + 1}/{maxRetries}): {webRequest.error}. {retryDelay}초 후 재시도...");
                            await UniTask.Delay(TimeSpan.FromSeconds(retryDelay));
                        }
                        else
                        {
                            Debug.LogError($"[APIManager] 업로드 최종 실패: {webRequest.error}");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[APIManager] 업로드 중 예외 발생: {e.Message}");
                    }
                }
            }
            return false;
        }
        
        /// <summary> 
        /// 완성된 MP4 영상을 서버로 업로드합니다.
        /// Why: 동영상 파일은 용량이 커 타임아웃을 300초로 길게 잡고, 콘텐츠 타입을 video/mp4로 지정하여 전송하기 위함.
        /// </summary>
        public async UniTask<bool> UploadVideoAsync(byte[] videoBytes, int idxUser, string uid, string moduleCode)
        {
            if (videoBytes == null || videoBytes.Length == 0) return false;

            string baseUrl = string.Empty;
            if (GameManager.Instance && GameManager.Instance.ApiConfig != null)
            {
                baseUrl = GameManager.Instance.ApiConfig.UploadFileUrl;
            }

            if (string.IsNullOrEmpty(baseUrl) || idxUser <= 0 || string.IsNullOrWhiteSpace(uid))
            {
                Debug.LogWarning($"[APIManager] 영상 업로드 중단: 필수 정보 부족 (idx_user: {idxUser}, uid: {uid})");
                return false;
            }

            string encodedUid = UnityWebRequest.EscapeURL(uid);
            // URL 파라미터에 type=mp4 를 적용
            string url = $"{baseUrl}?idx_user={idxUser}&uid={encodedUid}&code={moduleCode.ToLower()}&type=mp4";

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                using (UnityWebRequest webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(videoBytes);
                    webRequest.uploadHandler.contentType = "video/mp4"; 
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.timeout = 300; // 타임아웃 300초 적용

                    try
                    {
                        await webRequest.SendWebRequest().ToUniTask();

                        if (webRequest.result == UnityWebRequest.Result.Success)
                        {
                            Debug.Log($"[APIManager] 영상 업로드 성공: {webRequest.responseCode}");
                            return true;
                        }

                        if (attempt < maxRetries - 1)
                        {
                            Debug.LogWarning($"[APIManager] 영상 업로드 실패 ({attempt + 1}/{maxRetries}): {webRequest.error}. {retryDelay}초 후 재시도...");
                            await UniTask.Delay(TimeSpan.FromSeconds(retryDelay));
                        }
                        else
                        {
                            Debug.LogError($"[APIManager] 영상 업로드 최종 실패: {webRequest.error}");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[APIManager] 영상 업로드 중 예외 발생: {e.Message}");
                    }
                }
            }

            return false;
        }
    }
}