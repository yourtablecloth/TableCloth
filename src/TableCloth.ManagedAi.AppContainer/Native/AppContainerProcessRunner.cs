using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace TableCloth.AppContainerPoc;

public sealed class AppContainerProcessRunner(string ownedRoot)
{
    // Grants and restoration cannot overlap, including across runner instances.
    private static readonly SemaphoreSlim GrantGate = new(1, 1);

    public static int RecoverLegacyPoCGrants(string ownedRoot)
    {
        GrantGate.Wait();
        try
        {
            var paths = new OwnedPaths(ownedRoot);
            using var lease = paths.AcquireAclLease();
            return AclScope.RecoverLegacyPoCGrants(paths);
        }
        finally { GrantGate.Release(); }
    }

    public static string? RecoverPendingAclJournals(string ownedRoot)
    {
        GrantGate.Wait();
        try
        {
            var paths = new OwnedPaths(ownedRoot);
            using var lease = paths.AcquireAclLease();
            return AclScope.RecoverPendingJournals(paths);
        }
        finally { GrantGate.Release(); }
    }

    public async Task<SandboxRunResult> RunAsync(SandboxRunRequest request, CancellationToken token)
    {
        await GrantGate.WaitAsync(token);
        try { return await RunCoreAsync(request, token); }
        finally { GrantGate.Release(); }
    }

