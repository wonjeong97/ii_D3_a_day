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
            public const string Step2 = "04_Step2";
            public const string Step3 = "05_Step3";
            public const string PlayVideo = "06_PlayVideo";
            public const string Ending = "07_Ending"; 
        }

        public static class Path
        {
            public const string JsonSetting = "Settings"; 
            public const string Title = "JSON/Title"; 
            public const string Tutorial = "JSON/Tutorial";            
            public const string PlayTutorial = "JSON/PlayTutorial";
            public const string TcpSetting = "JSON/TcpSetting";
            public const string Step1 = "JSON/Step1";
            public const string Step2 = "JSON/Step2";
            public const string Step3 = "JSON/Step3";
            public const string Ending = "JSON/Ending";
            public const string ApiSetting = "JSON/Api";
        }
    }
}