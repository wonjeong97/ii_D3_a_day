using System;

namespace My.Scripts.Core.Data
{
    /// <summary>
    /// 외부 구성 파일에서 로드되는 API 엔드포인트 설정 데이터 모델.
    /// 서버 통신에 필요한 기본 URL과 각 기능별 파일 경로를 관리함.
    /// </summary>
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
        
        /// <summary>
        /// 시스템 환경 변수 또는 로컬 설정값 중 유효한 베이스 URL을 반환함.
        /// 실행 환경(로컬/서버)에 따라 통신 대상을 유연하게 변경하기 위함.
        /// </summary>
        /// <returns>최종 결정된 베이스 URL 문자열.</returns>
        public string GetBaseUrl()
        {
            string envUrl = Environment.GetEnvironmentVariable("BASE_URL");
            if (!string.IsNullOrEmpty(envUrl)) return envUrl;
            return baseUrl;
        }

        /// <summary>
        /// 베이스 URL과 세부 경로를 결합하여 완전한 URI 형식을 생성함.
        /// 경로 구분자 중복이나 누락으로 인한 통신 오류를 방지하기 위함.
        /// </summary>
        /// <param name="baseUri">기본 도메인 주소.</param>
        /// <param name="path">기능별 세부 경로.</param>
        /// <param name="fallbackPath">세부 경로가 비어있을 경우 사용할 기본값.</param>
        /// <returns>결합된 전체 URL 주소.</returns>
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
        public string UploadFileUrl => BuildUrl(GetBaseUrl(), uploadFile, "/uploadFile.cfm");
    }
}