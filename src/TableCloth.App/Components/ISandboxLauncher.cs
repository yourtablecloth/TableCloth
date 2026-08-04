using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.Configuration;

namespace TableCloth.Components;

public interface ISandboxLauncher
{
    /// <summary>
    /// 샌드박스를 실행한다. 샌드박스 프로세스를 띄우는 데 성공하면 <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// 이 메서드는 샌드박스가 <b>뜰 때까지만</b> 기다린다 — 게스트가 종료될 때까지 기다리지 않는다.
    /// 실패 사유는 이미 메시지 박스로 사용자에게 표시되므로, 반환값은 호출자가 후속 UI(예: 딥링크
    /// 스플래시의 '다시 시도')를 결정하는 용도로만 쓴다.
    /// </remarks>
    Task<bool> RunSandboxAsync(TableClothConfiguration config, CancellationToken cancellationToken = default);
    bool ValidateSandboxSpecFile(string wsbFilePath, out string? reason);
}