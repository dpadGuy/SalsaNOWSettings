@echo off
setlocal EnableExtensions

set "ROOT=%~dp0"
set "PROJECT=%ROOT%SalsaNOWSettings\SalsaNOWSettings.csproj"
set "OUT=%ROOT%publish"

if not exist "%PROJECT%" (
  echo Project not found: "%PROJECT%"
  exit /b 1
)

echo.
echo Publishing SalsaNOWSettings (Release, win-x64, single-file)...
dotnet publish "%PROJECT%" ^
  -c Release ^
  -r win-x64 ^
  -p:Platform=x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:PublishTrimmed=false ^
  -p:WindowsAppSDKSelfContained=true ^
  -p:WindowsPackageType=None ^
  -o "%OUT%"

if errorlevel 1 (
  echo Publish failed.
  exit /b 1
)

echo.
echo Published: "%OUT%\SalsaNOWSettings.exe"
start "" explorer.exe "%OUT%"
endlocal
