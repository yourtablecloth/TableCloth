using System;
using System.Collections.Generic;
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
    }

    // 오류 메시지에 표시될 문자열들
    partial class StringResources
    {
        public static string Error_Unknown(string file, string member, int line)
            => string.Format(ErrorStrings.Error_Unknown, file, line, member);

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

    // 호스트 프로그램의 오류 메시지 문자열들
    partial class StringResources
    {
        public static string Error_X509CertError(string certSubject, string errorCode)
            => string.Format(ErrorStrings.Error_X509CertError, certSubject, errorCode);
    }

    // 호스트 프로그램에서 사용할 스위치
    partial class StringResources
    {
        public static string TableCloth_TableCloth_Switches_Help => $@"ServiceID ServiceID ... {SwitchStrings.TableCloth_Switch_Option_Arg}

{string.Format(SwitchStrings.TableCloth_Switch_ServiceID_Help, ConstantStrings.CatalogUrl)}

{SwitchStrings.TableCloth_Switch_OptionList_Begin}

{ConstantStrings.TableCloth_Switch_EnableMicrophone}
  {SwitchStrings.TableCloth_Switch_EnableMicrophone_Help}

{ConstantStrings.TableCloth_Switch_EnableCamera}
  {SwitchStrings.TableCloth_Switch_EnableCamera_Help}

{ConstantStrings.TableCloth_Switch_EnablePrinter}
  {SwitchStrings.TableCloth_Switch_EnablePrinter_Help}

{ConstantStrings.TableCloth_Switch_EnableCert}
  {SwitchStrings.TableCloth_Switch_EnableCert_Help}

{ConstantStrings.TableCloth_Switch_CertPublicKey} {SwitchStrings.TableCloth_Switch_PublicKeyFilePath_Arg}
  {SwitchStrings.TableCloth_Switch_CertPublicKey_Help}

{ConstantStrings.TableCloth_Switch_CertPrivateKey} {SwitchStrings.TableCloth_Switch_PrivateKeyFilePath_Arg}
  {SwitchStrings.TableCloth_Switch_CertPrivateKey_Help}

{ConstantStrings.TableCloth_Switch_InstallEveryonesPrinter}
  {SwitchStrings.TableCloth_Switch_InstallEveryonesPrinter_Help}

{ConstantStrings.TableCloth_Switch_InstallAdobeReader}
  {SwitchStrings.TableCloth_Switch_InstallAdobeReader_Help}

{ConstantStrings.TableCloth_Switch_InstallHancomOfficeViewer}
  {SwitchStrings.TableCloth_Switch_InstallHancomOfficeViewer_Help}

{ConstantStrings.TableCloth_Switch_InstallRaiDrive}
  {SwitchStrings.TableCloth_Switch_InstallRaiDrive_Help}

{ConstantStrings.TableCloth_Switch_EnableIEMode}
  {SwitchStrings.TableCloth_Switch_EnableIEMode_Help}

{ConstantStrings.TableCloth_Switch_Help}
  {SwitchStrings.TableCloth_Switch_Help_Help}
";

        public static string TableCloth_Spork_Switches_Help => $@"ServiceID ServiceID ... {SwitchStrings.TableCloth_Switch_Option_Arg}

{string.Format(SwitchStrings.TableCloth_Switch_ServiceID_Help, ConstantStrings.CatalogUrl)}

{SwitchStrings.TableCloth_Switch_OptionList_Begin}

{ConstantStrings.TableCloth_Switch_InstallEveryonesPrinter}
  {SwitchStrings.TableCloth_Switch_InstallEveryonesPrinter_Help}

{ConstantStrings.TableCloth_Switch_InstallAdobeReader}
  {SwitchStrings.TableCloth_Switch_InstallAdobeReader_Help}

{ConstantStrings.TableCloth_Switch_InstallHancomOfficeViewer}
  {SwitchStrings.TableCloth_Switch_InstallHancomOfficeViewer_Help}

{ConstantStrings.TableCloth_Switch_InstallRaiDrive}
  {SwitchStrings.TableCloth_Switch_InstallRaiDrive_Help}

{ConstantStrings.TableCloth_Switch_EnableIEMode}
  {SwitchStrings.TableCloth_Switch_EnableIEMode_Help}

{ConstantStrings.TableCloth_Switch_DryRun}
  {SwitchStrings.TableCloth_Switch_DryRun_Help}

{ConstantStrings.TableCloth_Switch_Help}
  {SwitchStrings.TableCloth_Switch_Help_Help}
";
    }

    // 로그 기록용 메시지 (로그를 데이터로 분석하는 경우를 고려하여 이 부분은 번역하지 않습니다.)
    partial class StringResources
    {
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
