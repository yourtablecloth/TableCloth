using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TableCloth;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;

namespace Spork.Components.Implementations
{
    public sealed class X509CertScanner : IX509CertScanner
    {
        public X509CertScanner(ILogger<X509CertScanner> logger)
        {
            _logger = logger;
        }

        private readonly ILogger _logger;

        public IEnumerable<X509CertPair> ScanLocalNpkiCertificates()
        {
            var results = new List<X509CertPair>();

            // 같은 인증서 트리를 두 번 훑지 않도록 정규화된 절대 경로로 방문 기록.
            // (WSB 안에서는 실제 LocalLow 와 WSB canonical 이 같은 경로로 풀린다.)
            var scannedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in GetCandidateRoots())
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                    continue;

                string normalized;
                try { normalized = Path.GetFullPath(root); }
                catch { normalized = root; }

                if (!scannedRoots.Add(normalized))
                    continue;

                try
                {
                    ScanRecursive(normalized, results);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "NPKI scan failed at {root}", normalized);
                }
            }

            // 여러 후보 경로가 같은 인증서를 가리킬 수 있으므로 CertHash 로 중복 제거.
            var deduped = results
                .Where(x => !string.IsNullOrEmpty(x.CertHash))
                .GroupBy(x => x.CertHash, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First());

            return X509CertPair.SortX509CertPairs(deduped);
        }

        /// <summary>
        /// NPKI 인증서를 탐색할 후보 루트 디렉터리들. 환경에 따라 일부만 존재한다.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        ///   <item>실제 <c>LocalLow\NPKI</c> — 사용자가 직접 만든 VM(임의 계정명)이나 일반 호스트.
        ///         WSB 안에서는 계정이 <c>WDAGUtilityAccount</c> 라 <see cref="SandboxMountPaths.NpkiCanonicalPath"/>
        ///         와 동일 경로로 풀려 모드 1 비회귀를 보장한다.</item>
        ///   <item><see cref="SandboxMountPaths.NpkiCanonicalPath"/> — 모드 1 표준 경로(명시적 폴백).</item>
        ///   <item><see cref="SandboxMountPaths.NpkiDesktopMount"/> — 무설치 <c>.wsb</c> 가 호스트 NPKI 를
        ///         RO 마운트한 위치.</item>
        ///   <item>제거식 드라이브(USB) 루트 — 인증서를 USB 로 들고 다니는 경우(호스트 측
        ///         <c>X509CertPairScanner.GetCandidateDirectories</c> 와 동일 규칙).</item>
        /// </list>
        /// </remarks>
        private IEnumerable<string> GetCandidateRoots()
        {
            var roots = new List<string>();

            // 실제 LocalLow\NPKI (계정명 비의존). WSB 에서는 canonical 과 같은 경로로 풀린다.
            try
            {
                var localLow = NativeMethods.GetKnownFolderPath(NativeMethods.LocalLowFolderGuid);
                if (!string.IsNullOrWhiteSpace(localLow))
                    roots.Add(Path.Combine(localLow, "NPKI"));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not resolve LocalLow NPKI path");
            }

            // 모드 1 표준 경로(명시적). 위 LocalLow 와 겹치면 scannedRoots 가 중복 스캔을 막는다.
            roots.Add(SandboxMountPaths.NpkiCanonicalPath);

            // 무설치 .wsb 가 호스트 NPKI 를 RO 마운트한 위치(Desktop\NPKI).
            roots.Add(SandboxMountPaths.NpkiDesktopMount);

            // 제거식 드라이브(USB).
            try
            {
                foreach (var drive in DriveInfo.GetDrives()
                    .Where(d => d.DriveType == DriveType.Removable)
                    .Where(d => d.IsReady && Directory.Exists(d.RootDirectory.FullName))
                    .Select(d => d.RootDirectory.FullName))
                {
                    roots.Add(drive);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not enumerate removable drives");
            }

            return roots;
        }

        private void ScanRecursive(string dir, List<X509CertPair> results)
        {
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(dir))
                    ScanRecursive(sub, results);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping subdirectories under {dir}", dir);
            }

            try
            {
                var der = Directory.GetFiles(dir, "signCert.der").FirstOrDefault();
                var key = Directory.GetFiles(dir, "signPri.key").FirstOrDefault();

                if (string.IsNullOrEmpty(der) || string.IsNullOrEmpty(key))
                    return;

                try
                {
                    var pair = new X509CertPair(File.ReadAllBytes(der), File.ReadAllBytes(key));
                    results.Add(pair);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load NPKI cert pair at {dir}", dir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping files under {dir}", dir);
            }
        }
    }
}
