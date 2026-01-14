@echo off
dotnet run --file "%~dp0build.cs" -- %*
