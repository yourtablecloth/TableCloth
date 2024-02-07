@echo off
pushd "%~dp0"

for /f "delims=" %%i in ('git rev-parse HEAD') do set GIT_COMMIT=%%i
echo #define GitCommit "%GIT_COMMIT%" > commit.iss

set Configuration=Debug
dotnet publish ..\TableCloth\TableCloth.csproj -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishReadyToRun=true -c:%Configuration%
"%programfiles(x86)%\Inno Setup 6\ISCC.exe" /DConfiguration=%Configuration% /DArchitecture=x64 TableClothSetup.iss

set Configuration=Release
dotnet publish ..\TableCloth\TableCloth.csproj -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishReadyToRun=true -c:%Configuration%
"%programfiles(x86)%\Inno Setup 6\ISCC.exe" /DConfiguration=%Configuration% /DArchitecture=x64 TableClothSetup.iss

:exit
popd
@echo on
