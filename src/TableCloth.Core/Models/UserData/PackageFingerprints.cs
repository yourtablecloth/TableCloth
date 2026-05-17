using System;
using System.Security.Cryptography;
using System.Text;

namespace TableCloth.Models.UserData
{
    /// <summary>
    /// <see cref="SporkUserData.InstalledFingerprints"/>에 저장될 fingerprint 문자열을 일관된 규칙으로 생성한다.
    /// 같은 (URL, args) 조합 / 같은 확장 ID / 같은 스크립트 본문이라면 동일 fingerprint가 나와야 하므로
    /// 모든 호출자(StepsComposer 의 skip 판정, 각 install Step 의 성공 시 기록)는 본 헬퍼를 거친다.
    /// </summary>
    /// <remarks>
    /// 의도:
    /// <list type="bullet">
    ///   <item>카탈로그가 패키지 URL/인수/스크립트 본문을 갱신하면 새 fingerprint 가 생성되어 자동 재설치.</item>
    ///   <item>스크립트 본문은 길이가 클 수 있어 sha256 해시로 축약(파일 크기 절약).</item>
    ///   <item>확장 ID 는 안정 식별자라 해시 없이 그대로 사용 — 디버그 시 식별성 보존.</item>
    /// </list>
    /// </remarks>
    public static class PackageFingerprints
    {
        public const string PackagePrefix = "pkg:";
        public const string EdgeExtensionPrefix = "edgeext:";
        public const string PowerShellScriptPrefix = "ps:";

        public static string ForPackage(string packageUrl, string arguments)
            => PackagePrefix + Sha256(Normalize(packageUrl) + "|" + Normalize(arguments));

        public static string ForEdgeExtension(string extensionId)
            => EdgeExtensionPrefix + Normalize(extensionId);

        public static string ForPowerShellScript(string scriptContent)
            => PowerShellScriptPrefix + Sha256(Normalize(scriptContent));

        private static string Normalize(string value)
            => value ?? string.Empty;

        private static string Sha256(string input)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                return ToHex(bytes);
            }
        }

        private static string ToHex(byte[] bytes)
        {
            // netstandard2.0 호환을 위해 Convert.ToHexString 대신 직접 변환.
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
