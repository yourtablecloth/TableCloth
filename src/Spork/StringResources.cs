#nullable enable

using System;

namespace TableCloth.Resources
{
    internal static partial class StringResources { }

    // 공동 인증서 관련 문자열들
    partial class StringResources
    {
        public static readonly TimeSpan Cert_ExpireWindow = TimeSpan.FromDays(-3d);

        public static string Cert_Availability_MayTooEarly(DateTime now, DateTime notBefore)
            => string.Format(UIStringResources.Cert_Availability_MayTooEarly, (int)Math.Truncate((notBefore - now).TotalDays));

        public static string Cert_Availability_ExpireSoon(DateTime now, DateTime notAfter, TimeSpan expireWindow)
            => string.Format(UIStringResources.Cert_Availability_ExpireSoon, (int)Math.Truncate((now - (notAfter - expireWindow)).TotalDays));
    }

    // 오류 메시지에 표시될 문자열들
    partial class StringResources
    {
        public static string Error_Unknown(string file, string member, int line)
            => string.Format(ErrorStrings.Error_Unknown, file, line, member);

        public static string Error_With_Exception(
            string errorMessage,
            Exception? thrownException)
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

    // 호스트 프로그램의 오류 메시지 문자열들
    partial class StringResources
    {
        public static string Error_X509CertError(string certSubject, string errorCode)
            => string.Format(ErrorStrings.Error_X509CertError, certSubject, errorCode);
    }

    // 로그 기록용 메시지 (로그를 데이터로 분석하는 경우를 고려하여 이 부분은 번역하지 않습니다.)
    partial class StringResources
    {
        public static string TableCloth_UnwrapException(Exception? failureReason)
        {
            var unwrappedException = failureReason;

            if (failureReason is AggregateException ae)
                unwrappedException = ae.InnerException;

            return unwrappedException?.Message ?? CommonStrings.UnknownText;
        }

        public static string? AlternateIfWhitespaceString(string? content, string? alternateText = default)
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
