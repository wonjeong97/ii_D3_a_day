using System;
using Wonjeong.Data;

namespace My.Scripts.Data
{
    [Serializable]
    public class CommonOutroData
    {
        public TextSetting textOutro; 
        public TextSetting textOutro2;
    }
    
    [Serializable]
    public class CommonBackgroundData
    {
        public TextSetting nameA; 
        public TextSetting nameB; 
    }

    [Serializable]
    public class CommonIntroData
    {
        public TextSetting nameA; 
        public TextSetting nameB; 
    }

    [Serializable]
    public class QuestionSetting
    {   
        public string imageKey;
        public TextSetting textQuestion;
        public TextSetting textAnswer1;
        public TextSetting textAnswer2;
        public TextSetting textAnswer3;
        public TextSetting textAnswer4;
        public TextSetting textAnswer5;
    }

    [Serializable]
    public class CommonQuestionPageData
    {
        public QuestionSetting questionSetting; 
        public TextSetting textSelected;
        public TextSetting textDescription;
        public TextSetting textWait;
    }

    [Serializable]
    public class CommonResultPageData
    {
        public TextSetting textAnswerComplete;
        public TextSetting textMyScene;
        public TextSetting textPhotoSaved; 
    }

    [Serializable]
    public class QuestionResultSet
    {
        public CommonQuestionPageData questionData;
        public CommonResultPageData resultData;
    }
    
    /// <summary>
    /// 질문 페이지 공통 UI 데이터 모델.
    /// Why: 모든 질문에서 반복되는 안내 텍스트를 한 번만 로드하여 재사용하기 위함.
    /// </summary>
    [Serializable]
    public class CommonQuestionUI
    {
        public TextSetting textSelected;
        public TextSetting textDescription;
        public TextSetting textWait;
    }

    /// <summary>
    /// 결과(카메라) 페이지 공통 UI 데이터 모델.
    /// </summary>
    [Serializable]
    public class CommonResultUI
    {
        public TextSetting textAnswerComplete;
        public TextSetting textPhotoSaved;
    }

    /// <summary>
    /// 배열 내부에서 변경되는 고유 데이터만 담는 모델.
    /// </summary>
    [Serializable]
    public class QuestionSetItem
    {
        public QuestionSetting questionSetting; 
        public TextSetting textDescription;
        public TextSetting textMyScene; 
    }
    
    /// <summary>
    /// 로딩 페이지용 데이터 모델.
    /// Why: 제이슨으로부터 2개의 텍스트 서식 정보를 받아오기 위함.
    /// </summary>
    [Serializable]
    public class CommonLoadingData
    {
        public TextSetting text1;
        public TextSetting text2;
    }
}