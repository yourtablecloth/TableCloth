@echo off
pushd "%~dp0"

dotnet publish -r win-x64 -p:Configuration=Release -p:PublishSingleFile=true -p:PublishReadyToRun=true --self-contained true
powershell.exe Compress-Archive -Path '.\bin\Release\net5.0-windows10.0.17763.0\win-x64\publish\*' -DestinationPath '.\bin\Release\Host.zip' -CompressionLevel Optimal -Force

:exit
popd
@echo on