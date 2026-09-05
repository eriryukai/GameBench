@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

set "SHARPMAKE_DIR=%ROOT%\Engine\ThirdParty\Sharpmake"
set "SHARPMAKE_EXE=%SHARPMAKE_DIR%\Sharpmake.Application\bin\Release\net8.0\Sharpmake.Application.exe"
set "MAIN_SCRIPT=%ROOT%\Mortar.sharpmake.cs"
set "RC=0"

if not exist "%SHARPMAKE_EXE%" (
    echo [Mortar] Building Sharpmake...
    where dotnet >nul 2>nul
    if errorlevel 1 (
        echo.
        echo [ERROR] 'dotnet' was not found on PATH.
        echo Sharpmake targets .NET 8 and needs the .NET SDK to build.
        echo Install one of:
        echo   winget install Microsoft.DotNet.SDK.8
        echo   winget install Microsoft.DotNet.SDK.10
        echo or add a .NET SDK component via the Visual Studio Installer.
        echo.
        set "RC=1"
        goto :end
    )

    pushd "%SHARPMAKE_DIR%" >nul
    dotnet build Sharpmake.sln -c Release -nologo -v m
    set "BUILD_RC=!errorlevel!"
    popd >nul
    if not "!BUILD_RC!"=="0" (
        echo [ERROR] Sharpmake build failed.
        set "RC=!BUILD_RC!"
        goto :end
    )
)

echo [Mortar] Generating project files...
set "MAIN_SCRIPT_FWD=%MAIN_SCRIPT:\=/%"
"%SHARPMAKE_EXE%" "/sources('%MAIN_SCRIPT_FWD%')" /verbose
if errorlevel 1 (
    echo [ERROR] Sharpmake generation failed.
    set "RC=1"
    goto :end
)

echo.
echo [Mortar] Done. Open Mortar.sln to build.

:end
echo.
pause
endlocal & exit /b %RC%
