using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Spork.Components;

namespace Spork.Sandbox
{
    /// <summary>
    /// 통합 진입점(TableCloth.exe spork verb)이 <see cref="UseSporkExtensions.UseSpork"/> 직후 호출하여
    /// <see cref="ISandboxBootstrap"/> 등록을 실제 sandbox 구현으로 교체한다. 단독 출시 Spork.exe는
    /// 본 어셈블리(Spork.Sandbox)를 참조하지 않으므로 기본 noop 구현이 그대로 사용된다.
    /// </summary>
    public static class UseSandboxBootstrapExtensions
    {
        public static IHostApplicationBuilder UseSandboxBootstrap(this IHostApplicationBuilder builder)
        {
            // UseSpork()에서 기본 등록한 noop 구현을 제거하고 실제 sandbox 구현으로 교체.
            // Replace는 동일 ServiceType의 마지막 등록만 교체 — 동일 인터페이스에 한 구현만 존재함을 가정.
            builder.Services.Replace(
                ServiceDescriptor.Singleton<ISandboxBootstrap, SandboxBootstrap>());
            return builder;
        }
    }
}
