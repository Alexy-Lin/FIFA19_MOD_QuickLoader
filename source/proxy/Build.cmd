@echo off
setlocal
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo Visual Studio Installer / vswhere.exe not found. Install C++ Build Tools and Windows SDK.
    exit /b 1
)
set "VSPATH="
for /f "usebackq tokens=*" %%I in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSPATH=%%I"
if not defined VSPATH (
    echo MSVC x64 tools not found. Install Desktop development with C++ and Windows SDK.
    exit /b 1
)
call "%VSPATH%\VC\Auxiliary\Build\vcvars64.bat"
if errorlevel 1 exit /b 1
pushd "%~dp0"
if not exist "..\..\proxy" mkdir "..\..\proxy"
cl /nologo /utf-8 /O2 /LD /MT /Fo"..\..\proxy\proxy_ags.obj" proxy_ags.c /link /OUT:"..\..\proxy\amd_ags_x64.dll" /IMPLIB:"..\..\proxy\proxy_ags.lib" /MACHINE:X64
set "result=%errorlevel%"
if not "%result%"=="0" (
    popd
    exit /b %result%
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Update-Manifest.ps1"
set "result=%errorlevel%"
popd
exit /b %result%
