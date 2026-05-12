using Microsoft.Extensions.Hosting;

namespace TableCloth.App.DependencyInjection;

/// <summary>
/// 진입점에서 <see cref="IHostApplicationBuilder"/>에 TableCloth 호스트 런처 모듈을
/// 합성하는 확장 메서드 모음. verb 기반 단일 바이너리 구조에서 TableCloth verb 핸들러가
/// 이 메서드를 호출하여 모든 서비스/뷰모델/뷰를 DI에 등록한다.
/// </summary>
public static class UseTableClothExtensions
{
    /// <summary>
    /// TableCloth 모듈의 모든 의존성을 빌더에 등록한다.
    /// </summary>
    /// <remarks>
    /// Phase 1 진행 중 — 본 메서드는 현재 골격 상태이며, 진입점(<c>Program.cs</c>)의
    /// 인라인 DI 등록을 영역별(Components → ViewModels → Views)로 점진 이전한다.
    /// </remarks>
    public static IHostApplicationBuilder UseTableCloth(this IHostApplicationBuilder builder)
    {
        return builder;
    }
}
