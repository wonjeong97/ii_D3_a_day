namespace My.Scripts.Global
{
    /// <summary>
    /// 프로젝트 전역에서 사용되는 상수 값들을 관리하는 클래스.
    /// 하드코딩으로 인한 오타를 방지하고 유지보수 효율성을 높이기 위함.
    /// </summary>
    public static class GameConstants
    {
        /// <summary>
        /// 빌드 설정에 등록된 씬 이름 정의.
        /// SceneManager를 통한 씬 전환 시 정확한 문자열 참조를 보장함.
        /// </summary>
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
            public const string Test = "TestScene";
        }

        /// <summary>
        /// 외부 리소스 및 구성 파일 로드 시 사용되는 경로 정의.
        /// Resources 폴더 또는 StreamingAssets 내의 JSON 파일 위치를 일괄 관리함.
        /// </summary>
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
            public const string PlayVideo = "JSON/PlayVideo";
            public const string Ending = "JSON/Ending";
            public const string ApiSetting = "JSON/API";
            public const string CameraSetting = "JSON/CameraSetting";
        }
    }
}