    private async Task<SandboxRunResult> RunCoreAsync(SandboxRunRequest request, CancellationToken token)
    {
        OwnedPaths? paths = null;
        FileStream? ownedLease = null;
        Profile? profile = null;
        AclScope? acl = null;
        Child? child = null;
        string? runDirectory = null;
        var result = new SandboxRunResult(-1, "", "", false, false, false);
        var stage = "validation";
        string? cleanupFailure = null;
        OperationCanceledException? cancellationFailure = null;
        try
        {
            token.ThrowIfCancellationRequested();
            paths = new(ownedRoot);
            stage = "owned-root-lease";
            ownedLease = paths.AcquireAclLease();
            stage = "validation";
            if (Directory.EnumerateFiles(paths.Root, ".acl-journal-*.json").Any())
                throw new SecurityException("Pending ACL recovery must complete before another worker is launched.");
            Validate(paths, request);
            stage = "profile-create";
            profile = Profile.Create(request.AllowNetwork);
            stage = "owned-workspace";
            var runs = paths.Check(Path.Combine(paths.Root, ".appcontainer-runs"));
            Directory.CreateDirectory(runs);
            runDirectory = paths.Check(Path.Combine(runs, Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(runDirectory);
            foreach (var name in new[] { "home", "temp", "appdata", "localappdata" })
                Directory.CreateDirectory(Path.Combine(runDirectory, name));
            stage = "acl-grant";
            acl = new(paths, profile.Sid, profile.Name, token);
            acl.GrantRootTraversal();
            foreach (var directory in request.ReadOnlyDirectories) acl.GrantDirectory(directory, writable: false);
            foreach (var directory in request.ReadWriteDirectories) acl.GrantDirectory(directory, writable: true);
            acl.GrantDirectory(runDirectory, writable: true);
            token.ThrowIfCancellationRequested();
            stage = "appcontainer-launch";
            child = Child.Create(request, profile, EnvironmentBlock(request, runDirectory));
            result = result with { WasAppContainer = true };
            stage = "execution";
            using var timeout = new CancellationTokenSource(request.TimeoutMilliseconds);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
            using var stop = cancellation.Token.Register(child.Terminate);
            long outputBytes = 0;
            var outputLimited = 0;
            async Task<string> ReadAsync(Stream stream)
            {
                using var saved = new MemoryStream();
                var buffer = new byte[4096];
                while (true)
                {
                    var length = await stream.ReadAsync(buffer);
                    if (length == 0) break;
                    var total = Interlocked.Add(ref outputBytes, length);
                    var keep = (int)Math.Clamp(request.MaxOutputBytes - (total - length), 0, length);
                    if (keep != 0) saved.Write(buffer, 0, keep);
                    if (total <= request.MaxOutputBytes) continue;
                    Interlocked.Exchange(ref outputLimited, 1);
                    child.Terminate();
                }
                return Encoding.UTF8.GetString(saved.GetBuffer(), 0, (int)saved.Length);
            }
            async Task WriteAsync()
            {
                try
                {
                    var bytes = Encoding.UTF8.GetBytes(request.Stdin);
                    await child.Input.WriteAsync(bytes);
                    await child.Input.FlushAsync();
                }
                catch (IOException) { /* A worker may exit before consuming all input. */ }
                finally { child.Input.Dispose(); }
            }
            var stdout = ReadAsync(child.Output);
            var stderr = ReadAsync(child.Error);
            var input = WriteAsync();
            var wait = await Task.Run(() => Win32.WaitForSingleObject(child.Process, uint.MaxValue));
            Win32.Check(wait == 0);
            // Kill any remaining descendants before waiting for inherited pipes to reach EOF.
            child.Terminate();
            await Task.WhenAll(stdout, stderr, input);
            Win32.Check(Win32.GetExitCodeProcess(child.Process, out var exitCode));
            result = new(unchecked((int)exitCode), await stdout, await stderr,
                timeout.IsCancellationRequested && !token.IsCancellationRequested,
                Volatile.Read(ref outputLimited) != 0, true);
            token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException error) when (token.IsCancellationRequested) { cancellationFailure = error; }
        catch (Exception error) when (error is Win32Exception or IOException or UnauthorizedAccessException or
            ArgumentException or SecurityException or PlatformNotSupportedException)
        {
            result = result with { Failure = stage + ":" + ErrorCode(error) };
        }
        finally
        {
            try { child?.Dispose(); }
            catch (Exception error) when (error is Win32Exception or IOException or UnauthorizedAccessException)
            { cleanupFailure = "process-cleanup:" + ErrorCode(error); }
            if (acl is not null)
            {
                var aclFailure = acl.Restore();
                cleanupFailure ??= aclFailure;
            }
            if (profile is not null)
            {
                var profileFailure = profile.Delete();
                cleanupFailure ??= profileFailure;
            }
            if (paths is not null && runDirectory is not null)
            {
                try { paths.DeleteRunDirectory(runDirectory); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
                { cleanupFailure ??= "workspace-cleanup:" + ErrorCode(error); }
            }
            if (cleanupFailure is null && acl is not null)
            {
                try { acl.Complete(); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                { cleanupFailure = "journal-cleanup:" + ErrorCode(error); }
            }
            ownedLease?.Dispose();
        }
        if (cancellationFailure is not null)
        {
            if (cleanupFailure is not null) throw new IOException("Cancellation cleanup failed: " + cleanupFailure, cancellationFailure);
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cancellationFailure).Throw();
        }
        return cleanupFailure is null ? result : result with
        {
            Failure = result.Failure is null ? cleanupFailure : result.Failure + ";" + cleanupFailure
        };
    }

    private static string ErrorCode(Exception error) => AclScope.Describe(error);

    private static void Validate(OwnedPaths paths, SandboxRunRequest request)
    {
        if (request.TimeoutMilliseconds is < 1 or > 120000 || request.MemoryLimitBytes is < 16777216 or > 2147483648 ||
            request.MaxOutputBytes is < 1 or > 1048576 || request.MaxProcesses is < 1 or > 16 ||
            request.Arguments.Count > 128 || request.Arguments.Any(x => x is null || x.Length > 8192 || x.Contains('\0')) ||
            Encoding.UTF8.GetByteCount(request.Stdin) > 1048576 ||
            request.ReadOnlyDirectories.Count + request.ReadWriteDirectories.Count > 32)
            throw new ArgumentException("The sandbox request exceeds its budget.");
        var executable = paths.Check(request.Executable);
        var working = paths.Check(request.WorkingDirectory);
        paths.Identity(executable);
        if (!File.Exists(executable) || !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(working))
            throw new ArgumentException("A staged executable and working directory are required.");
        var read = request.ReadOnlyDirectories.Select(x => paths.Check(x)).ToArray();
        var write = request.ReadWriteDirectories.Select(x => paths.Check(x)).ToArray();
        if (read.Concat(write).Any(x => !Directory.Exists(x)) || !read.Any(x => Inside(x, executable)) ||
            !read.Concat(write).Any(x => Inside(x, working)) ||
            read.Any(ro => write.Any(rw => Inside(ro, rw) || Inside(rw, ro))))
            throw new ArgumentException("Executable access and disjoint owned grants are required.");
    }

    private static bool Inside(string directory, string path) => path.Equals(directory, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string EnvironmentBlock(SandboxRunRequest request, string runDirectory)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var environment = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = windows, ["WINDIR"] = windows,
            ["PATH"] = Path.GetDirectoryName(request.Executable)!, ["PATHEXT"] = ".EXE",
            ["HOME"] = Path.Combine(runDirectory, "home"), ["USERPROFILE"] = Path.Combine(runDirectory, "home"),
            ["APPDATA"] = Path.Combine(runDirectory, "appdata"), ["LOCALAPPDATA"] = Path.Combine(runDirectory, "localappdata"),
            ["TEMP"] = Path.Combine(runDirectory, "temp"), ["TMP"] = Path.Combine(runDirectory, "temp"),
            ["PYTHONNOUSERSITE"] = "1", ["PYTHONDONTWRITEBYTECODE"] = "1", ["PYTHONUTF8"] = "1"
        };
        return string.Join('\0', environment.Select(x => x.Key + "=" + x.Value)) + "\0\0";
    }

    private sealed class Profile(string name, nint sid, nint networkSid)
    {
        internal string Name => name;
        internal nint Sid => sid;
        internal nint NetworkSid => networkSid;

        internal static unsafe Profile Create(bool network)
        {
            var name = "TableCloth.SkillPoc." + Guid.NewGuid().ToString("N");
            nint networkSid = 0;
            try
            {
                if (network) Win32.Check(Win32.ConvertStringSidToSid("S-1-15-3-1", out networkSid));
                var capability = new Win32.SidAndAttributes { Sid = networkSid, Attributes = 4 };
                var status = Win32.CreateAppContainerProfile(name, name, "Dedicated TableCloth skill PoC worker",
                    network ? &capability : null, network ? 1u : 0, out var sid);
                if (status != 0)
                {
                    if (sid != 0) Win32.FreeSid(sid);
                    throw new Win32Exception(status);
                }
                return new(name, sid, networkSid);
            }
            catch
            {
                if (networkSid != 0) Win32.LocalFree(networkSid);
                throw;
            }
        }

        internal string? Delete()
        {
            var status = Win32.DeleteAppContainerProfile(name);
            Win32.FreeSid(sid);
            if (networkSid != 0) Win32.LocalFree(networkSid);
            return status == 0 ? null : "profile-delete:hresult-" + unchecked((uint)status).ToString("X8");
        }
    }

    private sealed class Child(SafeFileHandle process, SafeFileHandle thread, SafeFileHandle job,
        FileStream input, FileStream output, FileStream error) : IDisposable
    {
        private int _terminationError;
        internal SafeFileHandle Process => process;
        internal FileStream Input => input;
        internal FileStream Output => output;
        internal FileStream Error => error;

        internal void Terminate()
        {
            if (!job.IsClosed && !Win32.TerminateJobObject(job, 0xC000013A))
                Interlocked.CompareExchange(ref _terminationError, Marshal.GetLastPInvokeError(), 0);
        }

        public void Dispose()
        {
            Terminate();
            job.Dispose();
            var wait = Win32.WaitForSingleObject(process, 5000);
            var waitError = wait == uint.MaxValue ? Marshal.GetLastPInvokeError() : (int)wait;
            try { input.Dispose(); output.Dispose(); error.Dispose(); }
            finally { thread.Dispose(); process.Dispose(); }
            if (wait != 0) throw new Win32Exception(waitError);
            if (_terminationError != 0) throw new Win32Exception(_terminationError);
        }

        internal static unsafe Child Create(SandboxRunRequest request, Profile profile, string environment)
        {
            var attributes = new Win32.SecurityAttributes { Length = (uint)sizeof(Win32.SecurityAttributes), Inherit = 1 };
            SafeFileHandle? inputRead = null, inputWrite = null, outputRead = null, outputWrite = null,
                errorRead = null, errorWrite = null, process = null, thread = null, job = null;
                        nint list = 0;
                        var listReady = false;
                        var success = false;
                        try
                        {
                            Win32.Check(Win32.CreatePipe(out inputRead, out inputWrite, ref attributes, 0));
                            Win32.Check(Win32.CreatePipe(out outputRead, out outputWrite, ref attributes, 0));
                            Win32.Check(Win32.CreatePipe(out errorRead, out errorWrite, ref attributes, 0));
                            Win32.Check(Win32.SetHandleInformation(inputWrite, 1, 0));
                            Win32.Check(Win32.SetHandleInformation(outputRead, 1, 0));
                            Win32.Check(Win32.SetHandleInformation(errorRead, 1, 0));
                            job = Win32.CreateJobObject(0, 0);
                            if (job.IsInvalid) throw new Win32Exception(Marshal.GetLastPInvokeError());
                            var limits = new Win32.ExtendedLimits
                            {
                                Basic = new()
                                {
                                    Flags = 0x2000 | 0x8 | 0x100 | 0x200 | 0x400 | 0x2 | 0x4,
                                    ActiveProcesses = (uint)request.MaxProcesses,
                                    ProcessTime = request.TimeoutMilliseconds * 10000L,
                                    JobTime = request.TimeoutMilliseconds * 10000L
                                },
                                ProcessMemory = (nuint)request.MemoryLimitBytes, JobMemory = (nuint)request.MemoryLimitBytes
                            };
                            Win32.Check(Win32.SetInformationJobObject(job, 9, &limits, (uint)sizeof(Win32.ExtendedLimits)));
                            nuint size = 0;
                            Win32.InitializeProcThreadAttributeList(0, 2, 0, ref size);
                            if (size == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
                            list = (nint)NativeMemory.Alloc(size);
                            Win32.Check(Win32.InitializeProcThreadAttributeList(list, 2, 0, ref size));
                            listReady = true;
                            var capability = new Win32.SidAndAttributes { Sid = profile.NetworkSid, Attributes = 4 };
                            var capabilities = new Win32.SecurityCapabilities
                            {
                                AppContainerSid = profile.Sid,
                                Capabilities = profile.NetworkSid == 0 ? 0 : (nint)(&capability),
                                CapabilityCount = profile.NetworkSid == 0 ? 0u : 1
                            };
                            Win32.Check(Win32.UpdateProcThreadAttribute(list, 0, 0x20009, &capabilities,
                                (nuint)sizeof(Win32.SecurityCapabilities), 0, 0));
                            var handles = stackalloc nint[] { inputRead.DangerousGetHandle(), outputWrite.DangerousGetHandle(), errorWrite.DangerousGetHandle() };
                            Win32.Check(Win32.UpdateProcThreadAttribute(list, 0, 0x20002, handles, (nuint)(3 * sizeof(nint)), 0, 0));
                            var startup = new Win32.StartupInfoEx
                            {
                                Startup = new()
                                {
                                    Size = (uint)sizeof(Win32.StartupInfoEx), Flags = 0x100,
                                    Input = handles[0], Output = handles[1], Error = handles[2]
                                },
                                Attributes = list
                            };
                            var command = string.Join(' ', new[] { request.Executable }.Concat(request.Arguments).Select(Quote)) + '\0';
                            if (command.Length > 32767) throw new ArgumentException("The command line exceeds the Win32 limit.");
                            fixed (char* commandLine = command)
                            fixed (char* variables = environment)
                            {
                                Win32.Check(Win32.CreateProcess(request.Executable, commandLine, 0, 0, true,
                                    0x00080000 | 0x00000400 | 0x00000004 | 0x08000000,
                                    variables, request.WorkingDirectory, ref startup, out var information));
                                process = new(information.Process, ownsHandle: true);
                                thread = new(information.Thread, ownsHandle: true);
                            }
                            Win32.Check(Win32.OpenProcessToken(process, 8, out var token));
                            using (token)
                            {
                                Win32.Check(Win32.GetTokenInformation(token, 29, out var isContainer, 4, out _));
                                if (isContainer != 1) throw new SecurityException("The worker token is not an AppContainer.");
                            }
                            Win32.Check(Win32.AssignProcessToJobObject(job, process));
                            var child = new Child(process, thread, job,
                                new FileStream(inputWrite, FileAccess.Write), new FileStream(outputRead, FileAccess.Read),
                                new FileStream(errorRead, FileAccess.Read));
                            Win32.Check(Win32.ResumeThread(thread) != uint.MaxValue);
                            success = true;
                            return child;
                        }
                        finally
                        {
                            if (listReady) Win32.DeleteProcThreadAttributeList(list);
                            if (list != 0) NativeMemory.Free((void*)list);
                            inputRead?.Dispose(); outputWrite?.Dispose(); errorWrite?.Dispose();
                            if (!success)
                            {
                                if (process is not null && !process.IsInvalid)
                                {
                                    Win32.TerminateProcess(process, 0xC000013A);
                                    Win32.WaitForSingleObject(process, 5000);
                                }
                                process?.Dispose(); thread?.Dispose(); job?.Dispose();
                                inputWrite?.Dispose(); outputRead?.Dispose(); errorRead?.Dispose();
                            }
                        }
        }

        private static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            var slashes = 0;
            foreach (var character in value)
            {
                if (character == '\\') { slashes++; continue; }
                result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
                result.Append(character);
                slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
    }
}
