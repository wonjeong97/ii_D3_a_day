using System;
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

    /// <summary>
    /// 전역 유저 세션 및 상태 데이터를 관리함.
    /// Why: API 응답 결과를 저장하고 씬 간 데이터 공유 및 에디터 테스트 환경을 제공하기 위함.
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        [Header("Editor Test Mode")]
        [Tooltip("체크 시 에디터 환경에서 아래 설정된 가짜 데이터를 세션에 강제 주입함.")]
        public bool useEditorTestData = false;
        
        public string testNameA = "PlayerA";
        public string testNameB = "PlayerB";
        
        public ColorData testColorA = ColorData.Cyan;
        public ColorData testColorB = ColorData.Pink;
        
        [Tooltip("카트리지 종류 (A, B, C, D)")]
        public string testCartridge = "A";
        
        [Tooltip("관계 유형 (1 ~ 6)")]
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

        public int PieceA1 { get; set; }
        public int PieceA2 { get; set; }
        public int PieceA3 { get; set; }
        public int PieceB1 { get; set; }
        public int PieceB2 { get; set; }
        public int PieceB3 { get; set; }
        public int PieceC1 { get; set; }
        public int PieceC2 { get; set; }
        public int PieceC3 { get; set; }
        public int PieceD1 { get; set; }
        public int PieceD2 { get; set; }
        public int PieceD3 { get; set; }
        
        public int TotalPieces => PieceA2 + PieceA3 + 
                                  PieceB1 + PieceB2 + PieceB3 + 
                                  PieceC1 + PieceC2 + PieceC3 + 
                                  PieceD1 + PieceD2 + PieceD3;

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                ApplyEditorTestData();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 에디터 모드 전용 가짜 데이터를 세션에 등록함.
        /// Why: API 서버 연동 없이도 UI 및 씬 흐름을 즉각적으로 테스트할 수 있도록 지원함.
        /// </summary>
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

                // 예시 입력값에 따른 결과값: testCartridge="B", testRelation=2 일 때 -> UserType.B2
                string combinedTypeStr = $"{Cartridge}{testRelation}";
                if (Enum.TryParse(combinedTypeStr, out UserType parsedType))
                {
                    CurrentUserType = parsedType;
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[SessionManager] 잘못된 테스트 카트리지/관계 조합({combinedTypeStr}). A1으로 폴백합니다.");
                    CurrentUserType = UserType.A1;
                }

                UnityEngine.Debug.Log($"<color=cyan>[SessionManager] 에디터 테스트 데이터 세팅 완료: 타입({CurrentUserType}), 카트리지({Cartridge}), 관계({testRelation}), A({testNameA}:{testColorA}), B({testNameB}:{testColorB})</color>");
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

            PieceA1 = 0; PieceA2 = 0; PieceA3 = 0;
            PieceB1 = 0; PieceB2 = 0; PieceB3 = 0;
            PieceC1 = 0; PieceC2 = 0; PieceC3 = 0;
            PieceD1 = 0; PieceD2 = 0; PieceD3 = 0;

            // Why: 타이틀로 돌아가서 리셋되더라도 테스트 환경 유지를 위해 더미 데이터를 다시 주입함.
#if UNITY_EDITOR
            if (useEditorTestData)
            {
                ApplyEditorTestData();
            }
#endif
        }
    }
}