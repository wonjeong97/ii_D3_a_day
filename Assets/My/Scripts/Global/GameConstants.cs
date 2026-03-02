namespace My.Scripts.Global
{
    /// <summary> 게임 전역 상수 관리 클래스 </summary>
    public static class GameConstants
    {
        /// <summary> 씬 이름 상수 모음 </summary>
        public static class Scene
        { 
            public const string Title = "00_Title"; // 타이틀 씬
            public const string Tutorial = "01_Tutorial"; // 튜토리얼 씬
            public const string PlayTutorial = "02_Play_Tutorial"; // 플레이 튜토리얼 씬
        }

        /// <summary> 리소스 경로 상수 모음 </summary>
        public static class Path
        {
            public const string JsonSetting = "Settings"; // 기본 설정 JSON
            public const string Title = "JSON/Title"; // 타이틀 데이터
            public const string Tutorial = "JSON/Tutorial";            
        }
    }
}