@echo off
pushd "%~dp0"

echo Composing WSBX file.
dotnet run --project ..\Cloth.csproj -- compose --startup-batch-file .\test.cmd --include .\meet-ie-again.ps1 --force true --output .\test.wsbx

echo Run WSBX file.
dotnet run --project ..\Cloth.csproj -- run --map host= %userprofile%\Pictures / host= %userprofile%\Music\ --input .\test.wsbx

echo Remove temporary WSBX file.
if exist .\test.wsbx del .\test.wsbx

:exit
popd
@echo on