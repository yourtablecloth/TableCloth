@echo off
pushd "%~dp0"

"%programfiles(x86)%\NSIS\Bin\makensis.exe" /DBUILD_CONFIG=Release TableClothSetup.nsi

:exit
popd
@echo on