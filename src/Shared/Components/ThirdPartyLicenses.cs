#nullable enable

using System.Collections.Generic;

namespace TableCloth.Components
{
    /// <summary>
    /// 이슈 #296(AOT): About 창 OSS 크레딧 항목. 과거에는 <c>Assembly.GetReferencedAssemblies()</c> +
    /// <c>Assembly.Load</c> + 어셈블리 커스텀 어트리뷰트를 런타임 리플렉션으로 읽어 생성했으나, Native AOT 는
    /// 이를 지원하지 않아 <see cref="System.PlatformNotSupportedException"/> 을 던진다. 큐레이트된 정적 목록으로 대체.
    /// </summary>
    public sealed class ThirdPartyComponent
    {
        public ThirdPartyComponent(string title, string copyright, string? repositoryUrl = null)
        {
            Title = title;
            Copyright = copyright;
            RepositoryUrl = repositoryUrl;
        }

        public string Title { get; }
        public string Copyright { get; }
        public string? RepositoryUrl { get; }
    }

    /// <summary>
    /// 두 앱(TableCloth/Spork)의 About 창이 공유하는 OSS 크레딧 목록. src/Shared 공유 링크(단일 출처).
    /// 의존성을 추가/제거할 때 이 목록을 함께 갱신한다.
    /// </summary>
    public static class ThirdPartyLicenses
    {
        public static readonly IReadOnlyList<ThirdPartyComponent> Components = new ThirdPartyComponent[]
        {
            new("Avalonia", "(c) AvaloniaUI OÜ and contributors", "https://github.com/AvaloniaUI/Avalonia"),
            new("CommunityToolkit.Mvvm", "(c) .NET Foundation and contributors", "https://github.com/CommunityToolkit/dotnet"),
            new("Serilog", "(c) Serilog contributors", "https://github.com/serilog/serilog"),
            new("Sentry SDK for .NET", "(c) Functional Software, Inc. dba Sentry", "https://github.com/getsentry/sentry-dotnet"),
            new("System.CommandLine", "(c) .NET Foundation and contributors", "https://github.com/dotnet/command-line-api"),
            new("AsyncAwaitBestPractices", "(c) Brandon Minnick", "https://github.com/brminnick/AsyncAwaitBestPractices"),
            new("Velopack", "(c) Velopack Ltd and contributors", "https://github.com/velopack/velopack"),
            new("PnPeople.Security", "(c) PnPeople Co., Ltd.", null),
        };
    }
}
