<Query Kind="Expression">
  <Namespace>System.Net</Namespace>
</Query>

string.Join(Environment.NewLine,
@"
이곳에 자동으로 분류하려는 설치 프로그램의 주소를 여러 줄로 넣어주세요.
URL로 인식되는 주소만 처리되고 나머지는 무시됩니다.
"
.Split(new char[] { '\r', '\n', }, StringSplitOptions.RemoveEmptyEntries)
.Where(x => Uri.TryCreate(x, UriKind.Absolute, out _))
.Select(x =>
{
	var url = new Uri(x.Trim(), UriKind.Absolute);
	var fileName = Path.GetFileName(url.LocalPath);
	var componentName = Path.GetFileNameWithoutExtension(url.LocalPath);

	var urlAttrValue = WebUtility.HtmlEncode(url.AbsoluteUri);

	switch (fileName)
	{
		case "INIS_EX.exe":
			return $@"<Package Name=""IniSafeCrossWeb"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
		case "KSCertRelay.exe":
			return $@"<Package Name=""KSCertRelay"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
		case "KSCertRelay_nx_Installer_32bit.exe":
			return $@"<Package Name=""KSCertRelay32"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
		case "KSCertRelay_nx_Installer_64bit.exe":
			return $@"<Package Name=""KSCertRelay64"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
		case "TouchEnKey_Installer.exe":
			return $@"<Package Name=""TouchEnKey"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
		case "TEFW_Installer.exe":
			return $@"<Package Name=""TouchEnFirewall"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
		case "nxkey_x86.exe":
		case "TouchEn_nxKey_32bit.exe":
		case "TouchEn_nxKey_Installer_32bit.exe":
			return $@"<Package Name=""TouchEnKey32"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
		case "nxkey_x64.exe":
		case "TouchEn_nxKey_64bit.exe":
		case "TouchEn_nxKey_Installer_64bit.exe":
			return $@"<Package Name=""TouchEnKey64"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
		case "nos_setup.exe":
		case "nos_launcher.exe":
			return $@"<Package Name=""NOS"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
		case "UniquePC_Setup.exe":
			return $@"<Package Name=""UniquePC"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
		case "CertShare_Installer.exe":
			return $@"<Package Name=""CertShare"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
		case "xw_install.exe":
			return $@"<Package Name=""XecureWeb"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
		case "WebCryptX.exe":
			return $@"<Package Name=""WebCryptX"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
		case "SmartManagerEX.exe":
			return $@"<Package Name=""SmartManagerEx"" Url=""{urlAttrValue}"" />";
		case "CX60_Plugin_u_setup.exe":
			return $@"<Package Name=""CX60"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
		case "innogmp_win.exe":
			return $@"<Package Name=""InnoGmp"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
		case "KCaseAgent_Installer.exe":
			return $@"<Package Name=""KCaseAgent"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
		case "KISSCAP.exe":
			return $@"<Package Name=""KISSCAP"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
		default:
			if (fileName.Contains("astx_setup", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""AhnLabSafeTx"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("astxdn", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""ASTX"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("veraport-g3-x64", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""Veraport"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("I3GSvcManager", StringComparison.OrdinalIgnoreCase) ||
				fileName.Contains("IPInsideLWS", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""IPInside"" Url=""{urlAttrValue}"" />";
			if (fileName.Contains("TDClientforWindowsAgent", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""TDClientAgent"" Url=""{urlAttrValue}"" />";
			if (fileName.Contains("SKCertServiceSetup", StringComparison.OrdinalIgnoreCase) ||
				fileName.Contains("sk_ct", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""SignKoreaCert"" Url=""{urlAttrValue}"" />";
			if (fileName.Contains("AnySign", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""AnySign"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("UniSign", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""UniSign"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("RealIP", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""RealIp"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("RealIP", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""RealIp"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("Delfino", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""WizInDelfino"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("INIS_EX", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""INISAFECrossWeb"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("INISafeMail", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""INISafeMail"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("KOS_Setup", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""KOS"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("ePageSafer", StringComparison.OrdinalIgnoreCase) ||
				fileName.Contains("MAWS", StringComparison.OrdinalIgnoreCase) ||
				fileName.Contains("madownload", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""ePageSafer"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("NX_PRNMAN", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""PrintManager"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("VestCert", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""VestCert"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("KSBiz", StringComparison.OrdinalIgnoreCase) ||
				fileName.Contains("keysharp", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""KeySharpBiz"" Url=""{urlAttrValue}"" />";
			if (fileName.Contains("MoaSign", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""MoaSign"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("iSAS", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""iSASService"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("KSignCASE", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""KSignCASE"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("Dext", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""DextUploader"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("UbiViewer", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""UbiViewer"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("CrossWarp", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""CrossWarp"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			if (fileName.Contains("MagicLine", StringComparison.OrdinalIgnoreCase))
				return $@"<Package Name=""MagicLine"" Url=""{urlAttrValue}"" Arguments=""/silent"" />";
			return $@"<Package Name=""{WebUtility.HtmlEncode(componentName)}"" Url=""{urlAttrValue}"" />";
	}
})
)