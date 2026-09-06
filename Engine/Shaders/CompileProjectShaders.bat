@echo off
setlocal

set "output_dir=%~2"
set "configuration=%~3"
set "shadermake=%~4"
set "dxc=%~5"
set "platform=%~6"
set "shader_dir=%~dp0"
rem Include dir without the trailing backslash: a quoted path ending in "\" would
rem escape the closing quote ("...\Shaders\" -> broken arg parsing).
set "shader_dir_inc=%~dp0."

if "%output_dir%"=="" set "output_dir=%~dp0..\Binaries\Shaders"
if "%configuration%"=="" set "configuration=Debug"
if "%shadermake%"=="" set "shadermake=%~dp0..\ThirdParty\ShaderMake\Binaries\win64_%configuration%\ShaderMake.exe"
if "%dxc%"=="" set "dxc=%VULKAN_SDK%\Bin\dxc.exe"
if "%platform%"=="" set "platform=SPIRV"

rem "auto" asks us to locate the Windows SDK's dxc.exe. Only that DXC ships the
rem adjacent dxil.dll needed to sign DXIL (the Vulkan SDK's dxc.exe does not), so
rem it is required for the DX12 backend. Scan the Windows Kits bin folders and pick
rem the newest x64 dxc.exe.
if /i "%dxc%"=="auto" call :find_windows_dxc

if not exist "%shadermake%" (
    echo ERROR: ShaderMake was not found at "%shadermake%".
    goto :failed
)
if not exist "%dxc%" (
    echo ERROR: DXC was not found at "%dxc%".
    goto :failed
)

rem Quote --compiler: the Windows SDK dxc.exe lives under "C:\Program Files (x86)\..."
rem which contains spaces (the Vulkan SDK path does not, which is why this only bit DXIL).
set "common=--config "%shader_dir%Shaders.cfg" --binary --compiler "%dxc%" --shaderModel 6_6 --hlsl2021 --WX -I "%shader_dir_inc%""
set "build_options=--optimization 3 --stripReflection"
if /i "%configuration%"=="Debug" set "build_options=--optimization 0 --embedPDB"

if /i "%platform%"=="DXIL" goto :dxil

:spirv
rem SPIR-V for the Vulkan backend. Register shifts + dx memory layout match
rem BindingHelpers.hlsli; TARGET_VULKAN/SPIRV switch the shaders to the Vulkan path.
"%shadermake%" %common% %build_options% --platform SPIRV --out "%output_dir%\SPIRV" --outputExt .spirv -D SPIRV -D TARGET_VULKAN --vulkanVersion 1.3 --vulkanMemoryLayout dx --tRegShift 0 --sRegShift 128 --bRegShift 256 --uRegShift 384
if errorlevel 1 goto :failed
goto :done

:dxil
rem DXIL for the DX12 backend. No Vulkan-only options; DXC signs the bytecode via
rem the adjacent dxil.dll (that is why the Windows SDK dxc.exe is used, not the
rem Vulkan SDK's, which does not ship dxil.dll). D3D12 rejects unsigned DXIL.
"%shadermake%" %common% %build_options% --platform DXIL --out "%output_dir%\DXIL" --outputExt .dxil -D TARGET_D3D12
if errorlevel 1 goto :failed
goto :done

:done
if /i not "%~1"=="nopause" pause
exit /b 0

:failed
echo Shader compilation failed.
if /i not "%~1"=="nopause" pause
exit /b 1

rem ---------------------------------------------------------------------------
rem :find_windows_dxc
rem Resolves %dxc% to the newest Windows SDK dxc.exe (the one shipping dxil.dll).
rem Checks WindowsSdkVerBinPath / WindowsSdkDir first, then scans the default
rem Windows Kits\10\bin install for the highest-versioned x64\dxc.exe.
rem ---------------------------------------------------------------------------
:find_windows_dxc
setlocal enabledelayedexpansion
set "found="

rem 1. MSBuild-provided versioned bin path, if present in the environment.
if defined WindowsSdkVerBinPath if exist "%WindowsSdkVerBinPath%x64\dxc.exe" set "found=%WindowsSdkVerBinPath%x64\dxc.exe"

rem 2. WindowsSdkDir\bin\<version>\x64\dxc.exe (newest version wins).
if not defined found if defined WindowsSdkDir (
    for /f "delims=" %%D in ('dir /b /ad /o-n "%WindowsSdkDir%bin\10.*" 2^>nul') do (
        if not defined found if exist "%WindowsSdkDir%bin\%%D\x64\dxc.exe" set "found=%WindowsSdkDir%bin\%%D\x64\dxc.exe"
    )
)

rem 3. Default install roots (Program Files (x86) then Program Files).
if not defined found (
    for %%R in ("%ProgramFiles(x86)%\Windows Kits\10\bin" "%ProgramFiles%\Windows Kits\10\bin") do (
        if not defined found if exist "%%~R" (
            for /f "delims=" %%D in ('dir /b /ad /o-n "%%~R\10.*" 2^>nul') do (
                if not defined found if exist "%%~R\%%D\x64\dxc.exe" set "found=%%~R\%%D\x64\dxc.exe"
            )
        )
    )
)

endlocal & set "dxc=%found%"
if not defined dxc (
    echo ERROR: Could not locate a Windows SDK dxc.exe ^(with dxil.dll^) for DXIL compilation.
    echo        Install the Windows 10/11 SDK, or pass an explicit dxc.exe path.
)
exit /b 0
