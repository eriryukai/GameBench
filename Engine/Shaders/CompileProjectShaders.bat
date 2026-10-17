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
if "%platform%"=="" set "platform=SPIRV"

rem ---------------------------------------------------------------------------
rem Resolve the DXC compiler for the requested platform.
rem
rem The two backends need two *different* DXC builds and this is the whole reason
rem the dxc argument is resolved here instead of at the call site:
rem
rem   DXIL  ("auto")  -> a DXC shipping an adjacent dxil.dll so the bytecode can
rem                      be signed (D3D12 rejects unsigned DXIL). That is the
rem                      Windows SDK's dxc.exe, or the official DXC release.
rem   SPIRV ("vulkan") -> a DXC built with -DENABLE_SPIRV_CODEGEN=ON. The Windows
rem                      SDK build does NOT have it ("SPIR-V CodeGen not
rem                      available"), so the Vulkan SDK's dxc.exe is required.
rem
rem Argument contract for %5:
rem   auto           -> resolve the Windows SDK DXC (DXIL only)
rem   vulkan / empty -> resolve a SPIR-V capable DXC (falls back to VULKAN_SDK)
rem   <path>         -> use that dxc.exe as-is
rem ---------------------------------------------------------------------------
if /i "%platform%"=="DXIL" goto :resolve_dxil
goto :resolve_spirv

:resolve_dxil
if /i "%dxc%"=="auto" set "dxc="
if not defined dxc call :find_windows_dxc
if not defined dxc (
    echo ERROR: Could not locate a DXC that can sign DXIL ^(needs an adjacent dxil.dll^).
    echo        Install the Windows 10/11 SDK, or pass an explicit dxc.exe path.
    goto :failed
)
goto :check_tools

:resolve_spirv
if /i "%dxc%"=="vulkan" set "dxc="
if not defined dxc if defined GAMEBENCH_DXC set "dxc=%GAMEBENCH_DXC%"
rem A quoted "%%VULKAN_SDK%%\Bin\dxc.exe" with VULKAN_SDK unset expands to the
rem nonsense "\Bin\dxc.exe"; the existence check below discards it.
if not defined dxc set "dxc=%VULKAN_SDK%\Bin\dxc.exe"
if not exist "%dxc%" set "dxc="
if not defined dxc call :find_vulkan_dxc

if not defined dxc (
    rem The Windows SDK DXC cannot emit SPIR-V, so without the Vulkan SDK this
    rem pass is impossible. The DXIL pass above still produced shippable blobs and
    rem the runtime currently only loads DXIL, so skip instead of failing the build.
    echo WARNING: No SPIR-V capable DXC ^(Vulkan SDK^) found - skipping the SPIR-V pass.
    echo          DXIL shaders were still compiled and are what the DX12 runtime loads.
    echo          Install the LunarG Vulkan SDK, or set VULKAN_SDK / GAMEBENCH_DXC to a
    echo          dxc.exe built with SPIR-V codegen, to re-enable Vulkan shaders.
    if defined GAMEBENCH_REQUIRE_SPIRV (
        echo ERROR: GAMEBENCH_REQUIRE_SPIRV is set, refusing to skip SPIR-V compilation.
        goto :failed
    )
    goto :done
)
goto :check_tools

:check_tools
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
exit /b 0

rem ---------------------------------------------------------------------------
rem :find_vulkan_dxc
rem Resolves %dxc% to a SPIR-V capable dxc.exe. Only called when neither an
rem explicit path nor %VULKAN_SDK% produced one, so this is purely a best-effort
rem sweep of the LunarG default install locations (newest SDK wins).
rem ---------------------------------------------------------------------------
:find_vulkan_dxc
setlocal enabledelayedexpansion
set "found="

for %%R in ("%ProgramFiles%\VulkanSDK" "%ProgramFiles(x86)%\VulkanSDK" "%SystemDrive%\VulkanSDK") do (
    if not defined found if exist "%%~R" (
        for /f "delims=" %%D in ('dir /b /ad /o-n "%%~R" 2^>nul') do (
            if not defined found if exist "%%~R\%%D\Bin\dxc.exe" set "found=%%~R\%%D\Bin\dxc.exe"
        )
    )
)

endlocal & set "dxc=%found%"
exit /b 0
