@echo off
rem ==========================================================================
rem  TableCloth - Windows Sandbox boot chain diagnostics  (issue #304)
rem --------------------------------------------------------------------------
rem  WHERE TO RUN : inside the Windows Sandbox guest, after TableCloth has
rem                 launched the sandbox but Spork never appeared.
rem  HOW TO RUN   : put this file into the host folder that TableCloth mounts
rem                 as the Data drive (default: Documents\TableCloth\Data),
rem                 launch the sandbox as usual, then inside the sandbox run
rem                     %USERPROFILE%\Desktop\Data\diagnose_sandbox_boot.cmd
rem  OUTPUT       : tablecloth-diag.txt next to this script. Because the Data
rem                 folder is a read-write mount, the file is visible on the
rem                 host after the sandbox is closed - attach it to the issue.
rem
rem  ASCII only on purpose: the guest console codepage is not guaranteed, and
rem  mojibake in the report is worse than plain English.
rem ==========================================================================
setlocal enabledelayedexpansion
set LOG=%~dp0tablecloth-diag.txt
set APPDIR=C:\Users\WDAGUtilityAccount\Desktop\App

echo ==== TableCloth sandbox boot diagnostics (issue #304) ====> "%LOG%"
echo [00] collected at %DATE% %TIME%>> "%LOG%"
whoami>> "%LOG%" 2>&1
ver>> "%LOG%" 2>&1

rem --------------------------------------------------------------------------
rem  [01] Did StartupScript.cmd (the wsb LogonCommand) actually run?
rem       The two Edge policy values below are written ONLY by StartupScript.cmd,
rem       so their presence proves the logon command executed. This single
rem       answer splits the whole investigation in two.
rem --------------------------------------------------------------------------
echo.>> "%LOG%"
echo [01] did the LogonCommand batch run? (Edge policies are written only by it)>> "%LOG%"
reg query "HKLM\SOFTWARE\Policies\Microsoft\Edge\LocalNetworkAccessAllowedForUrls" /v 1>> "%LOG%" 2>&1
reg query "HKLM\SOFTWARE\Policies\Microsoft\Edge" /v HardwareAccelerationModeEnabled>> "%LOG%" 2>&1

rem --------------------------------------------------------------------------
rem  [02] Smart App Control state. 0 = off, 1 = enforced, 2 = evaluation.
rem       StartupScript.cmd tries to force it to 0 and then runs citool
rem       --refresh; if the value is still 1 or 2 here, that workaround no
rem       longer takes effect on this build.
rem --------------------------------------------------------------------------
echo.>> "%LOG%"
echo [02] Smart App Control state (0=off 1=enforce 2=evaluation)>> "%LOG%"
reg query "HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy" /v VerifiedAndReputablePolicyState>> "%LOG%" 2>&1
powershell -NoProfile -Command "try { 'SmartAppControlState = ' + (Get-MpComputerStatus).SmartAppControlState } catch { 'ERR: ' + $_.Exception.Message }">> "%LOG%" 2>&1
if exist "%SystemRoot%\System32\citool.exe" (echo citool.exe present>> "%LOG%") else (echo citool.exe MISSING>> "%LOG%")

rem --------------------------------------------------------------------------
rem  [03] Is the App mount actually there, with the binary we try to launch?
rem --------------------------------------------------------------------------
echo.>> "%LOG%"
echo [03] App mount contents>> "%LOG%"
if exist "%APPDIR%\TableCloth.exe" (
  dir "%APPDIR%">> "%LOG%" 2>&1
) else (
  echo   %APPDIR%\TableCloth.exe NOT FOUND>> "%LOG%"
  dir "C:\Users\WDAGUtilityAccount\Desktop">> "%LOG%" 2>&1
)

rem --------------------------------------------------------------------------
rem  [04] Launch it by hand and see whether the process survives creation.
rem       A Smart App Control block happens in the kernel at process creation,
rem       so the parent just continues and the task list stays empty.
rem --------------------------------------------------------------------------
echo.>> "%LOG%"
echo [04] launch TableCloth.exe spork and check survival>> "%LOG%"
if exist "%APPDIR%\TableCloth.exe" (
  echo Launching TableCloth.exe - if a "blocked" dialog pops up, screenshot it.
  start "" "%APPDIR%\TableCloth.exe" spork
  echo   start errorlevel=!errorlevel!>> "%LOG%"
  ping -n 21 127.0.0.1 >nul
  echo   --- tasklist after 20s --->> "%LOG%"
  tasklist /fi "imagename eq TableCloth.exe">> "%LOG%" 2>&1
) else (
  echo   SKIP - binary not present>> "%LOG%"
)

rem --------------------------------------------------------------------------
rem  [05] Kernel side evidence of a block.
rem --------------------------------------------------------------------------
echo.>> "%LOG%"
echo [05] CodeIntegrity operational log (most recent 30 events)>> "%LOG%"
wevtutil qe Microsoft-Windows-CodeIntegrity/Operational /c:30 /rd:true /f:text>> "%LOG%" 2>&1

rem --------------------------------------------------------------------------
rem  [06] Windows PowerShell sanity - not used by the boot path since v1.20.0,
rem       but per-site install steps still depend on it, so record it anyway.
rem --------------------------------------------------------------------------
echo.>> "%LOG%"
echo [06] Windows PowerShell availability>> "%LOG%"
if exist "%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" (
  echo   powershell.exe present>> "%LOG%"
  powershell -NoProfile -Command "$PSVersionTable.PSVersion.ToString()">> "%LOG%" 2>&1
  powershell -NoProfile -Command "(Get-ExecutionPolicy -List | Out-String)">> "%LOG%" 2>&1
) else (
  echo   powershell.exe MISSING>> "%LOG%"
)

rem --------------------------------------------------------------------------
rem  [07] Network / DNS - rules out the issue #237 class of failures.
rem --------------------------------------------------------------------------
echo.>> "%LOG%"
echo [07] DNS and catalog reachability>> "%LOG%"
ipconfig /all>> "%LOG%" 2>&1
nslookup yourtablecloth.app>> "%LOG%" 2>&1

echo.>> "%LOG%"
echo ==== done ====>> "%LOG%"

echo.
echo Diagnostics finished.
echo Result file: %LOG%
echo The same file is visible on the host under Documents\TableCloth\Data.
echo.
pause
