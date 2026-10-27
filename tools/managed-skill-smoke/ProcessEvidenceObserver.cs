#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace TableCloth.SkillSmokeDiagnostics
{
    public sealed class ProcessObservation
    {
        public string Kind { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public int ParentProcessId { get; set; }
        public DateTimeOffset StartedUtc { get; set; }
        public DateTimeOffset ObservedUtc { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public bool TokenQuerySucceeded { get; set; }
        public bool? IsAppContainer { get; set; }
        public int? QueryError { get; set; }
    }

    // This observer runs in the test controller, independently of broker reports and worker
    // stdout. It reads only process ancestry, exact expected image files and token class 29.
    // It never reads command lines, environments, credentials or general token information.
    public sealed class ProcessEvidenceObserver : IDisposable
    {
        private const int MaximumProcesses = 8192;
        private readonly int _rootProcessId;
        private readonly Dictionary<string, string> _expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<int> _identified = new HashSet<int>();
        private readonly ConcurrentQueue<ProcessObservation> _observations = new ConcurrentQueue<ProcessObservation>();
        private readonly ManualResetEventSlim _stop = new ManualResetEventSlim(false, 0);
        private readonly object _gate = new object();
        private Thread? _thread;
        private Exception? _failure;
        private ProcessObservation[]? _result;
        private bool _disposed;

        public ProcessEvidenceObserver(int rootProcessId, string brokerExecutable, string pythonExecutable, string nodeExecutable)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) throw new PlatformNotSupportedException();
            if (rootProcessId <= 0) throw new ArgumentOutOfRangeException(nameof(rootProcessId));
            _rootProcessId = rootProcessId;
            _expected.Add(ExpectedPath(brokerExecutable, "TableCloth.exe"), "broker");
            _expected.Add(ExpectedPath(pythonExecutable, "python.exe"), "worker");
            _expected.Add(ExpectedPath(nodeExecutable, "node.exe"), "worker");
        }

        public void Start()
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(ProcessEvidenceObserver));
                if (_result is not null) throw new InvalidOperationException("A stopped observer cannot be restarted.");
                if (_thread is not null) return;
                _thread = new Thread(Observe) { IsBackground = true, Name = "TableCloth process evidence observer" };
                _thread.Start();
            }
        }

        public ProcessObservation[] Stop()
        {
            lock (_gate)
            {
                if (_result is not null) return (ProcessObservation[])_result.Clone();
                _stop.Set();
                if (_thread is not null && !_thread.Join(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("The native process evidence observer did not stop.");
                if (_failure is not null) throw new InvalidOperationException("Native process observation failed.", _failure);
                _result = _observations.ToArray();
                return (ProcessObservation[])_result.Clone();
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                try { Stop(); }
                finally
                {
                    _disposed = true;
                    if (_thread is null || !_thread.IsAlive) _stop.Dispose();
                }
            }
        }

        private void Observe()
        {
            try
            {
                do { Capture(); }
                while (!_stop.Wait(20));
            }
            catch (Exception error) { _failure = error; }
        }

        private void Capture()
        {
            var processes = Snapshot();
            foreach (var pair in processes)
            {
                var process = pair.Value;
                if (process.Id == _rootProcessId || !ExpectedName(process.Name) || !IsDescendant(process, processes)) continue;
                ObserveProcess(process);
            }
        }

        private bool IsDescendant(ProcessEntry process, Dictionary<int, ProcessEntry> processes)
        {
            var visited = new HashSet<int>();
            var parent = process.Parent;
            for (var count = 0; count < MaximumProcesses && parent > 0 && visited.Add(parent); count++)
            {
                if (parent == _rootProcessId) return true;
                if (!processes.TryGetValue(parent, out var ancestor)) return false;
                parent = ancestor.Parent;
            }
            return false;
        }

        private bool ExpectedName(string name)
        {
            foreach (var path in _expected.Keys)
                if (string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void ObserveProcess(ProcessEntry entry)
        {
            var handle = Native.OpenProcess(0x1000, false, entry.Id); // PROCESS_QUERY_LIMITED_INFORMATION
            if (handle == IntPtr.Zero)
            {
                RecordUnavailable(entry, Marshal.GetLastWin32Error());
                return;
            }
            try
            {
                var buffer = new StringBuilder(32768);
                var length = (uint)buffer.Capacity;
                if (!Native.QueryFullProcessImageNameW(handle, 0, buffer, ref length))
                {
                    RecordUnavailable(entry, Marshal.GetLastWin32Error());
                    return;
                }
                var image = Path.GetFullPath(buffer.ToString());
                if (!_expected.TryGetValue(image, out var kind)) return;
                long started = 0;
                int? queryError = null;
                if (Native.GetProcessTimes(handle, out var creation, out _, out _, out _)) started = creation.ToInt64();
                else queryError = Marshal.GetLastWin32Error();
                if (!_seen.Add(entry.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + started.ToString(System.Globalization.CultureInfo.InvariantCulture))) return;
                if (started > 0) _identified.Add(entry.Id);
                var observation = new ProcessObservation
                {
                    Kind = kind,
                    ProcessId = entry.Id,
                    ParentProcessId = entry.Parent,
                    StartedUtc = started > 0 ? new DateTimeOffset(DateTime.FromFileTimeUtc(started)) : DateTimeOffset.MinValue,
                    ObservedUtc = DateTimeOffset.UtcNow,
                    ImagePath = image,
                    QueryError = queryError
                };
                QueryToken(handle, observation);
                try
                {
                    if (!_hashes.TryGetValue(image, out var hash))
                    {
                        using (var file = new FileStream(image, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan))
                        using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", string.Empty).ToLowerInvariant();
                        _hashes.Add(image, hash);
                    }
                    observation.Sha256 = hash;
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is CryptographicException)
                { observation.QueryError = error.HResult & 0xffff; }
                _observations.Enqueue(observation);
            }
            finally { Native.CloseHandle(handle); }
        }

        private void RecordUnavailable(ProcessEntry entry, int error)
        {
            // An exact image path could not be measured. Keep the failure visibly incomplete
            // instead of manufacturing positive token evidence from a matching basename.
            // A process already measured by PID+creation time may exit between the next
            // Toolhelp snapshot and OpenProcess; that normal exit adds no missing evidence.
            if (_identified.Contains(entry.Id) || !_seen.Add(entry.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":unavailable")) return;
            _observations.Enqueue(new ProcessObservation
            {
                Kind = string.Equals(entry.Name, "TableCloth.exe", StringComparison.OrdinalIgnoreCase) ? "broker" : "worker",
                ProcessId = entry.Id,
                ParentProcessId = entry.Parent,
                StartedUtc = DateTimeOffset.MinValue,
                ObservedUtc = DateTimeOffset.UtcNow,
                TokenQuerySucceeded = false,
                IsAppContainer = null,
                QueryError = error
            });
        }

        private static void QueryToken(IntPtr process, ProcessObservation observation)
        {
            if (!Native.OpenProcessToken(process, 0x0008, out var token)) // TOKEN_QUERY only
            { observation.QueryError = Marshal.GetLastWin32Error(); return; }
            try
            {
                if (!Native.GetTokenInformation(token, 29, out var isAppContainer, sizeof(int), out var returned))
                { observation.QueryError = Marshal.GetLastWin32Error(); return; }
                if (returned != sizeof(int) || isAppContainer is not (0 or 1))
                { observation.QueryError = 13; return; } // ERROR_INVALID_DATA
                observation.TokenQuerySucceeded = true;
                observation.IsAppContainer = isAppContainer != 0;
            }
            finally { Native.CloseHandle(token); }
        }

        private static Dictionary<int, ProcessEntry> Snapshot()
        {
            var snapshot = Native.CreateToolhelp32Snapshot(0x00000002, 0); // TH32CS_SNAPPROCESS
            if (snapshot == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var result = new Dictionary<int, ProcessEntry>();
                var entry = new Native.ProcessEntry32 { Size = (uint)Marshal.SizeOf(typeof(Native.ProcessEntry32)), Name = string.Empty };
                if (!Native.Process32FirstW(snapshot, ref entry))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error != 18) throw new Win32Exception(error); // ERROR_NO_MORE_FILES
                    return result;
                }
                var enumerated = 0;
                do
                {
                    if (++enumerated > MaximumProcesses) throw new InvalidOperationException("Process snapshot exceeds its observation budget.");
                    if (entry.ProcessId <= int.MaxValue && entry.ParentProcessId <= int.MaxValue)
                    {
                        var process = new ProcessEntry((int)entry.ProcessId, (int)entry.ParentProcessId, entry.Name ?? string.Empty);
                        result[process.Id] = process;
                    }
                }
                while (Native.Process32NextW(snapshot, ref entry));
                var lastError = Marshal.GetLastWin32Error();
                if (lastError != 18) throw new Win32Exception(lastError);
                return result;
            }
            finally { Native.CloseHandle(snapshot); }
        }

        private static string ExpectedPath(string path, string fileName)
        {
            if (string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) ||
                !string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("An exact local expected executable path is required.", nameof(path));
            var full = Path.GetFullPath(path);
            if (full.Length < 3 || full[1] != ':' || full.IndexOf(':', 2) >= 0)
                throw new ArgumentException("A local drive path without alternate streams is required.", nameof(path));
            return full;
        }

        private sealed class ProcessEntry
        {
            internal readonly int Id;
            internal readonly int Parent;
            internal readonly string Name;
            internal ProcessEntry(int id, int parent, string name) { Id = id; Parent = parent; Name = name; }
        }

        private static class Native
        {
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal struct ProcessEntry32
            {
                internal uint Size;
                internal uint Usage;
                internal uint ProcessId;
                internal UIntPtr DefaultHeapId;
                internal uint ModuleId;
                internal uint ThreadCount;
                internal uint ParentProcessId;
                internal int BasePriority;
                internal uint Flags;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string? Name;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct FileTime
            {
                internal uint Low;
                internal uint High;
                internal long ToInt64() { return unchecked((long)(((ulong)High << 32) | Low)); }
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
            [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry32 entry);
            [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry32 entry);
            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);
            [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, StringBuilder image, ref uint size);
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetProcessTimes(IntPtr process, out FileTime creation, out FileTime exit, out FileTime kernel, out FileTime user);
            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetTokenInformation(IntPtr token, int informationClass, out int information, uint length, out uint returned);
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CloseHandle(IntPtr handle);
        }
    }
}
