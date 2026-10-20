@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"
where dotnet >nul 2>&1
if errorlevel 1 (
  echo .NET SDK پیدا نشد. Visual Studio 2022 و .NET 8 SDK را نصب کن.
  pause
  exit /b 1
)
echo Restoring NuGet packages...
dotnet restore "HamDars\HamDars.csproj"
if errorlevel 1 goto failed
echo Building HamDars in Debug configuration...
dotnet build "HamDars\HamDars.csproj" -c Debug --no-restore
if errorlevel 1 goto failed
echo.
echo Build completed. To launch, run: dotnet run --project "HamDars\HamDars.csproj"
pause
exit /b 0
:failed
echo.
echo Build did not complete. Copy the first error from above if you need help.
pause
exit /b 1
