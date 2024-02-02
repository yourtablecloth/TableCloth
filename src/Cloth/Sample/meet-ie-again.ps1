Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process -Force;

Add-Type 'public class SFW { [System.Runtime.InteropServices.DllImport("user32.dll")][return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] public static extern System.Boolean SetForegroundWindow(System.IntPtr hWnd); }';

$handle = [activator]::CreateInstance([type]::GetTypeFromCLSID("0002DF01-0000-0000-C000-000000000046"));
$handle.Visible = $true;
$handle.Navigate('http://www.example.com/');
[SFW]::SetForegroundWindow($handle.HWND);
