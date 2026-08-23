namespace AvaloniaSlice;

/// <summary>DI로 주입되는 데모 서비스. 실 앱의 서비스 주입 패턴을 AOT에서 검증하기 위한 최소 예.</summary>
public interface IGreetingService
{
    string Greet();
}

public sealed class GreetingService : IGreetingService
{
    public string Greet() => "이 메시지는 DI 서비스(IGreetingService)에서 주입되었습니다 — Host + Lemon.Hosting + Native AOT 통합 검증.";
}
