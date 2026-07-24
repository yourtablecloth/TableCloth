using Avalonia;
using Lemon.Hosting.AvaloniauiDesktop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
using Spork.App.DependencyInjection;
using Spork.Sandbox;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using TableCloth.App.DependencyInjection;
using TableCloth.Bootstrap.Dialogs;
using TableCloth.Models.Configuration;
using TableCloth.Resources;
using TableCloth.Serialization;
using Velopack;

namespace TableCloth;

internal static class Program
{
    private const string SporkVerb = "spork";

    // [미리 보기] 유휴 자동 종료(이슈 #197) 전용 헤드리스 프로세스. StartupScript가 Spork 런처와 별개로
    // 기동하여, 창을 닫아도 유휴 보호가 유지되게 한다.
    private const string IdleGuardVerb = "idle-guard";

    [STAThread]
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            MessageBox.Show(
                e.ExceptionObject?.ToString() ?? "Unknown Error",
                "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        args ??= Helpers.GetCommandLineArguments();

        // verb 디스패치: 첫 토큰이 `spork`이면 Spork 모듈로 라우팅, 그렇지 않으면 TableCloth 호스트 모드.
        // System.CommandLine으로 감싸지 않고 단순 분기를 사용한 이유는, 두 모듈이 각자 자체
        // CommandLineArguments(System.CommandLine RootCommand)로 옵션을 파싱하기 때문. 디스패처는
        // 단지 verb 토큰을 소비하고 남은 인수를 Helpers.SetEffectiveCommandLineArguments로 노출한다.
        if (args.Length > 0 && string.Equals(args[0], SporkVerb, StringComparison.OrdinalIgnoreCase))
            return RunSpork(args.Skip(1).ToArray());

        if (args.Length > 0 && string.Equals(args[0], IdleGuardVerb, StringComparison.OrdinalIgnoreCase))
            return RunIdleGuard(args.Skip(1).ToArray());

        return RunTableCloth(args);
    }

    // [미리 보기] 유휴 자동 종료 가드(이슈 #197, #296). 메인 창 없는 최소 Avalonia 앱을 띄워 유휴 모니터만 돌린다.
    // 헤드리스 헬퍼이므로 실패해도 대화상자를 띄우지 않고 조용히 종료한다.
    private static int RunIdleGuard(string[] args)
    {
        try
        {
            Spork.SporkAvaloniaApp.Configure(AppBuilder.Configure<Spork.IdleGuardApplication>())
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[idle-guard] fatal: {ex}");
        }

        return Environment.ExitCode;
    }

    private static int RunTableCloth(string[] args)
    {
        // Velopack 초기화 - 설치/업데이트/제거 시 처리
        VelopackApp.Build().Run();

        // 이슈 #296: 라이선스 동의 게이트는 Avalonia 창을 써야 하므로 App 라이프사이클(LicenseGate)로 이관.
        // 설치 후 첫 실행 시 파일 연결 등록(UI 아님)은 여기서 유지.
        RegisterFileAssociationsIfNeeded();

        try
        {
            Helpers.SetEffectiveCommandLineArguments(args);

            var builder = Host.CreateApplicationBuilder(args);
            builder.UseTableCloth();

            // 이슈 #296: WPF Application.Run → Avalonia(Lemon.Hosting). App(TableClothApplication)은 DI 로 생성.
#pragma warning disable CS0618 // Lemon.Hosting 1.1.1 obsolete API — M2c 검증 경로(신 API 이관은 후속).
            builder.Services.AddAvaloniauiDesktopApplication<TableClothApplication>(TableClothAvaloniaApp.Configure);
            using var appHost = builder.Build();
            appHost.RunAvaloniauiApplication(args);
#pragma warning restore CS0618
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex?.ToString() ?? "Unknown Error",
                "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return Environment.ExitCode;
    }

    private static int RunSpork(string[] args)
    {
        try
        {
            // 모듈의 CommandLineArguments는 Helpers.GetCommandLineArguments()를 호출해 인수를 읽는다.
            // verb 토큰('spork')이 소비된 뒤의 인수만 모듈에 노출되도록 명시.
            Helpers.SetEffectiveCommandLineArguments(args);

            var builder = Host.CreateApplicationBuilder(args);
            builder.UseSpork();
            // UseSpork()가 등록한 ISandboxBootstrap의 noop 기본 구현을 실제 sandbox 구현으로 교체.
            // 본 호출은 TableCloth.exe(통합 진입점)에서만 일어나며, 단독 Spork.exe는 Spork.Sandbox를
            // 참조하지 않으므로 noop 그대로 사용된다.
            builder.UseSandboxBootstrap();

            // 이슈 #296: WPF Application.Run → Avalonia(Lemon.Hosting). App(SporkApplication)은 DI 로 생성.
#pragma warning disable CS0618 // Lemon.Hosting 1.1.1 obsolete API — M2c 검증 경로(신 API 이관은 후속).
            builder.Services.AddAvaloniauiDesktopApplication<Spork.SporkApplication>(Spork.SporkAvaloniaApp.Configure);
            using var appHost = builder.Build();
            appHost.RunAvaloniauiApplication(args);
#pragma warning restore CS0618
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex?.ToString() ?? "Unknown Error",
                "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return Environment.ExitCode;
    }

    private static void RegisterFileAssociationsIfNeeded()
    {
        try
        {
            // Velopack으로 설치된 경우에만 파일 연결 등록
            var updateManager = new UpdateManager(new Velopack.Sources.GithubSource(
                "https://github.com/yourtablecloth/TableCloth", null, false));

            if (!updateManager.IsInstalled)
                return;

            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                return;

            using var classesKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
            if (classesKey == null) return;

            // 이미 등록되어 있는지 확인
            using var existingKey = classesKey.OpenSubKey("TableCloth.tclnk");
            if (existingKey != null) return;

            // .tclnk 확장자 등록
            using (var extKey = classesKey.CreateSubKey(".tclnk"))
            {
                extKey?.SetValue("", "TableCloth.tclnk");
                using var progIdsKey = extKey?.CreateSubKey("OpenWithProgids");
                progIdsKey?.SetValue("TableCloth.tclnk", "");
            }

            // TableCloth.tclnk ProgId 등록
            using (var progIdKey = classesKey.CreateSubKey("TableCloth.tclnk"))
            {
                progIdKey?.SetValue("", "TableCloth Link File");

                using (var iconKey = progIdKey?.CreateSubKey("DefaultIcon"))
                {
                    iconKey?.SetValue("", $"\"{exePath}\",0");
                }

                using (var commandKey = progIdKey?.CreateSubKey(@"shell\open\command"))
                {
                    commandKey?.SetValue("", $"\"{exePath}\" \"@%1\"");
                }
            }
        }
        catch (Exception ex)
        {
            // 레지스트리 권한/잠금 등으로 등록에 실패하면 .tclnk 더블클릭이 동작하지
            // 않으나 앱 실행 자체에는 영향을 주지 않는다. 부트스트랩 단계라 DI
            // 로거가 아직 없으므로 Debug 출력에만 남긴다.
            Debug.WriteLine($"[TableCloth] RegisterFileAssociationsIfNeeded failed: {ex}");
        }
    }
}
