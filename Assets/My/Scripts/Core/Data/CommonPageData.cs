using System;
using Wonjeong.Data;

namespace My.Scripts.Core.Data
{
    /// <summary>
    /// 아웃트로 페이지에서 사용되는 텍스트 설정 데이터 모델.
    /// </summary>
    [Serializable]
    public class CommonOutroData
    {
        public TextSetting textOutro; 
        public TextSetting textOutro2;
    }
    
    /// <summary>
    /// 배경 UI에서 공통으로 사용되는 플레이어 이름 정보 모델.
    /// </summary>
    [Serializable]
    public class CommonBackgroundData
    {
        public TextSetting nameA; 
        public TextSetting nameB; 
    }

    /// <summary>
    /// 인트로 페이지의 텍스트 구성을 위한 데이터 모델.
    /// </summary>
    [Serializable]
    public class CommonIntroData
    {
        public TextSetting nameA; 
        public TextSetting nameB; 
    }

    /// <summary>
    /// 개별 질문과 5개 답변 항목의 텍스트 정보를 담는 모델.
    /// </summary>
    [Serializable]
    public class QuestionSetting
    {
        public TextSetting textQuestion;
        public TextSetting textAnswer1;
        public TextSetting textAnswer2;
        public TextSetting textAnswer3;
        public TextSetting textAnswer4;
        public TextSetting textAnswer5;
    }

    /// <summary>
    /// 질문 페이지 렌더링에 필요한 모든 텍스트 데이터를 결합한 모델.
    /// 질문 내용과 더불어 선택 상태, 안내 문구, 무응답 경고 등의 상태 메시지를 포함함.
    /// </summary>
    [Serializable]
    public class CommonQuestionPageData
    {
        public QuestionSetting questionSetting; 
        public TextSetting textSelected;
        public TextSetting textDescription;
        public TextSetting textWait;
        public TextSetting textPopupWarning; 
        public TextSetting textPopupTimeout; 
    }

    /// <summary>
    /// 결과(카메라 촬영) 페이지 렌더링에 필요한 텍스트 데이터 모델.
    /// </summary>
    [Serializable]
    public class CommonResultPageData
    {
        public TextSetting textAnswerComplete;
        public TextSetting textMyScene;
        public TextSetting textPhotoSaved; 
    }

    /// <summary>
    /// 질문과 결과 데이터를 하나의 세트로 관리하기 위한 구조체.
    /// </summary>
    [Serializable]
    public class QuestionResultSet
    {
        public CommonQuestionPageData questionData;
        public CommonResultPageData resultData;
    }
    
    /// <summary>
    /// 질문 페이지에서 전역적으로 공유되는 공통 UI 요소 모델.
    /// 모든 질문에서 반복되는 안내 및 경고 메시지를 일괄 관리하여 데이터 중복을 방지하기 위함.
    /// </summary>
    [Serializable]
    public class CommonQuestionUI
    {
        public TextSetting textSelected;
        public TextSetting textDescription;
        public TextSetting textWait;
        public TextSetting textPopupWarning;
        public TextSetting textPopupTimeout;
    }

    /// <summary>
    /// 결과(카메라) 페이지에서 전역적으로 공유되는 공통 UI 요소 모델.
    /// </summary>
    [Serializable]
    public class CommonResultUI
    {
        public TextSetting textAnswerComplete;
        public TextSetting textPhotoSaved;
    }

    /// <summary>
    /// 질문 세트 배열 내에서 각 항목마다 변경되는 고유 데이터 모델.
    /// 공통 UI 외에 해당 문항만의 특수한 설명이나 씬 텍스트를 오버라이드하기 위함.
    /// </summary>
    [Serializable]
    public class QuestionSetItem
    {
        public QuestionSetting questionSetting; 
        public TextSetting textDescription;
        public TextSetting textMyScene; 
    }
    
    /// <summary>
    /// 로딩 페이지 렌더링을 위한 데이터 모델.
    /// </summary>
    [Serializable]
    public class CommonLoadingData
    {
        public TextSetting text1;
        public TextSetting text2;
    }
}