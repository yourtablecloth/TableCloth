using System.Threading.Tasks;
using TableCloth.Models;

namespace TableCloth.Components;

public interface ICommandLineArguments
{
    Task<string> GetHelpStringAsync();

    Task<string> GetVersionStringAsync();

    CommandLineArgumentModel GetCurrent();

    /// <summary>
    /// 최초 실행 인자에 실린 딥링크 대상을 이미 처리했는지 여부.
    /// </summary>
    /// <remarks>
    /// 딥링크로 앱이 시작되면 스플래시가 그 자리에서 샌드박스를 실행한다. 그 뒤 사용자가 '식탁보 열기'를
    /// 골라 메인 창이 열리면 <see cref="GetCurrent"/> 는 여전히 같은 딥링크 인자를 돌려주므로, 이 표식이
    /// 없으면 같은 샌드박스를 한 번 더 띄우게 된다.
    /// </remarks>
    bool IsStartupTargetHandled { get; set; }
}