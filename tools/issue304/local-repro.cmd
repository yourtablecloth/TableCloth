@echo off
rem ==========================================================================
rem  TableCloth issue #304 - LOCAL REPRO probe (maintainer side)
rem --------------------------------------------------------------------------
rem  This is NOT the script we hand to reporters. See tools/diagnose_sandbox_boot.cmd
rem  for that one; it runs inside a real TableCloth session.
rem
rem  This one runs in a bare Windows Sandbox launched from local-repro.wsb, with
rem  no TableCloth involvement, and answers three questions:
rem    - does a wsb LogonCommand run at all on this build?             -> [00]
rem    - what is the guest's Smart App Control state out of the box?   -> [01]
rem    - does the reg+citool workaround still turn it off?             -> [02][03]
rem    - does an unsigned .NET single-file exe survive process creation
rem      when launched from a mapped folder?                           -> [04]
rem
rem  Result lands in result.txt next to this script (mapped read-write), so the
rem  host can read it after the sandbox is gone.
rem
rem  ASCII only on purpose: the guest console codepage is not guaranteed.
rem ==========================================================================
setlocal enabledelayedexpansion
set LOG=%~dp0result.txt
set TCDIR=C:\Users\WDAGUtilityAccount\Desktop\current

echo ==== TableCloth #304 sandbox diagnostics ====> "%LOG%"
echo [00] LogonCommand DID run. %DATE% %TIME%>> "%LOG%"
whoami>> "%LOG%" 2>&1
ver>> "%LOG%" 2>&1

echo.>> "%LOG%"
echo [01] SAC policy state BEFORE workaround>> "%LOG%"
reg query "HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy" /v VerifiedAndReputablePolicyState>> "%LOG%" 2>&1
echo [01b] Defender SmartAppControlState (0=off 1=on 2=eval)>> "%LOG%"
powershell -NoProfile -Command "try { (Get-MpComputerStatus).SmartAppControlState } catch { 'ERR: ' + $_.Exception.Message }">> "%LOG%" 2>&1

echo.>> "%LOG%"
echo [02] apply StartupScript.cmd workaround (reg + citool)>> "%LOG%"
reg add "HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy" /v VerifiedAndReputablePolicyState /t REG_DWORD /d 0 /f>> "%LOG%" 2>&1
echo   reg add errorlevel=!errorlevel!>> "%LOG%"
if exist "%SystemRoot%\System32\citool.exe" (
  "%SystemRoot%\System32\citool.exe" --refresh>> "%LOG%" 2>&1
  echo   citool errorlevel=!errorlevel!>> "%LOG%"
) else (
  echo   citool.exe NOT FOUND in System32>> "%LOG%"
)

echo.>> "%LOG%"
echo [03] SAC policy state AFTER workaround>> "%LOG%"
reg query "HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy" /v VerifiedAndReputablePolicyState>> "%LOG%" 2>&1
powershell -NoProfile -Command "try { (Get-MpComputerStatus).SmartAppControlState } catch { 'ERR: ' + $_.Exception.Message }">> "%LOG%" 2>&1

echo.>> "%LOG%"
echo [04] launch the UNSIGNED TableCloth.exe from a mapped folder>> "%LOG%"
if not exist "%TCDIR%\TableCloth.exe" (
  echo   SKIP - %TCDIR%\TableCloth.exe not mounted>> "%LOG%"
) else (
  echo Launching TableCloth.exe - if a "blocked" dialog pops up, screenshot it.
  start "" "%TCDIR%\TableCloth.exe" spork
  echo   start errorlevel=!errorlevel!>> "%LOG%"
  ping -n 16 127.0.0.1 >nul
  echo   --- tasklist after 15s --->> "%LOG%"
  tasklist /fi "imagename eq TableCloth.exe">> "%LOG%" 2>&1
)

echo.>> "%LOG%"
echo [05] CodeIntegrity operational log (most recent 30)>> "%LOG%"
wevtutil qe Microsoft-Windows-CodeIntegrity/Operational /c:30 /rd:true /f:text>> "%LOG%" 2>&1

echo.>> "%LOG%"
echo [06] AppLocker EXE and DLL log (most recent 20)>> "%LOG%"
wevtutil qe "Microsoft-Windows-AppLocker/EXE and DLL" /c:20 /rd:true /f:text>> "%LOG%" 2>&1

echo.>> "%LOG%"
echo [07] powershell availability (old hypothesis check)>> "%LOG%"
if exist "%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" (
  echo   powershell.exe present>> "%LOG%"
  powershell -NoProfile -Command "$PSVersionTable.PSVersion.ToString()">> "%LOG%" 2>&1
  powershell -NoProfile -Command "(Get-ExecutionPolicy -List | Out-String)">> "%LOG%" 2>&1
) else (
  echo   powershell.exe MISSING>> "%LOG%"
)

echo.>> "%LOG%"
echo ==== done ====>> "%LOG%"
echo Diagnostics finished. Result: %LOG%
echo (This window stays open on purpose.)
cmd /k
