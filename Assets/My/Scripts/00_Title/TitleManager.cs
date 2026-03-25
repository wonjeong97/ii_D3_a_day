using System.Collections;
using My.Scripts.Global;
using My.Scripts.Network; 
using UnityEngine;
using UnityEngine.SceneManagement;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._00_Title
{
    /// <summary>
    /// 타이틀 화면 입력 처리 및 씬 전환 매니저.
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        private bool _isTransitioning = false; 
        private float _fadeTime = 1.0f; 

        // Why: 클라이언트가 타이틀에 올 때까지 기다리기 위한 플래그 및 코루틴
        private bool _isWaitingForClient = false;
        private Coroutine _requestCoroutine;

        private void Start()
        {
            LoadSettings();

            if (TcpManager.Instance)
            {
                TcpManager.Instance.onMessageReceived += OnNetworkMessageReceived;
            }
            else
            {
                Debug.LogWarning("[TitleManager] TcpManager 인스턴스가 존재하지 않습니다.");
            }
        }

        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);

            if (settings != null)
            {
                if (SoundManager.Instance)
                {
                    SoundManager.Instance.PlayBGM("MainBGM");
                }
                else
                {
                    Debug.LogWarning("[TitleManager] SoundManager 인스턴스가 존재하지 않습니다.");
                }
                
                _fadeTime = settings.fadeTime; 
            }
            else
            {
                Debug.LogWarning("[TitleManager] Settings.json 로드 실패.");
            }
        }

        private void Update()
        {
            if (_isTransitioning) return; 

            // Why: 씬 전환 권한을 서버가 전담하며, 엔터 입력 시 클라이언트 상태부터 체크함
            if (TcpManager.Instance && TcpManager.Instance.IsServer)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    if (!_isWaitingForClient)
                    {
                        _isWaitingForClient = true;
                        Debug.Log("[TitleManager] 클라이언트의 Title 씬 진입을 대기합니다...");
                        _requestCoroutine = StartCoroutine(RequestStartRoutine());
                    }
                }
            }
        }

        /// <summary>
        /// 클라이언트가 Title 씬에 도착해 응답할 때까지 1초 주기로 계속 넘어갈 준비가 되었는지 물어봅니다.
        /// </summary>
        private IEnumerator RequestStartRoutine()
        {
            while (_isWaitingForClient)
            {
                if (TcpManager.Instance)
                {
                    TcpManager.Instance.SendMessageToTarget("REQUEST_START", "");
                }
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }
        }

        /// <summary>
        /// 동기화가 완료되면 최종적으로 씬을 넘기는 처리를 합니다.
        /// </summary>
        private void ProcessTag(int playerID)
        {
            if (_isTransitioning) return;
            _isTransitioning = true; 

            if (TcpManager.Instance && TcpManager.Instance.IsServer)
            {
                TcpManager.Instance.SendMessageToTarget("CHANGE_SCENE", GameConstants.Scene.Tutorial);
            }

            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.Tutorial, true);
            }
            else
            {
                Debug.LogWarning("[TitleManager] GameManager 인스턴스가 존재하지 않습니다.");
                SceneManager.LoadScene(GameConstants.Scene.Tutorial);
            }
        }

        /// <summary>
        /// 수신된 TCP 메시지를 파싱하여 씬 전환을 완벽하게 동기화합니다.
        /// </summary>
        private void OnNetworkMessageReceived(TcpMessage msg)
        {
            if (msg == null) return;

            // 1. [클라이언트] 서버로부터 튜토리얼로 넘어갈 거냐는 물음을 받으면, 나도 타이틀에 있다고 대답함
            if (msg.command == "REQUEST_START")
            {
                if (TcpManager.Instance && !TcpManager.Instance.IsServer)
                {
                    TcpManager.Instance.SendMessageToTarget("START_ACK", "");
                }
            }
            // 2. [서버] 클라이언트가 타이틀에 도착해 대답을 주면, 물어보기를 멈추고 씬을 넘김
            else if (msg.command == "START_ACK")
            {
                if (TcpManager.Instance && TcpManager.Instance.IsServer)
                {
                    if (_isWaitingForClient && !_isTransitioning)
                    {
                        _isWaitingForClient = false;
                        if (_requestCoroutine != null) StopCoroutine(_requestCoroutine);
                        
                        Debug.Log("[TitleManager] 클라이언트 준비 완료. Tutorial 씬으로 동시 이동합니다.");
                        ProcessTag(1); 
                    }
                }
            }
            // 3. [클라이언트] 서버의 최종 명령(CHANGE_SCENE)을 받고 씬을 넘김
            else if (msg.command == "CHANGE_SCENE")
            {
                if (!_isTransitioning)
                {
                    _isTransitioning = true;
                    
                    if (GameManager.Instance)
                    {
                        GameManager.Instance.ChangeScene(msg.payload, true);
                    }
                    else
                    {
                        Debug.LogWarning("[TitleManager] GameManager 인스턴스가 존재하지 않습니다.");
                        SceneManager.LoadScene(msg.payload);
                    }
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