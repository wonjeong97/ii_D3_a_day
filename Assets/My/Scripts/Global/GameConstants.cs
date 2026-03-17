namespace My.Scripts.Global
{
    /// <summary> 게임 전역 상수 관리 클래스 </summary>
    public static class GameConstants
    {
        public static class Scene
        { 
            public const string Title = "00_Title"; 
            public const string Tutorial = "01_Tutorial"; 
            public const string PlayTutorial = "02_PlayTutorial"; 
            public const string Step1 = "03_Step1";
        }

        public static class Path
        {
            public const string JsonSetting = "Settings"; 
            public const string Title = "JSON/Title"; 
            public const string Tutorial = "JSON/Tutorial";            
            public const string PlayTutorial = "JSON/PlayTutorial";
            public const string TcpSetting = "JSON/TcpSetting";
            public const string Step1 = "JSON/Step1";
        }
    }
}