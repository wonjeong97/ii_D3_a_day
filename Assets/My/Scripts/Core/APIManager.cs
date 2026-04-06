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
    /// <summary>
    /// 컬러 데이터 구분을 위한 열거형.
    /// </summary>
    public enum ColorData { NotSet = -1, Cyan = 0, Pink = 1, Orange = 2, Green = 3, Red = 4, Yellow = 5 }
    
    /// <summary>
    /// 서버로부터 수신된 유저 정보를 담는 구조체.
    /// </summary>
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

    /// <summary>
    /// API 응답 테이블 형식을 파싱하기 위한 클래스.
    /// </summary>
    public class ApiTableResponse
    {
        public List<string> COLUMNS { get; set; }
        public List<List<object>> DATA { get; set; } 
    }

    /// <summary>
    /// 서버와의 REST API 통신을 전담하는 매니저 클래스.
    /// 유저 데이터 조회, 이미지 및 영상 업로드 시 재시도 로직과 예외 처리를 수행함.
    /// </summary>
    public class APIManager : MonoBehaviour
    {   
        public static APIManager Instance { get; private set; }
        
        private string userUid;

        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries = 3;
        [SerializeField] private float retryDelay = 1.0f;
        
        [Header("Upload Retry Settings")]
        [SerializeField] private int uploadMaxRetries = 3;
        [SerializeField] private float uploadRetryDelay = 2.0f;
        
        /// <summary>
        /// 싱글톤 인스턴스를 생성하고 씬 전환 시 파괴되지 않도록 설정함.
        /// </summary>
        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 비동기 데이터 조회를 시작함.
        /// </summary>
        /// <param name="uid">조회할 유저의 고유 식별자.</param>
        public void FetchData(string uid) 
        { 
            FetchDataAsync(uid, 15.0f, CancellationToken.None).Forget(); 
        }
        
        /// <summary>
        /// 서버로부터 유저 데이터를 비동기로 조회하고 세션 매니저에 반영함.
        /// 에디터 환경에서는 통신 없이 가상 데이터를 생성하여 개발 편의성을 높임.
        /// </summary>
        /// <param name="uid">유저 UID.</param>
        /// <param name="timeoutSeconds">타임아웃 시간.</param>
        /// <param name="cancellationToken">취소 토큰.</param>
        /// <returns>성공 여부.</returns>
       [ContextMenu("Fetch API Data")]
        public async UniTask<bool> FetchDataAsync(string uid, float timeoutSeconds = 15.0f, CancellationToken cancellationToken = default)
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
            Debug.Log("[APIManager] 에디터 모드: 가상 세션 유지");
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
                Debug.LogError("[APIManager] API 설정 로드 실패");
                return false;
            }

            string requestUrl = $"{config.GetUserUrl}?uid={userUid}";
            
            using (CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                CancellationToken linkedToken = timeoutCts.Token;
                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    linkedToken.ThrowIfCancellationRequested();
                    using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
                    {
                        webRequest.timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));
                        
                        try
                        {
                            await webRequest.SendWebRequest().ToUniTask(cancellationToken: linkedToken);

                            if (webRequest.result == UnityWebRequest.Result.Success)
                            {
                                return await ParseAndProcessDataAsync(webRequest.downloadHandler.text);
                            }

                            if (attempt < maxRetries - 1)
                            {
                                Debug.LogWarning($"[APIManager] 조회 실패 ({attempt + 1}/{maxRetries}): {retryDelay}초 후 재시도");
                                await UniTask.Delay(TimeSpan.FromSeconds(retryDelay), cancellationToken: linkedToken);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            Debug.LogWarning("[APIManager] 작업 취소됨");
                            throw;
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[APIManager] 통신 예외 발생 ({attempt + 1}/{maxRetries}): {e.Message}");
                            if (attempt < maxRetries - 1) 
                            {
                                await UniTask.Delay(TimeSpan.FromSeconds(retryDelay), cancellationToken: linkedToken);
                            }
                        }
                    }
                }
            }
            
            return false;
        }

        /// <summary>
        /// 수신된 JSON을 파싱하여 세션 매니저의 각 필드에 값을 할당함.
        /// 카트리지 정보와 관계 정보를 조합하여 유저 타입을 결정하고 콘텐츠 클리어 여부를 계산함.
        /// </summary>
        /// <param name="jsonString">서버 응답 JSON 문자열.</param>
        /// <returns>파싱 및 처리 성공 여부.</returns>
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
                    userData.RESERVATION_LAST_NAME_LEFT = ParseStringSafe(colMap, firstRow, "RESERVATION_LAST_NAME_LEFT");
                    userData.RESERVATION_FIRST_NAME_RIGHT = ParseStringSafe(colMap, firstRow, "RESERVATION_FIRST_NAME_RIGHT");
                    userData.RESERVATION_LAST_NAME_RIGHT = ParseStringSafe(colMap, firstRow, "RESERVATION_LAST_NAME_RIGHT");
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
                        
                        if (!string.IsNullOrEmpty(userData.RESERVATION_LAST_NAME_LEFT))
                        {
                            SessionManager.Instance.PlayerALastName = userData.RESERVATION_LAST_NAME_LEFT;
                        }
                        
                        if (!string.IsNullOrEmpty(userData.RESERVATION_LAST_NAME_RIGHT))
                        {
                            SessionManager.Instance.PlayerBLastName = userData.RESERVATION_LAST_NAME_RIGHT;
                        }
                        
                        SessionManager.Instance.PlayerAColor = userData.COLOR_LEFT;
                        SessionManager.Instance.PlayerBColor = userData.COLOR_RIGHT;

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
                        
                        Debug.Log($"유저 데이터 로드 완료\n" +
                                  $"- 유저 인덱스(IDX_USER): {userData.IDX_USER}\n" +
                                  $"- 이름 (L/R): {userData.RESERVATION_FIRST_NAME_LEFT} / {userData.RESERVATION_FIRST_NAME_RIGHT}\n" +
                                  $"- UID (L/R): {userData.UID_LEFT} / {userData.UID_RIGHT}\n" +
                                  $"- 컬러 (L/R): {userData.COLOR_LEFT} / {userData.COLOR_RIGHT}\n" +
                                  $"- 언어/관계: {userData.LANG} / {userData.RELATION}\n" +
                                  $"- 카트리지: {userData.CARTRIDGE}\n" +
                                  $"- 블록 코드: {userData.BLOCK_CODE}");
                        
                        if (!SessionManager.Instance) Debug.Log("[APIManager]: SessionManager is not valid.");
                        
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

                        return true; 
                    }
                }
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[APIManager] JSON 파싱 에러: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 응답 로우에서 지정된 컬럼명에 해당하는 정수값을 안전하게 추출함.
        /// </summary>
        private int ParseIntSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null)
            {
                string valStr = row[idx].ToString().Trim();
                if (int.TryParse(valStr, out int val)) return val;
            }
            return 0; 
        }

        /// <summary>
        /// 응답 로우에서 지정된 컬럼명에 해당하는 문자열을 안전하게 추출함.
        /// </summary>
        private string ParseStringSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null) 
            {
                return row[idx].ToString();
            }
            return string.Empty; 
        }

        /// <summary>
        /// 응답 로우의 정수값을 컬러 데이터 열거형으로 변환함.
        /// </summary>
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
        /// 촬영된 이미지 바이트 데이터를 서버로 업로드함.
        /// </summary>
        /// <param name="imageBytes">이미지 데이터.</param>
        /// <param name="idxUser">유저 인덱스.</param>
        /// <param name="uid">유저 UID.</param>
        /// <param name="moduleCode">모듈 코드.</param>
        /// <param name="cancellationToken">취소 토큰.</param>
        /// <returns>업로드 성공 여부.</returns>
        public async UniTask<bool> UploadImageAsync(byte[] imageBytes, int idxUser, string uid, string moduleCode, CancellationToken cancellationToken = default)
        {
            if (imageBytes == null || imageBytes.Length == 0) return false;

            string baseUrl = string.Empty;
            if (GameManager.Instance && GameManager.Instance.ApiConfig != null)
            {
                baseUrl = GameManager.Instance.ApiConfig.UploadFileUrl;
            }

            if (string.IsNullOrEmpty(baseUrl) || idxUser <= 0 || string.IsNullOrWhiteSpace(uid))
            {
                Debug.LogWarning("[APIManager] 업로드 필수 정보 부족");
                return false;
            }

            string encodedUid = UnityWebRequest.EscapeURL(uid);
            string url = $"{baseUrl}?idx_user={idxUser}&uid={encodedUid}&code={moduleCode}&type=png";

            for (int attempt = 0; attempt < uploadMaxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (UnityWebRequest webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(imageBytes);
                    webRequest.uploadHandler.contentType = "image/png"; 
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.timeout = 15;

                    try
                    {
                        await webRequest.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

                        if (webRequest.result == UnityWebRequest.Result.Success)
                        {
                            Debug.Log($"[APIManager] 업로드 성공: {webRequest.responseCode}");
                            return true;
                        }

                        if (attempt < uploadMaxRetries - 1)
                        {
                            Debug.LogWarning($"[APIManager] 업로드 실패 ({attempt + 1}/{uploadMaxRetries}): {uploadRetryDelay}초 후 재시도");
                            await UniTask.Delay(TimeSpan.FromSeconds(uploadRetryDelay), cancellationToken: cancellationToken);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.LogWarning("[APIManager] 업로드 작업 취소됨");
                        throw;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[APIManager] 업로드 중 예외 발생: {e.Message}");
                        if (attempt < uploadMaxRetries - 1)
                        {
                            await UniTask.Delay(TimeSpan.FromSeconds(uploadRetryDelay), cancellationToken: cancellationToken);
                        }
                    }
                }
            }
            return false;
        }
        
        /// <summary>
        /// 생성된 비디오 파일을 서버로 업로드함.
        /// 대용량 파일이므로 타임아웃을 길게 설정하여 안정성을 확보함.
        /// </summary>
        /// <param name="filePath">로컬 비디오 파일 경로.</param>
        /// <param name="idxUser">유저 인덱스.</param>
        /// <param name="uid">유저 UID.</param>
        /// <param name="moduleCode">모듈 코드.</param>
        /// <param name="cancellationToken">취소 토큰.</param>
        /// <returns>업로드 성공 여부.</returns>
        public async UniTask<bool> UploadVideoAsync(string filePath, int idxUser, string uid, string moduleCode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            {   
                Debug.LogWarning("[APIManager] 업로드할 비디오 파일을 찾지 못했습니다.");
                return false;
            }

            string baseUrl = string.Empty;
            if (GameManager.Instance && GameManager.Instance.ApiConfig != null)
            {
                baseUrl = GameManager.Instance.ApiConfig.UploadFileUrl;
            }

            if (string.IsNullOrEmpty(baseUrl) || idxUser <= 0 || string.IsNullOrWhiteSpace(uid))
            {
                Debug.LogWarning("[APIManager] 영상 업로드 필수 정보 부족");
                return false;
            }

            string encodedUid = UnityWebRequest.EscapeURL(uid);
            string url = $"{baseUrl}?idx_user={idxUser}&uid={encodedUid}&code={moduleCode}&type=mp4";

            for (int attempt = 0; attempt < uploadMaxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (UnityWebRequest webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    webRequest.uploadHandler = new UploadHandlerFile(filePath);
                    webRequest.uploadHandler.contentType = "video/mp4"; 
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.timeout = 300; 

                    try
                    {
                        await webRequest.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

                        if (webRequest.result == UnityWebRequest.Result.Success)
                        {
                            Debug.Log($"[APIManager] 영상 업로드 성공: {webRequest.responseCode}");
                            return true;
                        }

                        if (attempt < uploadMaxRetries - 1)
                        {
                            Debug.LogWarning($"[APIManager] 영상 업로드 실패 ({attempt + 1}/{uploadMaxRetries}): {uploadRetryDelay}초 후 재시도");
                            await UniTask.Delay(TimeSpan.FromSeconds(uploadRetryDelay), cancellationToken: cancellationToken);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.LogWarning("[APIManager] 영상 업로드 작업 취소됨");
                        throw;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[APIManager] 영상 업로드 중 예외 발생: {e.Message}");
                        if (attempt < uploadMaxRetries - 1)
                        {
                            await UniTask.Delay(TimeSpan.FromSeconds(uploadRetryDelay), cancellationToken: cancellationToken);
                        }
                    }
                }
            }

            return false;
        }
    }
}