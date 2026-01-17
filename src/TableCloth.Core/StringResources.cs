using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace TableCloth.Resources
{
    public static partial class StringResources { }

    // 공동 인증서 관련 문자열들
    partial class StringResources
    {
        public static readonly TimeSpan Cert_ExpireWindow = TimeSpan.FromDays(-3d);

        public static string Cert_Availability_MayTooEarly(DateTime now, DateTime notBefore)
            => string.Format(UIStringResources.Cert_Availability_MayTooEarly, (int)Math.Truncate((notBefore - now).TotalDays));

        public static string Cert_Availability_ExpireSoon(DateTime now, DateTime notAfter, TimeSpan expireWindow)
            => string.Format(UIStringResources.Cert_Availability_ExpireSoon, (int)Math.Truncate((now - (notAfter - expireWindow)).TotalDays));

        public static string Error_Cert_MayTooEarly(DateTime now, DateTime notBefore)
            => string.Format(ErrorStrings.Error_Cert_MayTooEarly, (int)Math.Truncate((notBefore - now).TotalDays));

        public static string Error_Cert_ExpireSoon(DateTime now, DateTime notAfter, TimeSpan expireWindow)
            => string.Format(ErrorStrings.Error_Cert_ExpireSoon, (int)Math.Truncate((now - (notAfter - expireWindow)).TotalDays));

        public static string Error_X509CertError(string certSubject, string errorCode)
            => string.Format(ErrorStrings.Error_X509CertError, certSubject, errorCode);
    }

    // GitHub User-Agent 헤더 문자열 생성
    partial class StringResources
    {
        // https://docs.github.com/en/rest/using-the-rest-api/getting-started-with-the-rest-api#user-agent
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/User-Agent
        public static string TableCloth_GitHubRestUAString
        {
            get
            {
                var asm = Assembly.GetExecutingAssembly();
                var asmProduct = asm.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
                var asmVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

                var os = Environment.OSVersion;

                var resourceNames = asm.GetManifestResourceNames();
                var commitTextFileName = resourceNames.Where(x => x.EndsWith("commit.txt", StringComparison.Ordinal)).FirstOrDefault();

                return $"{asmProduct}/{asmVersion} ({os.Platform}; {os.VersionString}; {RuntimeInformation.ProcessArchitecture}) yourtablecloth";
            }
        }
    }

    // 오류 메시지에 표시될 문자열들
    partial class StringResources
    {
        public static string Error_Cannot_Invoke_GetVersionEx(int errorCode)
            => string.Format(ErrorStrings.Error_Cannot_Invoke_GetVersionEx, errorCode);

        public static string Error_HostFolder_Unavailable(IEnumerable<string> unavailableDirectories)
        {
            var buffer = new StringBuilder();
            foreach (var eachUnavailableDirectory in unavailableDirectories)
                buffer.AppendLine(string.Format(ErrorStrings.Error_HostFolder_Unavailable_Item, eachUnavailableDirectory));

            return ErrorStrings.Error_HostFolder_Unavailable + Environment.NewLine +
                Environment.NewLine + buffer.ToString();
        }

        public static string Error_MappedFolder_DuplicateLeafName(IEnumerable<IGrouping<string, string>> duplicateGroups)
        {
            var buffer = new StringBuilder();
            foreach (var group in duplicateGroups)
            {
                foreach (var hostFolder in group)
                    buffer.AppendLine($"- {hostFolder}");
            }

            return ErrorStrings.Error_MappedFolder_DuplicateLeafName + Environment.NewLine +
                Environment.NewLine + buffer.ToString();
        }

        public static string Error_Unknown(string file, string member, int line)
            => string.Format(ErrorStrings.Error_Unknown, file, line, member);

        public static string Error_With_Exception(
            string errorMessage,
            Exception thrownException)
        {
            if (thrownException is AggregateException ae)
                return Error_With_Exception(errorMessage, ae?.InnerException);

            var message = errorMessage;

            if (thrownException != null)
            {
                message = string.Concat(message, Environment.NewLine +
                    Environment.NewLine +
                    string.Format(ErrorStrings.Error_ForYourReference, thrownException.Message));
            }

            return message;
        }
    }

    // 로그 기록용 메시지 (로그를 데이터로 분석하는 경우를 고려하여 이 부분은 번역하지 않습니다.)
    partial class StringResources
    {
        public static string TableCloth_Log_DuplicateMappedFolderLeafName_ProhibitTranslation(string leafName, IEnumerable<string> hostFolders)
            => $"Duplicate mapped folder leaf names detected without explicit SandboxFolder paths. Folders with leaf name `{leafName}`: {string.Join(", ", hostFolders)}";

        public static string TableCloth_Log_WsbFileCreateFail_ProhibitTranslation(string wsbFilePath)
            => string.Format(LogStrings.TableCloth_Log_WsbFileCreateFail_ProhibitTranslation, wsbFilePath);

        public static string TableCloth_Log_CannotParseWsbFile_ProhibitTranslation(string wsbFilePath)
            => string.Format(LogStrings.TableCloth_Log_CannotParseWsbFile_ProhibitTranslation, wsbFilePath);

        public static string TableCloth_Log_HostFolderNotExists_ProhibitTranslation(string hostFolderPath)
            => string.Format(LogStrings.TableCloth_Log_HostFolderNotExists_ProhibitTranslation, hostFolderPath);

        public static string TableCloth_Log_DirectoryEnumFail_ProhibitTranslation(string targetPath, Exception reason)
            => string.Format(LogStrings.TableCloth_Log_DirectoryEnumFail_ProhibitTranslation, targetPath, TableCloth_UnwrapException(reason));

        public static string TableCloth_UnwrapException(Exception failureReason)
        {
            var unwrappedException = failureReason;

            if (failureReason is AggregateException ae)
                unwrappedException = ae.InnerException;

            return unwrappedException?.Message ?? CommonStrings.UnknownText;
        }

        public static string AlternateIfWhitespaceString(string content, string alternateText = default)
            => (string.IsNullOrWhiteSpace(content) ? (string.IsNullOrWhiteSpace(alternateText) ? CommonStrings.UnknownText : alternateText) : content);

        public static string TableCloth_DebugInformation(
            string processName,
            string rawCommandLine,
            string parsedCommandLine)
        {
            return $@"
=================
Debug Information
=================

* Process name: {AlternateIfWhitespaceString(processName)}
* Raw Commandline: {AlternateIfWhitespaceString(rawCommandLine, "(none)")}
* Parsed Commandline: {AlternateIfWhitespaceString(parsedCommandLine, "(none)")}
".TrimStart();
        }
    }
}
