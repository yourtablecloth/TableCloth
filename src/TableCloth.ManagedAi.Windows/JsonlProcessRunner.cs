using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace TableCloth.ManagedAi.Windows;

public sealed class JsonlProcessRunner : IJsonlProcessRunner
{
    public async Task<ProcessOutcome> RunAsync(ProcessRunSpec spec, Action<string> stdout,
        Action<string>? stderr, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = new CancellationTokenSource(spec.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var process = new Process { StartInfo = spec.StartInfo };
        ProcessJob? job = null;
        Exception? firstFailure = null;
        try
        {
            if (!process.Start()) throw new ManagedAiException(AiFailureCode.BlockedByPolicy);
            job = new ProcessJob(process);
            using var registration = linked.Token.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                catch (Win32Exception) { }
                job.Dispose();
            });

            async Task<T> Guard<T>(Func<Task<T>> action)
            {
                try { return await action(); }
                catch (Exception ex)
                {
                    Interlocked.CompareExchange(ref firstFailure, ex, null);
                    await linked.CancelAsync();
                    throw;
                }
            }
            var inputReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var output = Guard(() => ReadLinesAsync(process.StandardOutput.BaseStream, spec.MaxStdoutBytes, spec.MaxLineBytes, async line =>
            {
                stdout(line);
                if (spec.Respond is null) return;
                await inputReady.Task.WaitAsync(linked.Token);
                var reply = spec.Respond(line);
                if (reply?.Text is { } text)
                {
                    await process.StandardInput.WriteAsync(text.AsMemory(), linked.Token);
                    await process.StandardInput.FlushAsync(linked.Token);
                }
                if (reply?.Close == true) process.StandardInput.Close();
            }, linked.Token));
            var error = Guard(() => ReadLinesAsync(process.StandardError.BaseStream, spec.MaxStderrBytes, spec.MaxLineBytes,
                stderr is null ? null : line => { stderr(line); return Task.CompletedTask; }, linked.Token));
            var input = Guard(async () =>
            {
                if (spec.Input is not null) await process.StandardInput.WriteAsync(spec.Input.AsMemory(), linked.Token);
                if (spec.Respond is not null) await process.StandardInput.FlushAsync(linked.Token);
                if (spec.Respond is null) process.StandardInput.Close();
                inputReady.TrySetResult();
                await process.WaitForExitAsync(linked.Token);
                // A launcher child can inherit stdout/stderr and keep them open after the parent exits.
                // Close the job before awaiting EOF, otherwise cleanup itself waits until the timeout.
                job.Dispose();
                return 0L;
            });
            try { await Task.WhenAll(output, error, input); }
            catch
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (timeout.IsCancellationRequested) throw new ManagedAiException(AiFailureCode.ProviderTimeout);
                if (firstFailure is ManagedAiException failure) throw failure;
                throw new ManagedAiException(AiFailureCode.ProviderFailed);
            }
            return new(process.ExitCode, await output, await error);
        }
        catch (Win32Exception) { throw new ManagedAiException(AiFailureCode.BlockedByPolicy); }
        finally
        {
            // Closing the job also terminates descendants after their parent has already exited.
            job?.Dispose();
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await process.WaitForExitAsync(exitTimeout.Token);
                }
            }
            catch (InvalidOperationException) { }
        }
    }

    private static async Task<long> ReadLinesAsync(Stream stream, int maxBytes, int maxLineBytes,
        Func<string, Task>? onLine, CancellationToken token)
    {
        var utf8 = new UTF8Encoding(false, true);
        var buffer = new byte[8192];
        using var line = new MemoryStream();
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, token)) != 0)
        {
            total += read;
            if (total > maxBytes) throw new ManagedAiException(AiFailureCode.OutputLimitExceeded);
            for (int i = 0; i < read; i++)
            {
                if (buffer[i] == (byte)'\n')
                {
                    if (onLine is not null) await onLine(utf8.GetString(line.GetBuffer(), 0, (int)line.Length).TrimEnd('\r'));
                    line.SetLength(0);
                }
                else
                {
                    if (line.Length >= maxLineBytes) throw new ManagedAiException(AiFailureCode.OutputLimitExceeded);
                    line.WriteByte(buffer[i]);
                }
            }
        }
        if (line.Length != 0 && onLine is not null) await onLine(utf8.GetString(line.GetBuffer(), 0, (int)line.Length).TrimEnd('\r'));
        return total;
    }
}
