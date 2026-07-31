@echo off
setlocal
set "OPTIDIAG_DOTNET_EXE=%USERPROFILE%\.dotnet\dotnet.exe"
if not exist "%OPTIDIAG_DOTNET_EXE%" set "OPTIDIAG_DOTNET_EXE=dotnet"

"%OPTIDIAG_DOTNET_EXE%" publish "%~dp0src\OptiDiag.App\OptiDiag.App.csproj" --configuration Release --runtime win-x64 --self-contained true --output "%~dp0artifacts\OptiDiag-win-x64" --maxcpucount:1 || exit /b 1
echo [OptiDiag] Published to "%~dp0artifacts\OptiDiag-win-x64"
