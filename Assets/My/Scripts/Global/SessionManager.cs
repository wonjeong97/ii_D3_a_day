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

        public int CurrentUserIdx { get; set; } 
        public string PlayerAUid { get; set; } = string.Empty;
        public string PlayerBUid { get; set; } = string.Empty;
        public string CurrentLanguage { get; set; } = "ko";
        public string BlockCode { get; set; } = string.Empty;
        
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
        
        /// <summary>
        /// 데이터베이스의 블록 코드를 기반으로 획득한 조각의 총합을 반환합니다.
        /// Why: 유저가 겪은 컨텐츠(BlockCode)의 조각만 유효하며, 현재 진행 중인 모듈(D3)은 합산에서 제외하기 위함.
        /// </summary>
        public int TotalPieces
        {
            get
            {
                if (string.IsNullOrWhiteSpace(BlockCode)) 
                {
                    return PieceA1 + PieceA2 + PieceA3 +
                           PieceB1 + PieceB2 + PieceB3 +
                           PieceC1 + PieceC2 + PieceC3 +
                           PieceD1 + PieceD2; 
                }

                int sum = 0;
                string[] blocks = BlockCode.Split(',');
                string currentModule = CurrentModuleCode.ToUpper();

                foreach (string b in blocks)
                {
                    string block = b.Trim().ToUpper();
            
                    if (block == currentModule) 
                    {
                        continue;
                    }

                    switch (block)
                    {
                        case "A1": sum += PieceA1; break;
                        case "A2": sum += PieceA2; break;
                        case "A3": sum += PieceA3; break;
                        case "B1": sum += PieceB1; break;
                        case "B2": sum += PieceB2; break;
                        case "B3": sum += PieceB3; break;
                        case "C1": sum += PieceC1; break;
                        case "C2": sum += PieceC2; break;
                        case "C3": sum += PieceC3; break;
                        case "D1": sum += PieceD1; break;
                        case "D2": sum += PieceD2; break;
                        case "D3": sum += PieceD3; break;
                    }
                }
                return sum;
            }
        }

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
                CurrentUserIdx = 9999;
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
            CurrentUserIdx = 0;
            PlayerAUid = string.Empty;
            PlayerBUid = string.Empty;
            BlockCode = string.Empty;
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

            string rootPath = @"C:\UnitySharedPicture";
            SessionFolderPath = Path.Combine(rootPath, DateTime.Now.ToString("yyyy-MM-dd"));

#if UNITY_EDITOR
            if (useEditorTestData) ApplyEditorTestData();
#endif
        }
    }
}