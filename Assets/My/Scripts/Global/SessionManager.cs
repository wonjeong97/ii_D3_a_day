using System;
using System.IO;
using My.Scripts.Core;
using UnityEngine;

namespace My.Scripts.Global
{
    /// <summary>
    /// 카트리지 테마와 관계 번호를 조합한 유저 타입 정의.
    /// </summary>
    public enum UserType
    {
        A1, A2, A3, A4, A5, A6,
        B1, B2, B3, B4, B5, B6,
        C1, C2, C3, C4, C5, C6,
        D1, D2, D3, D4, D5, D6
    }

    /// <summary>
    /// 현재 체험 중인 유저의 세션 데이터와 진행 상태를 전역적으로 관리하는 매니저.
    /// 서버에서 받은 유저 정보, 컬러, 획득 조각 수 등을 씬 간에 유지하기 위함.
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        public string SessionFolderPath { get; set; }
        
        [Header("Editor Test Mode")]
        public bool useEditorTestData;
        
        public string testNameA;
        public string testNameB;
        
        public ColorData testColorA;
        public ColorData testColorB;
        
        public string testCartridge;
        
        [Range(1, 6)] 
        public int testRelation;

        public int CurrentUserIdx { get; set; } 
        public string PlayerAUid { get; set; }
        public string PlayerBUid { get; set; }
        public string CurrentLanguage { get; set; }
        public string BlockCode { get; set; }
        
        public string PlayerAFirstName { get; set; }
        public string PlayerBFirstName { get; set; }
        
        public ColorData PlayerAColor { get; set; }
        public ColorData PlayerBColor { get; set; }
        
        public UserType CurrentUserType { get; set; }
        public string CurrentModuleCode { get; set; }
        public string Cartridge { get; set; }
        
        public bool IsOtherCartridgeContentsCleared { get; set; }
        public int ClearedEndCount { get; set; } 

        public string Step2MainTheme { get; set; }
        public int Step2SubTheme { get; set; }

        public int PieceA1 { get; set; } public int PieceA2 { get; set; } public int PieceA3 { get; set; }
        public int PieceB1 { get; set; } public int PieceB2 { get; set; } public int PieceB3 { get; set; }
        public int PieceC1 { get; set; } public int PieceC2 { get; set; } public int PieceC3 { get; set; }
        public int PieceD1 { get; set; } public int PieceD2 { get; set; } public int PieceD3 { get; set; }
        
        /// <summary>
        /// 데이터베이스의 블록 코드를 기반으로 이전에 획득한 조각의 총합을 계산함.
        /// 현재 진행 중인 모듈(D3) 결과는 아직 확정 전이므로 합산에서 제외하여 누적 보상 연출에 활용함.
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

        /// <summary>
        /// 싱글톤 초기화 및 세션 상태를 초기 상태로 리셋함.
        /// </summary>
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

        /// <summary>
        /// 유니티 에디터 환경에서 테스트를 위한 가상 유저 데이터를 주입함.
        /// </summary>
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

        /// <summary>
        /// 모든 세션 필드와 진행 데이터를 기본값으로 비움.
        /// 이전 체험자의 데이터가 다음 체험자에게 노출되지 않도록 철저히 초기화하기 위함.
        /// </summary>
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

            // # TODO: 로컬 사진 저장 경로(C:\UnitySharedPicture)를 하드코딩하지 않고 설정 파일에서 관리하도록 개선할 것.
            string rootPath = @"C:\UnitySharedPicture";
            SessionFolderPath = Path.Combine(rootPath, DateTime.Now.ToString("yyyy-MM-dd"));

#if UNITY_EDITOR
            if (useEditorTestData) ApplyEditorTestData();
#endif
        }
    }
}