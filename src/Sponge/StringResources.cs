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
}
