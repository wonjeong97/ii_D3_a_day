using My.Scripts._06_PlayVideo;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;

/// <summary>
/// 애니메이션 타임라인의 이벤트를 수신하여 영상 재생 상태를 제어하는 브릿지 컴포넌트.
/// 애니메이터와 PlayVideoManager 사이의 명령 전달을 수행하기 위함.
/// </summary>
public class FilmAnimEvent : MonoBehaviour
{   
    [SerializeField] private Text filmText;
    
    [Header("Film Audio Settings")]
    [Tooltip("레고_7 효과음을 재생할 전용 AudioSource")]
    [SerializeField] private AudioSource filmAudioSource;
    [Tooltip("페이드아웃에 걸리는 시간")]
    [SerializeField] private float fadeOutDuration = 1.0f;
    
    private Coroutine _fadeCoroutine;

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
    /// 애니메이션 타임라인 이벤트에서 호출되어 PlayVideoManager의 텍스트 캔버스 페이드 인 기능을 실행함.
    /// 애니메이터와 UI 연출 로직의 결합도를 낮추고 매니저를 통해 중앙 제어하기 위함.
    /// </summary>
    /// <param name="duration">페이드 인에 소요되는 시간.</param>
    public void FadeInFilmTextCanvas(float duration)
    {
        if (PlayVideoManager.Instance)
        {
            PlayVideoManager.Instance.FadeInFilmTextCanvas(duration);
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
    
    /// <summary>
    /// 카운트다운 효과음을 재생함.
    /// </summary>
    public void PlayCountSfx()
    {
        if (SoundManager.Instance)
        {
            SoundManager.Instance.PlaySFX("공통_10_3초");
        }
    }

    /// <summary>
    /// 필름 롤링 효과음을 루프 모드로 재생함.
    /// </summary>
    public void PlayFilmSfx()
    {
        if (filmAudioSource)
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
            
            filmAudioSource.loop = true;
            filmAudioSource.volume = 0.5f;
            
            if (!filmAudioSource.isPlaying)
            {
                filmAudioSource.Play();
            }
        }
    }

    /// <summary>
    /// 재생 중인 필름 효과음을 설정된 시간에 걸쳐 자연스럽게 페이드아웃하고 정지함.
    /// </summary>
    public void FadeOutFilmSfx()
    {
        if (filmAudioSource && filmAudioSource.isPlaying)
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }
            _fadeCoroutine = StartCoroutine(FadeOutRoutine());
        }
    }

    private System.Collections.IEnumerator FadeOutRoutine()
    {
        float startVolume = filmAudioSource.volume;
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            filmAudioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeOutDuration);
            yield return null;
        }

        filmAudioSource.volume = 0f;
        filmAudioSource.Stop();
    }

    /// <summary>
    /// 객체 파괴 시 실행 중인 코루틴과 오디오를 안전하게 정지하여 에러 및 메모리 누수를 방지함.
    /// </summary>
    private void OnDestroy()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        if (filmAudioSource && filmAudioSource.isPlaying)
        {
            filmAudioSource.Stop();
        }
    }
}