using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class PowerSchemeGuideWindowViewModelForDesigner : PowerSchemeGuideWindowViewModel { }

/// <summary>
/// CPU 최대 성능(최대 프로세서 상태)이 제한되어 Windows Sandbox가 느려질 수 있음을 안내하고,
/// 사용자가 클래식 제어판(<c>powercfg.cpl</c>) 또는 모던 설정(전원 및 절전) 중 하나를 골라 열 수 있게 하는
/// 안내 창의 뷰모델. 전원 옵션을 여는 커맨드는 창을 닫지 않고 그대로 유지하며(사용자가 확인 후 직접 닫음),
/// '나중에'만 창을 닫는다. 여는 데 실패해도 무해하다(베스트에포트).
/// </summary>
public partial class PowerSchemeGuideWindowViewModel : ObservableObject
{
    protected PowerSchemeGuideWindowViewModel() { }

    [ActivatorUtilitiesConstructor]
    public PowerSchemeGuideWindowViewModel(
        TaskFactory taskFactory)
    {
        _taskFactory = taskFactory;
    }

    private readonly TaskFactory _taskFactory = default!;

    /// <summary>안내 창을 닫아 달라는 요청. 코드 비하인드가 구독해 <c>Close()</c>를 호출한다.</summary>
    public event EventHandler? CloseRequested;

    private async Task RequestCloseAsync()
        => await _taskFactory.StartNew(() => CloseRequested?.Invoke(this, EventArgs.Empty)).ConfigureAwait(false);

    /// <summary>클래식 제어판의 전원 옵션(powercfg.cpl)을 연다. 안내 창은 닫지 않고 그대로 둔다.</summary>
    [RelayCommand]
    private void OpenClassicPowerOptions()
        // control.exe 로 .cpl 을 여는 것이 셸 연결에 의존하지 않아 안정적이다.
        => TryStart(new ProcessStartInfo("control.exe", "powercfg.cpl") { UseShellExecute = true });

    /// <summary>모던 Windows 설정의 전원 페이지(전원 및 절전)를 연다. 안내 창은 닫지 않고 그대로 둔다.</summary>
    [RelayCommand]
    private void OpenModernPowerSettings()
        => TryStart(new ProcessStartInfo("ms-settings:powersleep") { UseShellExecute = true });

    /// <summary>아무 것도 열지 않고 안내 창을 닫는다(나중에).</summary>
    [RelayCommand]
    private async Task Dismiss()
        => await RequestCloseAsync();

    private static void TryStart(ProcessStartInfo startInfo)
    {
        try
        {
            Process.Start(startInfo);
        }
        catch
        {
            // 외부 설정 페이지 열기는 보조 동작이므로 실패해도 조용히 무시한다.
        }
    }
}
