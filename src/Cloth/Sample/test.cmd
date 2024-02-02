@echo off
pushd "%~dp0"

powershell -Command "Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope LocalMachine -Force"

winver
cmd /c powershell .\meet-ie-again.ps1

:exit
popd
@echo on