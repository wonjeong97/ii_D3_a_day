using System;
using System.IO;
using My.Scripts.Core;
using UnityEngine;

namespace My.Scripts.Global
{
    public enum UserType
    {
        A1, A2, A3, A4, A5, A6,
        B1, B2, B3, B4, B5, B6,
        C1, C2, C3, C4, C5, C6,
        D1, D2, D3, D4, D5, D6
    }

    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }
        
        [Header("Network Share Settings")]
        public string networkSharedRootPath = @"\\192.168.0.44\SharedPicture";

        public string SessionFolderPath { get; set; } = string.Empty;
        
        [Header("Editor Test Mode")]
        public bool useEditorTestData = false;
        
        public string testNameA = "PlayerA";
        public string testNameB = "PlayerB";
        
        public ColorData testColorA = ColorData.Cyan;
        public ColorData testColorB = ColorData.Pink;
        
        public string testCartridge = "A";
        
        [Range(1, 6)] 
        public int testRelation = 1;

        public int CurrentUserId { get; set; } 
        public string PlayerAUid { get; set; } = string.Empty;
        public string PlayerBUid { get; set; } = string.Empty;
        public string CurrentLanguage { get; set; } = "ko";
        
        public string PlayerAFirstName { get; set; } = "NoNameA";
        public string PlayerBFirstName { get; set; } = "NoNameB";
        
        public ColorData PlayerAColor { get; set; } = ColorData.NotSet;
        public ColorData PlayerBColor { get; set; } = ColorData.NotSet;
        
        public UserType CurrentUserType { get; set; } = UserType.A1;
        public string CurrentModuleCode { get; set; } = "d3";
        public string Cartridge { get; set; } = string.Empty;
        
        public bool IsOtherCartridgeContentsCleared { get; set; } = false;
        public int ClearedEndCount { get; set; } = 0; 

        public string Step2MainTheme { get; set; } = "Sea";
        public int Step2SubTheme { get; set; } = 1;

        public int PieceA1 { get; set; } public int PieceA2 { get; set; } public int PieceA3 { get; set; }
        public int PieceB1 { get; set; } public int PieceB2 { get; set; } public int PieceB3 { get; set; }
        public int PieceC1 { get; set; } public int PieceC2 { get; set; } public int PieceC3 { get; set; }
        public int PieceD1 { get; set; } public int PieceD2 { get; set; } public int PieceD3 { get; set; }
        
        public int TotalPieces => PieceA1 + PieceA2 + PieceA3 + 
                                  PieceB1 + PieceB2 + PieceB3 + 
                                  PieceC1 + PieceC2 + PieceC3 + 
                                  PieceD1 + PieceD2 + PieceD3;

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                ClearSession(); 
                ApplyEditorTestData();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void ApplyEditorTestData()
        {
#if UNITY_EDITOR
            if (useEditorTestData)
            {
                CurrentUserId = 9999;
                PlayerAFirstName = testNameA;
                PlayerBFirstName = testNameB;
                PlayerAColor = testColorA;
                PlayerBColor = testColorB;
                Cartridge = testCartridge.Trim().ToUpper();

                string combinedTypeStr = $"{Cartridge}{testRelation}";
                if (Enum.TryParse(combinedTypeStr, out UserType parsedType))
                {
                    CurrentUserType = parsedType;
                }
                else
                {
                    CurrentUserType = UserType.A1;
                }
            }
#endif
        }

        public void ClearSession()
        {
            CurrentUserId = 0;
            PlayerAUid = string.Empty;
            PlayerBUid = string.Empty;
            CurrentLanguage = "ko";
            
            PlayerAFirstName = "NoNameA";
            PlayerBFirstName = "NoNameB";
            
            PlayerAColor = ColorData.NotSet;
            PlayerBColor = ColorData.NotSet;

            CurrentUserType = UserType.A1;
            CurrentModuleCode = "d3";
            Cartridge = string.Empty;
            
            IsOtherCartridgeContentsCleared = false;
            ClearedEndCount = 0; 

            Step2MainTheme = "Sea";
            Step2SubTheme = 1;

            PieceA1 = 0; PieceA2 = 0; PieceA3 = 0;
            PieceB1 = 0; PieceB2 = 0; PieceB3 = 0;
            PieceC1 = 0; PieceC2 = 0; PieceC3 = 0;
            PieceD1 = 0; PieceD2 = 0; PieceD3 = 0;

            string rootPath = networkSharedRootPath;

            // 설정한 값이 null이거나 비어있는 경우 fallback 대신 디버그 로그 출력
            if (string.IsNullOrEmpty(rootPath))
            {
                UnityEngine.Debug.LogWarning("[SessionManager] networkSharedRootPath가 설정되지 않았습니다. 기본 폴더(MyPictures)에 저장됩니다.");
                rootPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            }
            
            SessionFolderPath = Path.Combine(rootPath, DateTime.Now.ToString("yy-MM-dd"));

#if UNITY_EDITOR
            if (useEditorTestData) ApplyEditorTestData();
#endif
        }
    }
}