@echo off
setlocal
set "OPTIDIAG_DOTNET_EXE=%USERPROFILE%\.dotnet\dotnet.exe"
if not exist "%OPTIDIAG_DOTNET_EXE%" set "OPTIDIAG_DOTNET_EXE=dotnet"

echo [OptiDiag] Using:
"%OPTIDIAG_DOTNET_EXE%" --version || exit /b 1
"%OPTIDIAG_DOTNET_EXE%" restore "%~dp0OptiDiag.sln" || exit /b 1
"%OPTIDIAG_DOTNET_EXE%" build "%~dp0OptiDiag.sln" --configuration Release --no-restore --maxcpucount:1 || exit /b 1
"%OPTIDIAG_DOTNET_EXE%" test "%~dp0OptiDiag.sln" --configuration Release --no-build --no-restore --maxcpucount:1 || exit /b 1
echo [OptiDiag] Build and tests passed.
