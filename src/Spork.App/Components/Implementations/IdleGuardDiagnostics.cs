using System;
using System.IO;

namespace Spork.Components.Implementations
{
    /// <summary>
    /// [미리 보기] idle-guard(이슈 #197)는 창 없는 백그라운드 프로세스라 문제가 생겨도 눈에 보이지 않는다.
    /// 진단을 위해 실행 폴더(<see cref="AppContext.BaseDirectory"/>)에 간단한 텍스트 로그를 남긴다.
    /// 쓰기 실패(예: RO 마운트)해도 가드 동작을 막지 않도록 best-effort로 처리한다.
    /// </summary>
    internal static class IdleGuardDiagnostics
    {
        private static readonly object _gate = new();
        private static readonly string _logPath = Path.Combine(AppContext.BaseDirectory, "idle-guard.log");

        public static void Log(string message)
        {
            try
            {
                lock (_gate)
                    File.AppendAllText(_logPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
            catch
            {
                // best-effort: 진단 로그 실패가 가드 자체를 방해해선 안 된다.
            }
        }
    }
}
