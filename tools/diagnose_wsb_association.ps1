using namespace Microsoft.Win32;
using namespace System.Security.AccessControl;

cls

$RawJson = '[{"BaseKey":"HKCR","KeyPath":".wsb","ValueTests":[]},{"BaseKey":"HKCR","KeyPath":"Applications\\WindowsSandbox.exe","ValueTests":[{"ExpectedType":"String","ValueKey":"","ExpectedValue":"Windows Sandbox"},{"ExpectedType":"String","ValueKey":"FriendlyAppName","ExpectedValue":"@%SystemRoot%\\System32\\WindowsSandbox.exe,-97"}]},{"BaseKey":"HKCR","KeyPath":"Windows.Sandbox","ValueTests":[{"ExpectedType":"DWord","ValueKey":"EditFlags","ExpectedValue":131072}]},{"BaseKey":"HKCR","KeyPath":"Windows.Sandbox\\shell\\open\\command","ValueTests":[{"ExpectedType":"ExpandString","ValueKey":"","ExpectedValue":"%SystemRoot%\\System32\\WindowsSandbox.exe \"%1\""}]},{"BaseKey":"HKCU","KeyPath":"Software\\Microsoft\\Windows\\CurrentVersion\\ApplicationAssociationToasts","ValueTests":[{"ExpectedType":"DWord","ValueKey":"Windows.Sandbox_.wsb","ExpectedValue":0}]},{"BaseKey":"HKCU","KeyPath":"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\.wsb\\OpenWithList","ValueTests":[{"ExpectedType":"String","ValueKey":"a","ExpectedValue":"WindowsSandbox.exe"},{"ExpectedType":"String","ValueKey":"MRUList","ExpectedValue":"a"}]},{"BaseKey":"HKCU","KeyPath":"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\.wsb\\UserChoice","ValueTests":[{"ExpectedType":"String","ValueKey":"ProgId","ExpectedValue":"Windows.Sandbox"}]},{"BaseKey":"HKLM","KeyPath":"SOFTWARE\\Classes\\Applications\\WindowsSandbox.exe","ValueTests":[{"ExpectedType":"String","ValueKey":"","ExpectedValue":"Windows Sandbox"},{"ExpectedType":"String","ValueKey":"FriendlyAppName","ExpectedValue":"@%SystemRoot%\\System32\\WindowsSandbox.exe,-97"}]},{"BaseKey":"HKLM","KeyPath":"SOFTWARE\\Classes\\Windows.Sandbox","ValueTests":[{"ExpectedType":"DWord","ValueKey":"EditFlags","ExpectedValue":131072}]},{"BaseKey":"HKLM","KeyPath":"SOFTWARE\\Classes\\Windows.Sandbox\\shell\\open\\command","ValueTests":[{"ExpectedType":"ExpandString","ValueKey":"","ExpectedValue":"%SystemRoot%\\System32\\WindowsSandbox.exe \"%1\""}]},{"BaseKey":"HKLM","KeyPath":"SOFTWARE\\Microsoft\\Windows Sandbox\\Capabilities","ValueTests":[{"ExpectedType":"ExpandString","ValueKey":"ApplicationDescription","ExpectedValue":"@%SystemRoot%\\System32\\WindowsSandbox.exe,-98"},{"ExpectedType":"ExpandString","ValueKey":"ApplicationName","ExpectedValue":"@%SystemRoot%\\System32\\WindowsSandbox.exe,-97"}]},{"BaseKey":"HKLM","KeyPath":"SOFTWARE\\Microsoft\\Windows Sandbox\\Capabilities\\FileAssociations","ValueTests":[{"ExpectedType":"String","ValueKey":".wsb","ExpectedValue":"Windows.Sandbox"}]}]'
$KeyTests = ConvertFrom-Json $RawJson

foreach ($EachKey in $KeyTests) {
    [RegistryKey] $Key = $null;
    try {
        $BaseKey = switch ($EachKey.BaseKey) {
            "HKCR" { [Registry]::ClassesRoot; }
            "HKCC" { [Registry]::CurrentConfig; }
            "HKCU" { [Registry]::CurrentUser; }
            "HKLM" { [Registry]::LocalMachine; }
            "HKU" { [Registry]::Users; }
            default { continue; }
        }
        $Key = $BaseKey.OpenSubKey(
            $EachKey.KeyPath,
            [RegistryKeyPermissionCheck]::ReadSubTree,
            [RegistryRights]::ReadKey);

        foreach ($EachValue in $EachKey.ValueTests) {
            Write-Host "Testing $($EachKey.BaseKey.Name)\$($EachKey.KeyPath)\$($EachValue.ValueKey)";
            $FoundValueType = $Key.GetValueKind($EachValue.ValueKey);
            if ($FoundValueType -ne [RegistryValueKind] $EachValue.ExpectedType) {
                Write-Host "- Different Value Type (Expected: $($EachValue.ExpectedType), Actual: $($FoundValueType))";
            }
            $FoundValue = $Key.GetValue($EachValue.ValueKey, $null, [RegistryValueOptions]::DoNotExpandEnvironmentNames);
            if ($FoundValue -ne $EachValue.ExpectedValue) {
                Write-Host "- Different Value (Expected: $($EachValue.ExpectedValue), Actual: $($FoundValue))";
            }
            Write-Host "- Test passed.";
        }
    } catch {
        Write-Error "- Test Failed! $($_.Exception.Message)";
    } finally {
        if ($Key -ne $null) {
            $Key.Close()
        }
    }
}
