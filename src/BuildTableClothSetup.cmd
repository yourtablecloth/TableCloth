@echo off
pushd "%~dp0"

choice /t 5 /d R /c DRC /m "D: Debug, R: Release, C: Cancel (Default: R)"
if %ERRORLEVEL% EQU 1 (
  "%programfiles(x86)%\NSIS\Bin\makensis.exe" /DBUILD_CONFIG=Debug TableClothSetup.nsi
  goto exit
)
if %ERRORLEVEL% EQU 2 (
  "%programfiles(x86)%\NSIS\Bin\makensis.exe" /DBUILD_CONFIG=Release TableClothSetup.nsi
  goto exit
)

:exit
popd
@echo on