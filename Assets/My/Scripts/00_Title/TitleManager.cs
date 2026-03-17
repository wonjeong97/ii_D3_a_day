using System;
using System.Collections;
using My.Scripts.Global;
using My.Scripts.Network; // TCP 매니저 네임스페이스 추가
using UnityEngine;
using UnityEngine.SceneManagement;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._00_Title
{
    /// <summary> 타이틀 화면 입력 처리 및 씬 전환 매니저 </summary>
    public class TitleManager : MonoBehaviour
    {
        private bool _isTransitioning = false; 
        private float _fadeTime = 1.0f; 

        private void Start()
        {
            LoadSettings();

            // Why: 클라이언트가 서버의 씬 전환 명령을 수신하기 위해 이벤트 구독함
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
            }
        }

        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);

            // 일반 C# 객체이므로 명시적 null 검사 적용
            if (settings != null)
            {
                if (SoundManager.Instance) SoundManager.Instance.PlayBGM("MainBGM");
                _fadeTime = settings.fadeTime; 
            }
            else
            {
                // Fallback 대신 에러 로그 출력
                Debug.LogWarning("[TitleManager] Settings.json 로드 실패.");
            }
        }

        private void Update()
        {
            if (_isTransitioning) return; 

            // Why: 향후 외부 API가 마스터 PC(서버)에만 연결되므로, 서버에서만 키보드 입력을 처리함
            // # TODO: 추후 외부 API 연동 시 입력 감지부를 API 콜백 함수로 교체할 것
            if (TcpManager.Instance && TcpManager.Instance.IsServer)
            {
                if (Input.GetKeyDown(KeyCode.Return))
                {
                    ProcessTag(1);
                }
            }
        }

        private void ProcessTag(int playerID)
        {
            if (_isTransitioning) return;
            _isTransitioning = true; 

            // 서버가 씬을 이동할 때 클라이언트도 함께 이동하도록 전환 신호를 전송함
            if (TcpManager.Instance && TcpManager.Instance.IsServer)
            {
                TcpManager.Instance.SendMessageToTarget("CHANGE_SCENE", GameConstants.Scene.Tutorial);
            }

            SceneManager.LoadScene(GameConstants.Scene.Tutorial);
        }

        /// <summary> 수신된 TCP 메시지를 파싱하여 씬 전환을 동기화함 </summary>
        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg != null && msg.command == "CHANGE_SCENE")
            {
                if (!_isTransitioning)
                {
                    _isTransitioning = true;
                    SceneManager.LoadScene(msg.payload); // 전달받은 씬 이름(Tutorial)으로 이동
                }
            }
        }

        private void OnDestroy()
        {   
            StopAllCoroutines();
            
            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived -= OnNetworkMessageReceived;
            }
        }
    }
}