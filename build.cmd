@echo off
rem builds the exe with the csc that ships with .NET Framework, no SDK needed
setlocal enabledelayedexpansion
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo csc not found: %CSC%
    exit /b 1
)

rem every lang\*.ini gets embedded as resource "lang.<file>"
set RES=
for %%f in ("%~dp0lang\*.ini") do set RES=!RES! /resource:"%%~ff",lang.%%~nxf

rem x86 because Keyboard_Core.dll is 32 bit
rem codepage 65001: sources are UTF-8
"%CSC%" /nologo /target:winexe /platform:x86 /optimize+ /codepage:65001 ^
    /out:"%~dp0ThinkPadKbdLight.exe" ^
    /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
    !RES! "%~dp0src\*.cs"

if errorlevel 1 (
    echo build failed
    exit /b 1
)
echo OK: %~dp0ThinkPadKbdLight.exe
