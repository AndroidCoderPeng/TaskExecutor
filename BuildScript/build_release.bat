@echo off
setlocal

echo.
echo Set variables environment.
echo.
set QT_DIR=D:\3rdparty\Qt5.15\5.15.2\mingw81_64
set OPEN_SSL_DIR=C:\Program Files\OpenSSL-Win64
set QMAKE=%QT_DIR%\bin\qmake.exe
set WINDEPLOYQT=%QT_DIR%\bin\windeployqt.exe
set PLUGIN_DIR=%QT_DIR%\plugins

set PROJECT_DIR=D:\Code\QtProjects\TaskDispatcher
set HTML_DIR=%PROJECT_DIR%\html
set ADB_DIR=%PROJECT_DIR%\tool
set BUILD_DIR=%PROJECT_DIR%\build\Desktop_Qt_5_15_2_MinGW_64_bit_Release\release
set OUTPUT_EXE=TaskDispatcher.exe

set DESKTOP_DIR=C:\Users\Administrator\Desktop
set TEMP_DIR=%DESKTOP_DIR%\temp_build
set INNO_SETUP=D:\Program Files (x86)\Inno Setup 6\ISCC.exe
set ISS_SCRIPT=%PROJECT_DIR%\script\windows_x86_64\TaskDispatcher.iss
set ICON_PATH=%PROJECT_DIR%\application.ico


echo.
echo Change directory to desktop.
echo.
cd /d "%DESKTOP_DIR%"


echo.
echo Remove old temp_build directory if it existed.
echo.
if exist "%TEMP_DIR%" rd /s /q "%TEMP_DIR%"


echo.
echo Create new temporary build directory.
echo.
mkdir "%TEMP_DIR%"


echo.
echo Copy application executable to temporary directory.
echo.
copy "%BUILD_DIR%\%OUTPUT_EXE%" "%TEMP_DIR%\"


echo.
echo Change working directory to temporary build folder.
echo.
cd /d "%TEMP_DIR%"


echo.
echo Copy ADB environment to temporary directory.
echo.
xcopy "%ADB_DIR%\*" "%TEMP_DIR%\tool\" /E /I /Y

echo.
echo Copy HTML directory to temporary directory.
echo.
xcopy "%HTML_DIR%\*" "%TEMP_DIR%\html\" /E /I /Y

echo.
echo Deploy Qt dependencies using windeployqt.
echo.
"%WINDEPLOYQT%" "%TEMP_DIR%\%OUTPUT_EXE%"


echo.
echo Copy MinGW runtime libraries to temporary directory.
echo.
copy "%QT_DIR%\bin\libgcc_s_seh-1.dll" "%TEMP_DIR%\"
copy "%QT_DIR%\bin\libstdc++-6.dll" "%TEMP_DIR%\"
copy "%QT_DIR%\bin\libwinpthread-1.dll" "%TEMP_DIR%\"
copy "%QT_DIR%\bin\D3Dcompiler_47.dll" "%TEMP_DIR%\"
copy "%QT_DIR%\bin\libEGL.dll" "%TEMP_DIR%\"
copy "%QT_DIR%\bin\libGLESv2.dll" "%TEMP_DIR%\"
copy "%QT_DIR%\bin\opengl32sw.dll" "%TEMP_DIR%\"
copy "%OPEN_SSL_DIR%\bin\libcrypto-1_1-x64.dll" "%TEMP_DIR%\"
copy "%OPEN_SSL_DIR%\bin\libssl-1_1-x64.dll" "%TEMP_DIR%\"


echo.
echo Packaging with Inno Setup...
echo %INNO_SETUP%
echo %ISS_SCRIPT%
echo.
if not exist "%ISS_SCRIPT%" (
    echo Error: Inno Setup script not found at %ISS_SCRIPT%
    pause
)
"%INNO_SETUP%" "%ISS_SCRIPT%"

if errorlevel 1 (
    echo Error: Inno Setup packaging failed!
    pause
)

echo.
echo Packaging completed successfully! Installer generated on the desktop.
echo.

echo.
echo Deleting temporary build directory...
echo.

REM 切换到桌面目录，释放对 TEMP_DIR 的占用
cd /d "%DESKTOP_DIR%"

if exist "%TEMP_DIR%" rd /s /q "%TEMP_DIR%"

endlocal