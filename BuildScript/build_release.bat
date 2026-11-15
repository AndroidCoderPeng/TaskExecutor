@echo off
setlocal

REM ============================================================
REM  TaskExecutor (WPF / .NET 8) build and package script
REM  Usage: run in CMD. Output: TaskExecutor_Setup.exe on desktop
REM ============================================================

REM ---------------- Configurable parameters ----------------
REM Build config / target platform
set CONFIG=Release
set RID=win-x64
REM true = self-contained (no .NET 8 runtime needed on target); false = framework-dependent
set SELF_CONTAINED=true

REM Inno Setup compiler path
set INNO_SETUP=D:\Program Files (x86)\Inno Setup 6\ISCC.exe

REM Installer output directory (also written into the .iss OutputDir)
set OUTPUT_DIR=%USERPROFILE%\Desktop

REM ---------------- Derived paths, do not edit ----------------
set SCRIPT_DIR=%~dp0
set PROJECT_DIR=%SCRIPT_DIR%..
set CSPROJ=%PROJECT_DIR%\TaskExecutor\TaskExecutor.csproj
set PUBLISH_DIR=%PROJECT_DIR%\publish
set ISS_SCRIPT=%SCRIPT_DIR%TaskExecutor.iss

echo.
echo ===== TaskExecutor build script =====
echo Project   : %CSPROJ%
echo Publish   : %PUBLISH_DIR%
echo Installer : %OUTPUT_DIR%\TaskExecutor_Setup.exe
echo.

REM ---------------- 0. Environment checks ----------------
where dotnet >nul 2>nul
if errorlevel 1 goto :err_dotnet
if not exist "%CSPROJ%" goto :err_csproj
if not exist "%INNO_SETUP%" goto :err_inno
if not exist "%ISS_SCRIPT%" goto :err_iss

REM ---------------- 1. Clean old publish directory ----------------
echo [1/3] Cleaning old publish directory...
if exist "%PUBLISH_DIR%" rd /s /q "%PUBLISH_DIR%"

REM ---------------- 2. dotnet publish ----------------
echo.
echo [2/3] Publishing (%CONFIG% / %RID% / self-contained=%SELF_CONTAINED%)...
dotnet publish "%CSPROJ%" -c %CONFIG% -r %RID% --self-contained %SELF_CONTAINED% -o "%PUBLISH_DIR%"
if errorlevel 1 goto :err_publish
if not exist "%PUBLISH_DIR%\TaskExecutor.exe" goto :err_publish

REM ---------------- 3. Inno Setup packaging ----------------
echo.
echo [3/3] Packaging with Inno Setup...
"%INNO_SETUP%" /DOutputDir="%OUTPUT_DIR%" "%ISS_SCRIPT%"
if errorlevel 1 goto :err_pack

echo.
echo ===== Build succeeded! =====
echo Installer: %OUTPUT_DIR%\TaskExecutor_Setup.exe
echo.

REM Pass "keep" to preserve the publish directory: build_release.bat keep
if /i "%~1"=="keep" (
    echo Publish output kept at: %PUBLISH_DIR%
) else (
    echo Cleaning publish directory...
    if exist "%PUBLISH_DIR%" rd /s /q "%PUBLISH_DIR%"
)

pause
exit /b 0

:err_dotnet
echo Error: dotnet SDK not found in PATH.
goto :fail
:err_csproj
echo Error: project file not found: %CSPROJ%
goto :fail
:err_inno
echo Error: Inno Setup compiler not found, expected at:
echo %INNO_SETUP%
goto :fail
:err_iss
echo Error: Inno Setup script not found: %ISS_SCRIPT%
goto :fail
:err_publish
echo Error: dotnet publish failed!
goto :fail
:err_pack
echo Error: Inno Setup packaging failed!
goto :fail

:fail
echo.
echo ===== Build FAILED =====
pause
exit /b 1
