using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Threading;
using TableCloth.Models;

namespace TableCloth.Components.Implementations;

/// <summary>
/// 명명된 파이프로 구현한 <see cref="IDeepLinkActivationChannel"/>.
/// </summary>
/// <remarks>
/// <para>
/// 파이프 이름에 현재 사용자 SID 를 넣어 같은 PC 의 다른 계정이 각자의 인스턴스를 갖도록 한다.
/// 접근 제어는 따로 지정하지 않고 명명된 파이프의 기본 보안 서술자에 맡긴다 — 기본 DACL 은 생성한
/// 사용자와 SYSTEM 에만 쓰기를 허용하므로 다른 사용자 계정이 페이로드를 밀어 넣을 수 없다.
/// </para>
/// <para>
/// 수신한 페이로드는 <b>신뢰할 수 없는 입력</b>이다. 이 클래스는 길이 상한만 적용하고 해석하지 않으며,
/// 실제 판정은 <see cref="TableClothUri"/> 파서와 카탈로그 도메인 게이트가 한다.
/// </para>
/// </remarks>
public sealed class DeepLinkActivationChannel : IDeepLinkActivationChannel
{
    public DeepLinkActivationChannel(ILogger<DeepLinkActivationChannel> logger)
    {
        _logger = logger;
    }

    // 실행 중인 인스턴스를 찾는 데 쓰는 대기 시간. 짧게 잡아 "앱이 안 떠 있는" 흔한 경우에
    // 사용자가 기다리지 않게 한다(이 시간만큼 딥링크 첫 실행이 늦어진다).
    private const int ConnectTimeoutMilliseconds = 500;

    // 페이로드 길이 상한. 악의적인 클라이언트가 무한정 밀어 넣는 것을 막는다.
    private const int MaxPayloadBytes = TableClothUri.MaxLength * 4;

    private readonly ILogger? _logger;
    private CancellationTokenSource? _cancellation;
    private Thread? _listenerThread;
    private bool _disposed;

    /// <summary>
    /// DI 컨테이너가 준비되기 전(진입점)에서 쓰는 전달 경로.
    /// </summary>
    public static bool TrySend(string payload)
        => TrySendCore(payload, null);

    public bool TrySendToRunningInstance(string payload)
        => TrySendCore(payload, _logger);

    /// <summary>
    /// 사용자별 파이프 이름. SID 를 못 읽으면 도메인/사용자 이름으로 대체한다.
    /// </summary>
    private static string GetPipeName()
    {
        var scope = default(string);

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            scope = identity?.User?.Value;
        }
        catch (Exception)
        {
            // 아래 폴백을 사용한다.
        }

        if (string.IsNullOrWhiteSpace(scope))
            scope = $"{Environment.UserDomainName}_{Environment.UserName}";

        return $"TableCloth.DeepLink.{scope}";
    }

    private static bool TrySendCore(string payload, ILogger? logger)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        var buffer = Encoding.UTF8.GetBytes(payload);

        if (buffer.Length > MaxPayloadBytes)
            return false;

        try
        {
            using var client = new NamedPipeClientStream(".", GetPipeName(), PipeDirection.Out);
            client.Connect(ConnectTimeoutMilliseconds);
            client.Write(buffer, 0, buffer.Length);
            client.Flush();
            return true;
        }
        catch (TimeoutException)
        {
            // 실행 중인 인스턴스가 없다 — 정상 경로(이 프로세스가 새 인스턴스가 된다).
            return false;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to hand the deep link over to the running instance.");
            return false;
        }
    }

    public void StartListening(Action<string> onPayloadReceived)
    {
        ArgumentNullException.ThrowIfNull(onPayloadReceived);

        if (_disposed || _listenerThread != null)
            return;

        _cancellation = new CancellationTokenSource();
        _listenerThread = new Thread(() => ListenLoop(onPayloadReceived, _cancellation.Token))
        {
            IsBackground = true,
            Name = nameof(DeepLinkActivationChannel),
        };
        _listenerThread.Start();
    }

    private void ListenLoop(Action<string> onPayloadReceived, CancellationToken cancellationToken)
    {
        var pipeName = GetPipeName();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    pipeName, PipeDirection.In, maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                server.WaitForConnection();

                var payload = ReadPayload(server);

                if (!string.IsNullOrWhiteSpace(payload) && !cancellationToken.IsCancellationRequested)
                    onPayloadReceived(payload!);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                // 한 번의 실패로 채널을 죽이지 않는다. 다만 즉시 재시도해 CPU 를 태우지 않도록 잠시 쉰다.
                _logger?.LogWarning(ex, "The deep link listener failed to accept a connection.");
                Thread.Sleep(500);
            }
        }
    }

    private static string? ReadPayload(Stream stream)
    {
        var buffer = new byte[MaxPayloadBytes];
        var total = 0;

        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);

            if (read < 1)
                break;

            total += read;
        }

        return total < 1 ? null : Encoding.UTF8.GetString(buffer, 0, total).Trim();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try { _cancellation?.Cancel(); }
        catch (Exception) { /* 종료 경로의 예외는 흡수 */ }

        // 대기 중인 WaitForConnection 을 깨우기 위해 자기 자신에게 접속을 시도한다.
        try
        {
            using var wakeup = new NamedPipeClientStream(".", GetPipeName(), PipeDirection.Out);
            wakeup.Connect(50);
        }
        catch (Exception) { /* 이미 닫혔으면 그만 */ }

        try { _cancellation?.Dispose(); }
        catch (Exception) { /* 종료 경로의 예외는 흡수 */ }

        _cancellation = null;
        _listenerThread = null;
    }
}
