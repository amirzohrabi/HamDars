@echo off
setlocal
cd /d "%~dp0"
echo Publishing HamDars for Windows x64...
dotnet publish "HamDars\HamDars.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "publish\win-x64"
if errorlevel 1 (
  echo.
  echo Publish failed. Install Visual Studio 2022 with .NET desktop development and the .NET 8 SDK, then retry.
  pause
  exit /b 1
)
echo.
echo Finished. EXE location: publish\win-x64\HamDars.exe
pause
