using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TableCloth.ViewModels;

/// <summary>
/// 호환성 탭의 개별 토글 설정을 검색·필터링 가능한 형태로 표현하는 항목이다.
/// 실제 값은 <see cref="OptionsWindowViewModel"/>의 대응 <c>[ObservableProperty]</c> bool을 그대로
/// 읽고 쓰는 얇은 어댑터이므로, 기존 저장 파이프라인(속성 변경 → preferences 저장)은 변경 없이 재사용된다.
/// 호스트 보안 프로그램 대응 등으로 호환성 설정이 계속 늘어나도, 뷰모델에 항목 하나만 등록하면
/// 검색·스크롤 UI에 자동으로 편입된다.
/// </summary>
public sealed class CompatibilityOptionItem : ObservableObject
{
    private readonly Func<bool> _getter;
    private readonly Action<bool> _setter;

    /// <param name="owner">대응 속성을 보유한 옵션 창 뷰모델.</param>
    /// <param name="watchedPropertyName">
    /// 뷰모델에서 이 항목이 반영하는 속성 이름. 해당 속성이 바뀌면(예: 초기 로드) 체크 상태를 화면에 다시 반영한다.
    /// </param>
    /// <param name="title">체크박스에 표시되는 현재 UI 언어의 설정 이름.</param>
    /// <param name="description">체크박스 아래에 표시되는 현재 UI 언어의 설명.</param>
    /// <param name="searchText">검색 매칭용 텍스트(이름·설명의 한국어와 영어를 모두 합친 것).</param>
    /// <param name="getter">대응하는 뷰모델 속성 값을 읽는 대리자.</param>
    /// <param name="setter">대응하는 뷰모델 속성 값을 쓰는 대리자(기존 저장 파이프라인을 트리거).</param>
    public CompatibilityOptionItem(
        OptionsWindowViewModel owner,
        string watchedPropertyName,
        string title,
        string description,
        string searchText,
        Func<bool> getter,
        Action<bool> setter)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Title = title;
        Description = description;
        SearchText = searchText;
        _getter = getter;
        _setter = setter;

        // 뷰모델의 대응 속성이 바뀔 때(초기 로드로 값이 채워질 때 포함) 체크박스가 최신 값을 보이도록 알린다.
        owner.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == watchedPropertyName)
                OnPropertyChanged(nameof(IsChecked));
        };
    }

    /// <summary>체크박스 옆에 표시되는 설정 이름(현재 UI 언어).</summary>
    public string Title { get; }

    /// <summary>체크박스 아래에 표시되는 설정 설명(현재 UI 언어).</summary>
    public string Description { get; }

    /// <summary>검색 매칭에만 쓰이는, 이름·설명의 한국어와 영어를 합친 텍스트.</summary>
    public string SearchText { get; }

    /// <summary>대응 뷰모델 속성을 그대로 읽고 쓰는 체크 상태.</summary>
    public bool IsChecked
    {
        get => _getter();
        set
        {
            if (_getter() == value)
                return;
            _setter(value);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 검색어가 비어 있으면 항상 표시하고, 아니면 이름·설명(한/영)에 부분 일치하는지 검사한다.
    /// 대소문자를 구분하지 않는다.
    /// </summary>
    public bool MatchesFilter(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;
        return SearchText.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
