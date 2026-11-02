@echo off
rem Export Luban table *data only* (no code generation).
rem
rem The generated C# in GameProto\GameConfig is pinned to the template version the
rem project was created with; the Luban build available on this machine is newer and
rem would write flat file names (takeover.Building.cs) plus template churn. So table
rem data is exported here, while generated code changes are applied by hand.
setlocal
cd /d %~dp0

set WORKSPACE=..\..
if not defined LUBAN_DLL set LUBAN_DLL=%WORKSPACE%\Tools\Luban\Luban.dll
if not exist "%LUBAN_DLL%" (
    echo [ERROR] Luban.dll not found: "%LUBAN_DLL%"
    echo         Put a Luban build under Tools\Luban\ or set the LUBAN_DLL env var.
    exit /b 1
)

rem Luban ships as a net7 app; roll forward so a newer runtime can still run it.
if not defined DOTNET_ROLL_FORWARD set DOTNET_ROLL_FORWARD=Major

set STAGE=%TEMP%\cbzb_luban_data
set DATA_OUTPATH=%WORKSPACE%\UnityProject\Assets\AssetRaw\Configs\bytes

if exist "%STAGE%" rd /s /q "%STAGE%"
mkdir "%STAGE%"

echo [1/2] Exporting table data ...
dotnet "%LUBAN_DLL%" ^
    -t client ^
    -d bin ^
    --conf "%~dp0luban.conf" ^
    -x "outputDataDir=%STAGE%"
if errorlevel 1 (
    echo [ERROR] Luban data export failed.
    exit /b 1
)

echo [2/2] Copying bytes to %DATA_OUTPATH%
copy /y "%STAGE%\*.bytes" "%DATA_OUTPATH%\" >nul
if errorlevel 1 (
    echo [ERROR] Copy bytes failed.
    exit /b 1
)

dir /b "%STAGE%\*.bytes"
echo [OK] Table bytes exported. Run git status to confirm only the tables you edited changed.
exit /b 0
