@echo off
pushd "%~dp0"

setlocal enabledelayedexpansion
for /f "usebackq tokens=*" %%i in (`vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
  set MSBUILDPATH="%%i"
)
setlocal DisableDelayedExpansion

echo %MSBUILDPATH%
%MSBUILDPATH% --version

for /f "delims=" %%i in ('git rev-parse HEAD') do set GIT_COMMIT=%%i
echo #define GitCommit "%GIT_COMMIT%" > commit.iss

nuget restore TableCloth.sln

set PlatformList=x64
set ConfigurationList=Debug,Release

for %%p in (x64) do (
  for %%c in (Debug,Release) do (
    echo Building %%c - %%p build...

    %MSBUILDPATH% TableCloth.sln /t:Restore /p:Configuration=%%c /m
    %MSBUILDPATH% Spork\Spork.csproj /p:Configuration=%%c /p:Platform=%%p /m

    dotnet test TableCloth.Test\TableCloth.Test.csproj -r win-%%p -c:%%c
    dotnet publish TableCloth\TableCloth.csproj -r win-%%p --self-contained -p:PublishSingleFile=true -p:PublishReadyToRun=true -c:%%c

    "%programfiles(x86)%\Inno Setup 6\ISCC.exe" /DConfiguration=%%c /DArchitecture=%%p TableClothSetup.iss
  )
)

if exist Output start Output

:exit
pause
popd
@echo on
