@echo off
setlocal
set "OPTIDIAG_DOTNET_EXE=%USERPROFILE%\.dotnet\dotnet.exe"
if not exist "%OPTIDIAG_DOTNET_EXE%" set "OPTIDIAG_DOTNET_EXE=dotnet"
if exist "%USERPROFILE%\.dotnet\shared\Microsoft.NETCore.App" set "DOTNET_ROOT=%USERPROFILE%\.dotnet"

"%OPTIDIAG_DOTNET_EXE%" run --project "%~dp0src\OptiDiag.App\OptiDiag.App.csproj" --configuration Release
