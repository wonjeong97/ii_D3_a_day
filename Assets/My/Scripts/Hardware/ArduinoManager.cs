using System;
using System.Collections.Generic;
using System.IO.Ports;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace My.Scripts.Hardware
{
    /// <summary>
    /// 단일 아두이노 장치와의 시리얼 통신 연결 및 조명 제어를 백그라운드에서 관리함.
    /// 수신 스레드 없이 병렬 스캔과 송신 로직만 구성하여 메모리 할당 및 프레임 저하를 원천 차단함.
    /// </summary>
    public class ArduinoManager : MonoBehaviour
    {
        public static ArduinoManager Instance;

        private SerialPort _arduinoPort;
        private volatile bool _isRunning;
        private readonly object _connectionLock = new object();

        /// <summary>
        /// 시리얼 포트 객체가 존재하고 현재 개방된 상태인지 확인.
        /// </summary>
        public bool IsConnected => _arduinoPort != null && _arduinoPort.IsOpen;

        /// <summary>
        /// 싱글톤 인스턴스를 초기화하고 씬 전환 시 파괴되지 않도록 설정함.
        /// </summary>
        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 객체 생성 시 백그라운드 포트 스캔 및 자동 연결 프로세스를 시작함.
        /// </summary>
        private void Start()
        {
            _isRunning = true;
            AutoConnectAsync().Forget();
        }

        /// <summary>
        /// 가용한 모든 COM 포트를 스캔하여 아두이노 장치 연결을 시도함.
        /// 여러 포트를 하나씩 열지 않고 병렬 처리(WhenAll)하여 초기 연결 대기 시간을 1~2초 내외로 단축함.
        /// </summary>
        private async UniTask AutoConnectAsync()
        {
            string[] portNames = SerialPort.GetPortNames();
            Debug.Log($"[ArduinoManager] 발견된 COM 포트 수: {portNames.Length}");

            List<UniTask> tasks = new List<UniTask>();

            foreach (string portName in portNames)
            {
                tasks.Add(TryConnectPortAsync(portName));
            }

            await UniTask.WhenAll(tasks);

            if (!IsConnected)
            {
                Debug.LogWarning("[ArduinoManager] 아두이노를 찾지 못했습니다. 케이블 연결을 확인해 주세요.");
            }
        }

        /// <summary>
        /// 단일 포트를 개방하고 초기 응답 문자열을 분석하여 아두이노를 식별함.
        /// </summary>
        /// <param name="portName">테스트할 COM 포트 이름.</param>
        private async UniTask TryConnectPortAsync(string portName)
        {
            await UniTask.RunOnThreadPool(async () =>
            {
                // 이미 다른 비동기 루틴에서 연결에 성공했다면 즉시 종료
                if (IsConnected) return; 

                SerialPort tempPort = new SerialPort(portName, 9600);
                tempPort.ReadTimeout = 2000;
                tempPort.DtrEnable = true;

                try
                {
                    tempPort.Open();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ArduinoManager] 포트 열기 실패 ({portName}): {e.Message}");
                    tempPort.Dispose();
                    return;
                }

                // 아두이노 리셋 후 부트로더 진입 및 "Arduino" 문자열 전송 대기
                await UniTask.Delay(TimeSpan.FromSeconds(1.5f));

                string response = string.Empty;
                float maxWaitTime = 5.0f;
                float elapsedTime = 0f;

                while (elapsedTime < maxWaitTime && _isRunning)
                {
                    if (IsConnected) 
                    {
                        tempPort.Close();
                        tempPort.Dispose();
                        return;
                    }

                    try
                    {
                        if (tempPort.BytesToRead > 0)
                        {
                            response += tempPort.ReadExisting();
                            if (response.Contains("Arduino"))
                            {
                                break;
                            }
                        }
                    }
                    catch (TimeoutException) { }
                    catch (Exception) { }

                    await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                    elapsedTime += 0.5f;
                }

                // 메인 스레드로 돌아와서 연결 정보를 갱신함
                await UniTask.SwitchToMainThread();

                // 경쟁 상태(Race Condition)를 방지하여 단일 태스크만 포트를 할당받도록 보장함.
                lock (_connectionLock)
                {
                    if (response.Contains("Arduino") && !IsConnected)
                    {
                        // 예기치 않은 좀비 핸들 방지를 위해 덮어쓰기 전 기존 포트가 있다면 명시적 닫기 수행
                        if (_arduinoPort != null)
                        {
                            try { _arduinoPort.Close(); _arduinoPort.Dispose(); } catch { }
                        }

                        tempPort.ReadTimeout = 10;
                        _arduinoPort = tempPort;
                        Debug.Log($"<color=green>[ArduinoManager] 조명 아두이노 연결 완료: {portName}</color>");
                        
                        // 연결 성공 직후 조명 초기 상태를 강제 보장하기 위함.
                        SendCommandToLight("LEDOff");
                    }
                    else
                    {
                        // 이미 다른 스레드에서 연결을 선점했거나 실패한 경우 자원을 안전하게 해제함
                        try { tempPort.Close(); } catch { }
                        tempPort.Dispose();
                    }
                }
            });
        }

        /// <summary>
        /// 조명 하드웨어로 제어 명령 문자열을 전송함.
        /// </summary>
        /// <param name="command">전송할 명령어 (예: LightOn / LightOff).</param>
        public bool SendCommandToLight(string command)
        {
            if (IsConnected)
            {
                try 
                { 
                    _arduinoPort.WriteLine(command); 
                    return true; 
                }
                catch (Exception e) 
                { 
                    Debug.LogError($"[ArduinoManager] 제어 명령 전송 실패: {e.Message}"); 
                    return false; 
                }
            }
            return false;
        }
        
        /// <summary>
        /// 통신 장애 복구 및 타이틀 씬 진입 시 호출되어 아두이노 재연결을 시도함.
        /// 이미 연결되어 있다면 안전을 위해 소등 명령만 전송하여 하드웨어를 초기 상태로 보장하기 위함.
        /// </summary>
        public async UniTask ReconnectAsync()
        {
            if (IsConnected)
            {
                SendCommandToLight("LEDOff");
                return;
            }

            Debug.Log("<color=blue>[ArduinoManager] 조명 아두이노 재연결 시도...</color>");
            
            if (_arduinoPort != null)
            {
                try 
                { 
                    _arduinoPort.Close(); 
                    _arduinoPort.Dispose(); 
                } 
                catch { }
                
                _arduinoPort = null;
            }

            await AutoConnectAsync();
            
            // 재연결 프로세스가 끝난 후 다시 한번 상태를 보장함.
            if (IsConnected)
            {
                SendCommandToLight("LEDOff");
            }
        }

        /// <summary>
        /// 매니저 파괴 시 열린 포트를 명시적으로 닫음.
        /// OS 단에서 COM 포트가 잠기는 좀비 포트 버그를 방지하기 위함.
        /// </summary>
        private void OnDestroy()
        {
            _isRunning = false;

            if (_arduinoPort != null)
            {
                try
                {
                    if (_arduinoPort.IsOpen)
                    {
                        // 종료 전 만약을 대비해 조명을 확실히 끔
                        _arduinoPort.WriteLine("LEDOff"); 
                        _arduinoPort.Close();
                    }
                    _arduinoPort.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ArduinoManager] 포트 닫기 에러: {e.Message}");
                }
            }
        }
    }
}