using My.Scripts._06_PlayVideo;
using UnityEngine;
using UnityEngine.UI;

public class FilmAnimEvent : MonoBehaviour
{   
    [SerializeField] private Text filmText;

    public void SetFilmText(string text)
    {
        if (!filmText)
        {
            Debug.LogWarning("[FilmAnimEvent] filmText 컴포넌트가 할당되지 않았습니다.");
            return;
        }

        filmText.text = text;
    }

    public void StartVideoAnimation()
    {
        if (PlayVideoManager.Instance)
        {
            PlayVideoManager.Instance.StartVideoAnimation();
        }
    }

    public void EndPlayVideoScene()
    {
        if (PlayVideoManager.Instance)
        {
            PlayVideoManager.Instance.MoveToEndingScene();
        }
    }
}