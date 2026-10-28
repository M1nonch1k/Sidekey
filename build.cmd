@echo off
rem Builds Sidekey.exe with the C# compiler that ships with Windows (.NET Framework 4.8).
rem Translations from lang\*.json are embedded into the .exe.
setlocal EnableDelayedExpansion
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo C# compiler not found: .NET Framework 4.8 is required.
  exit /b 1
)
if not exist app.ico powershell -NoProfile -ExecutionPolicy Bypass -File "tools\make-icon.ps1"
set "RES="
for %%f in (lang\*.json) do set "RES=!RES! /resource:lang\%%~nxf,Sidekey.lang.%%~nxf"
"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /platform:anycpu /out:Sidekey.exe /win32icon:app.ico ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll ^
  !RES! src\*.cs
if errorlevel 1 (
  echo BUILD FAILED
  exit /b 1
)
echo Built: %~dp0Sidekey.exe
