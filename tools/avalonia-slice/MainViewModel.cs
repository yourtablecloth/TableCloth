using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaSlice;

/// <summary>
/// CommunityToolkit.Mvvm 소스 생성(ObservableProperty/RelayCommand)이 Avalonia 컴파일 바인딩 +
/// Native AOT에서 정상 동작하는지 검증하는 슬라이스 ViewModel. (실 앱의 VM은 동일 패턴을 사용한다.)
/// </summary>
public partial class MainViewModel : ObservableObject
{
    public string Title => "TableCloth Avalonia + Native AOT 수직 슬라이스 검증";

    [ObservableProperty]
    private string _message = "검증 항목: FluentTheme · 컴파일 바인딩(x:DataType) · CommunityToolkit.Mvvm 소스젠 · 한글 IME · 런타임 테마 스왑";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputEcho))]
    private string _inputText = string.Empty;

    public string InputEcho => string.IsNullOrEmpty(InputText)
        ? "(입력한 내용이 여기에 에코됩니다 — 두 방향 바인딩 확인)"
        : $"입력 에코: {InputText}";

    public ObservableCollection<string> Items { get; } = new()
    {
        "항목 1 — 컴파일 바인딩",
        "항목 2 — ItemsControl",
        "항목 3 — 한글 렌더링",
    };

    [ObservableProperty]
    private string _themeToggleLabel = "다크 모드로 전환";

    [RelayCommand]
    private void ToggleTheme()
    {
        var app = Application.Current;
        if (app is null)
            return;

        if (app.RequestedThemeVariant == ThemeVariant.Dark)
        {
            app.RequestedThemeVariant = ThemeVariant.Light;
            ThemeToggleLabel = "다크 모드로 전환";
        }
        else
        {
            app.RequestedThemeVariant = ThemeVariant.Dark;
            ThemeToggleLabel = "라이트 모드로 전환";
        }
    }
}
