@echo off
setlocal
cd /d "%~dp0"

echo.
echo ========================================================================
echo  NDI Intercom2 - Publish + Installer
echo ========================================================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
  echo ERROR: dotnet not found in PATH.
  pause
  exit /b 1
)

echo [1/3] dotnet publish -^> publish-intercom2 ...
dotnet publish "NDI Intercom2.csproj" ^
  --configuration Release ^
  --runtime win-x64 ^
  --self-contained true ^
  --output "publish-intercom2" ^
  /p:PublishSingleFile=false ^
  /p:PublishTrimmed=false ^
  /p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 (
  echo.
  echo ERROR during publish. Close NDI Intercom2.exe if running and retry.
  pause
  exit /b 1
)

if not exist "publish-intercom2\NDI Intercom2.exe" (
  echo ERROR: publish-intercom2\NDI Intercom2.exe not found.
  pause
  exit /b 1
)

set "NDI_DLL=C:\Program Files\NDI\NDI 6 SDK\Bin\x64\Processing.NDI.Lib.x64.dll"
if exist "%NDI_DLL%" (
  copy /Y "%NDI_DLL%" "publish-intercom2\Processing.NDI.Lib.x64.dll" >nul
)

if exist "COM_icon_windows.ico" (
  if not exist "publish-intercom2\app.ico" copy /Y "COM_icon_windows.ico" "publish-intercom2\app.ico" >nul
)

set "ISCC="
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if exist "C:\Program Files\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"
if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"

if "%ISCC%"=="" (
  echo.
  echo [OK] Publish complete: "%~dp0publish-intercom2"
  echo      Inno Setup not found. Compile installer-script-intercom2.iss manually.
  pause
  exit /b 0
)

echo.
echo [2/3] Inno Setup compile ...
if not exist "Installer-Output" mkdir "Installer-Output"
"%ISCC%" "%~dp0installer-script-intercom2.iss"
if errorlevel 1 (
  echo.
  echo ERROR: Inno Setup compile failed.
  pause
  exit /b 1
)

echo.
echo [3/3] Done. Installer in: Installer-Output\
echo.
pause
