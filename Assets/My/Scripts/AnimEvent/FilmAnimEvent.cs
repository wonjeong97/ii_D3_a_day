using My.Scripts._06_PlayVideo;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 애니메이션 타임라인의 이벤트를 수신하여 영상 재생 상태를 제어하는 브릿지 컴포넌트.
/// 애니메이터와 PlayVideoManager 사이의 명령 전달을 수행하기 위함.
/// </summary>
public class FilmAnimEvent : MonoBehaviour
{   
    [SerializeField] private Text filmText;

    /// <summary>
    /// 애니메이션 특정 시점에 화면에 표시될 텍스트 내용을 변경함.
    /// 연출 흐름에 맞는 자막이나 가이드를 동적으로 출력하기 위함.
    /// </summary>
    /// <param name="text">UI에 표시할 문자열.</param>
    public void SetFilmText(string text)
    {
        if (!filmText)
        {
            Debug.LogWarning("[FilmAnimEvent] filmText 컴포넌트가 할당되지 않았습니다.");
            return;
        }

        filmText.text = text;
    }

    /// <summary>
    /// 애니메이션 시작 지점에서 사진 교체 코루틴을 실행하도록 매니저에 알림.
    /// 시각적 연출과 데이터 로직의 실행 시점을 완벽히 동기화하기 위함.
    /// </summary>
    public void StartVideoAnimation()
    {
        if (PlayVideoManager.Instance)
        {
            PlayVideoManager.Instance.StartVideoAnimation();
        }
    }

    /// <summary>
    /// 애니메이션 타임라인 종료 시점에 호출되어 엔딩 씬 전환 프로세스를 시작함.
    /// 연출이 완전히 끝난 뒤 다음 단계로 안전하게 넘어가기 위함.
    /// </summary>
    public void EndPlayVideoScene()
    {
        if (PlayVideoManager.Instance)
        {
            PlayVideoManager.Instance.MoveToEndingScene();
        }
    }
}