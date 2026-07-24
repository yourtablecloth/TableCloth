using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaSlice;

/// <summary>
/// 실 <c>TableCloth.ViewModels.AboutWindowViewModel</c>의 바인딩 표면(속성/커맨드 이름)을 그대로 미러링한
/// 슬라이스 VM. 실 VM은 WPF-free임이 확인됐으므로(M1), M3에서는 이 axaml에 실 VM을 바로 물릴 수 있다.
/// 여기서는 서비스 대신 샘플 데이터를 채워 레이아웃/바인딩/템플릿을 검증한다.
/// </summary>
public partial class AboutViewModel : ObservableObject
{
    public AboutViewModel()
    {
        // 실 VM에서는 OnAboutWindowLoaded()가 서비스로 채운다. 슬라이스는 샘플로 대체.
        Sponsors = new List<SponsorVm>
        {
            new("정현", "https://example.invalid/a.png"),
            new("식탁보 후원자", "https://example.invalid/b.png"),
            new("Contributor Kim", "https://example.invalid/c.png"),
        };
        HasSponsors = true;
        AnonymousSponsorsText = "외 3명";
        HasAnonymousSponsors = true;

        Contributors = new List<ContributorVm>
        {
            new("rkttu", "https://example.invalid/d.png"),
            new("기여자 이", "https://example.invalid/e.png"),
        };
        HasContributors = true;

        LicenseDetails =
            "TableCloth은 다음 오픈소스에 기대고 있습니다(발췌):\n\n" +
            "• Avalonia (MIT)\n• CommunityToolkit.Mvvm (MIT)\n• Velopack (MIT)\n• Sentry (MIT)\n\n" +
            "이 영역은 기존 WPF RichTextBox(FlowDocument) 대신 Avalonia SelectableTextBlock로 렌더링됩니다. " +
            "긴 라이선스 전문이 스크롤되어 표시되는지, 한글이 올바르게 렌더링되는지 확인하세요.";
    }

    [ObservableProperty] private string _appVersion = "1.20.6.0 (Avalonia 슬라이스)";
    [ObservableProperty] private string _catalogDate = "2026-07-24 12:00:00";
    [ObservableProperty] private string _licenseDetails = string.Empty;

    [ObservableProperty] private IReadOnlyList<SponsorVm> _sponsors = new List<SponsorVm>();
    [ObservableProperty] private bool _hasSponsors;
    [ObservableProperty] private string _anonymousSponsorsText = string.Empty;
    [ObservableProperty] private bool _hasAnonymousSponsors;

    [ObservableProperty] private IReadOnlyList<ContributorVm> _contributors = new List<ContributorVm>();
    [ObservableProperty] private bool _hasContributors;
    [ObservableProperty] private string _anonymousContributorsText = string.Empty;
    [ObservableProperty] private bool _hasAnonymousContributors;

    [ObservableProperty] private string _statusMessage = "(버튼을 눌러 커맨드 바인딩을 확인하세요)";

    [RelayCommand] private void OpenWebsite() => StatusMessage = "OpenWebsite 커맨드 실행됨";
    [RelayCommand] private void OpenUserManual() => StatusMessage = "OpenUserManual 커맨드 실행됨";
    [RelayCommand] private void ShowSystemInfo() => StatusMessage = "ShowSystemInfo 커맨드 실행됨";
    [RelayCommand] private void CheckUpdatedVersion() => StatusMessage = "CheckUpdatedVersion 커맨드 실행됨";
    [RelayCommand] private void OpenPrivacyPolicy() => StatusMessage = "OpenPrivacyPolicy 커맨드 실행됨";
    [RelayCommand] private void OpenDiscord() => StatusMessage = "OpenDiscord 커맨드 실행됨";
    [RelayCommand] private void OpenSponsorPage() => StatusMessage = "OpenSponsorPage 커맨드 실행됨";
}

/// <summary>실 SponsorInfo의 바인딩 대상 멤버(Name/AvatarUrl)만 미러링한 슬라이스 항목 타입.</summary>
public sealed record SponsorVm(string Name, string AvatarUrl);

/// <summary>실 ContributorInfo의 바인딩 대상 멤버(Name/AvatarUrl)만 미러링한 슬라이스 항목 타입.</summary>
public sealed record ContributorVm(string Name, string AvatarUrl);
