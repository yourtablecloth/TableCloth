using System.Threading;
using System.Threading.Tasks;

namespace Spork.Components.Implementations
{
    /// <summary>
    /// 비-샌드박스 환경(예: 단독 출시 Spork.exe)에서 사용되는 <see cref="ISandboxBootstrap"/>의 noop 기본 구현.
    /// Spork.App만 참조하는 빌드에는 본 구현이 그대로 사용되어 sandbox-only 코드 경로(netsh, NPKI 등)가
    /// 바이너리에 포함되지 않는다. TableCloth.exe spork verb는
    /// <c>builder.UseSandboxBootstrap()</c>(Spork.Sandbox 어셈블리 제공)로 이 등록을 실제 구현으로
    /// 교체한다.
    /// </summary>
    public sealed class NoopSandboxBootstrap : ISandboxBootstrap
    {
        public Task RunAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
