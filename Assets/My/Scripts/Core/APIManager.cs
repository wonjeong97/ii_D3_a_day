using System;
using System.Collections;
using System.Collections.Generic;
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

        public void FetchData(string uid) { FetchDataAsync(uid).Forget(); }
        
        [ContextMenu("Fetch API Data")]
        public async UniTask<bool> FetchDataAsync(string uid)
        {
#if UNITY_EDITOR
            if (SessionManager.Instance)
            {
                // SessionManager의 에디터 테스트 데이터 사용이 꺼져있을 경우에만 기본값을 강제 세팅함
                if (!SessionManager.Instance.useEditorTestData)
                {
                    SessionManager.Instance.CurrentUserId = -1;
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
                if (GameManager.Instance && config != null) GameManager.Instance.ApiConfig = config;
            }

            if (config == null)
            {
                Debug.LogError("[APIManager] API 설정을 찾을 수 없습니다.");
                return false;
            }

            string requestUrl = $"{config.GetUserUrl}?uid={userUid}";
            int maxRetries = 10;
            
            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
                {
                    webRequest.timeout = 10; 
                    
                    try
                    {
                        await webRequest.SendWebRequest().ToUniTask();

                        if (webRequest.result == UnityWebRequest.Result.Success)
                            return await ParseAndProcessDataAsync(webRequest.downloadHandler.text);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[APIManager] FetchDataAsync 통신 예외 발생 ({i + 1}/{maxRetries}): {e.Message}");
                    }
                    
                    if (i < maxRetries - 1) await UniTask.Delay(TimeSpan.FromSeconds(1f));
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

                    if (SessionManager.Instance)
                    {   
                        SessionManager.Instance.CurrentUserId = userData.IDX_USER;
                        SessionManager.Instance.Cartridge = userData.CARTRIDGE; 
                        SessionManager.Instance.PlayerAUid = userData.UID_LEFT;
                        SessionManager.Instance.PlayerBUid = userData.UID_RIGHT;

                        if (!string.IsNullOrWhiteSpace(userData.LANG)) 
                            SessionManager.Instance.CurrentLanguage = userData.LANG.Trim();

                        if (!string.IsNullOrEmpty(userData.RESERVATION_FIRST_NAME_LEFT))
                            SessionManager.Instance.PlayerAFirstName = userData.RESERVATION_FIRST_NAME_LEFT;
                        if (!string.IsNullOrEmpty(userData.RESERVATION_FIRST_NAME_RIGHT))
                            SessionManager.Instance.PlayerBFirstName = userData.RESERVATION_FIRST_NAME_RIGHT;
                        
                        SessionManager.Instance.PlayerAColor = userData.COLOR_LEFT;
                        SessionManager.Instance.PlayerBColor = userData.COLOR_RIGHT;

                        string cartridgeStr = string.IsNullOrEmpty(userData.CARTRIDGE) ? "A" : userData.CARTRIDGE.Trim().ToUpper();
                        int relationInt = userData.RELATION;
                        string typeStr = $"{cartridgeStr}{relationInt}";

                        if (Enum.TryParse(typeStr, out UserType parsedType))
                        {
                            SessionManager.Instance.CurrentUserType = parsedType;
                        }
                        else
                        {
                            SessionManager.Instance.CurrentUserType = UserType.A1;
                        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log($"[APIManager] 유저 데이터 로드 완료!\n" +
                                  $"- 유저 인덱스(IDX_USER): {userData.IDX_USER}\n" +
                                  $"- 이름 (L/R): {userData.RESERVATION_FIRST_NAME_LEFT} / {userData.RESERVATION_FIRST_NAME_RIGHT}\n" +
                                  $"- 유저 타입 (카트리지+관계): {typeStr}\n" +
                                  $"- 컬러 (L/R): {userData.COLOR_LEFT} / {userData.COLOR_RIGHT}");
#else
                        Debug.Log($"[APIManager] 유저 데이터 로드 완료! (Masked)\n" +
                                  $"- 컬러 (L/R): {userData.COLOR_LEFT} / {userData.COLOR_RIGHT}\n" +
                                  $"- 언어/관계: {userData.LANG} / {userData.RELATION}\n" +
                                  $"- 카트리지: {userData.CARTRIDGE}");
#endif
                        
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

                        SessionManager.Instance.IsOtherCartridgeContentsCleared = ParseOtherCartridgeClearState(response, firstRow);

                        return true; 
                    }
                }
                return false;
            }
            catch (Exception)
            {
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
                return row[idx].ToString();
            return string.Empty; 
        }

        private ColorData ParseColorSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null)
            {
                if (int.TryParse(row[idx].ToString(), out int val))
                {
                    if (val >= (int)ColorData.NotSet && val <= (int)ColorData.Yellow) 
                        return (ColorData)val;   
                }
            }
            return ColorData.NotSet; 
        }

        private bool ParseOtherCartridgeClearState(ApiTableResponse resp, List<object> row)
        {   
            Dictionary<string, int> map = new Dictionary<string, int>();
            for (int i = 0; i < resp.COLUMNS.Count; i++) map[resp.COLUMNS[i]] = i;
            
            int clearCount = 0;
            string currentCode = SessionManager.Instance ? SessionManager.Instance.CurrentModuleCode.ToUpper() : "D3";

            foreach (string colName in resp.COLUMNS)
            {
                if (colName.StartsWith("END_", StringComparison.OrdinalIgnoreCase))
                {
                    string code = colName.Substring(4).ToUpper(); 
                    
                    if (code == currentCode || code.StartsWith("Z")) 
                        continue;
                    
                    string val = ParseStringSafe(map, row, colName);
                    if (!string.IsNullOrWhiteSpace(val) && !val.Equals("null", StringComparison.OrdinalIgnoreCase)) 
                    {
                        clearCount++;
                    }
                }
            }
            
            return clearCount >= 3;
        }
    }
}