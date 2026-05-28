@echo off
setlocal
cd /d "%~dp0"

echo.
echo ========================================================================
echo  NDI Intercom2 - Publish + Installer (usa questo PRIMA di compilare in Inno)
echo ========================================================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
  echo ERRORE: dotnet non trovato nel PATH.
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
  echo ERRORE durante publish. Chiudi NDI Intercom2.exe se e' in esecuzione e riprova.
  pause
  exit /b 1
)

if not exist "publish-intercom2\NDI Intercom2.exe" (
  echo ERRORE: publish-intercom2\NDI Intercom2.exe non trovato.
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

if "%ISCC%"=="" (
  echo.
  echo [OK] Publish completata in: "%~dp0publish-intercom2"
  echo      Inno Setup non trovato. Compila manualmente installer-script-intercom2.iss
  pause
  exit /b 0
)

echo.
echo [2/3] Inno Setup compile ...
if not exist "Installer-Output" mkdir "Installer-Output"
"%ISCC%" "%~dp0installer-script-intercom2.iss"
if errorlevel 1 (
  echo.
  echo ERRORE compilazione Inno Setup.
  pause
  exit /b 1
)

echo.
echo [3/3] Fatto. Installer in: Installer-Output\
echo.
pause
