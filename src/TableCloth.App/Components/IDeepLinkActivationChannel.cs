using System;

namespace TableCloth.Components;

/// <summary>
/// 이미 실행 중인 인스턴스에 <c>tablecloth:</c> 딥링크 페이로드를 전달하는 프로세스 간 채널.
/// </summary>
/// <remarks>
/// 호스트 앱은 Global mutex 로 단일 인스턴스를 강제한다(<see cref="IAppStartup"/>). 그래서 앱이 떠 있는
/// 상태에서 브라우저가 딥링크를 실행하면 두 번째 인스턴스가 "이미 실행 중" 오류로 끝나 링크가 무시된다.
/// 이 채널이 그 사이를 잇는다: 두 번째 인스턴스는 페이로드만 넘기고 즉시 종료하고, 원래 인스턴스가
/// 창을 활성화한 뒤 링크를 처리한다.
/// </remarks>
public interface IDeepLinkActivationChannel : IDisposable
{
    /// <summary>
    /// 실행 중인 인스턴스에 페이로드 전달을 시도한다.
    /// </summary>
    /// <returns>전달에 성공하면 <see langword="true"/>. 이 경우 호출자는 조용히 종료해야 한다.</returns>
    bool TrySendToRunningInstance(string payload);

    /// <summary>
    /// 수신 대기를 시작한다. 이미 대기 중이면 아무 일도 하지 않는다.
    /// </summary>
    /// <param name="onPayloadReceived">
    /// 페이로드 수신 시 호출된다. <b>백그라운드 스레드에서 호출</b>되므로 UI 작업은 호출 측이 디스패처로 옮겨야 한다.
    /// </param>
    void StartListening(Action<string> onPayloadReceived);
}
