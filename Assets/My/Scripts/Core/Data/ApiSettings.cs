using System;

namespace My.Scripts.Core.Data
{
    [Serializable]
    public class ApiSettings
    {
        public string baseUrl;
        public string getUser;
        public string updateTime;
        public string updateValue;
        public string updatePiece;
        public string checkRoomState;
        public string getCurrentRoomUser;
        public string uploadFile;
        public string exitRoom;
        public string resetStart;
        
        public string GetBaseUrl()
        {
            string envUrl = Environment.GetEnvironmentVariable("BASE_URL");
            if (!string.IsNullOrEmpty(envUrl)) return envUrl;
            return baseUrl;
        }

        private static string BuildUrl(string baseUri, string path, string fallbackPath = null)
        {
            string finalPath = string.IsNullOrWhiteSpace(path) ? fallbackPath : path;
            
            if (string.IsNullOrWhiteSpace(baseUri) || string.IsNullOrWhiteSpace(finalPath))
                return string.Empty;

            return $"{baseUri.TrimEnd('/')}/{finalPath.TrimStart('/')}";
        }

        public string GetUserUrl => BuildUrl(GetBaseUrl(), getUser, "/getUser.cfm");
        public string UpdateTimeUrl => BuildUrl(GetBaseUrl(), updateTime, "/updateTime.cfm");
        public string UpdateValueUrl => BuildUrl(GetBaseUrl(), updateValue, "/updateValue.cfm");
        public string UpdatePieceUrl => BuildUrl(GetBaseUrl(), updatePiece, "/updatePiece.cfm");
        public string CheckRoomStateUrl => BuildUrl(GetBaseUrl(), checkRoomState, "/checkRoomState.cfm");
        public string GetCurrentRoomUserUrl => BuildUrl(GetBaseUrl(), getCurrentRoomUser, "/getCurrentRoomUser.cfm");
        public string ExitRoomUrl => BuildUrl(GetBaseUrl(), exitRoom, "/exitRoom.cfm");
        public string ResetStartUrl => BuildUrl(GetBaseUrl(), resetStart, "/resetStart.cfm");
    }
}