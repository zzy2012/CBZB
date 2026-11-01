param(
    [string]$UnityExe = "",
    [string]$UnityProject = "",
    [string]$DotnetExe = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
if (-not $UnityProject) { $UnityProject = Join-Path $repoRoot "UnityProject" }
if (-not $UnityExe) { $UnityExe = "C:\Program Files\Unity\Hub\Editor\2022.3.20f1\Editor\Unity.exe" }
if (-not $DotnetExe) { $DotnetExe = "dotnet" }

if (-not (Test-Path $UnityExe)) { throw "Unity executable not found: $UnityExe" }
$windowsSupport = Get-ChildItem (Split-Path $UnityExe) -Recurse -Directory -Filter "WindowsStandaloneSupport" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $windowsSupport) { throw "Unity Windows Build Support is not installed. This script intentionally installs nothing." }

$luban = Join-Path $repoRoot "Tools\Luban\Luban.dll"
if (-not (Test-Path $luban)) { throw "Luban.dll is missing: $luban" }

Push-Location (Join-Path $repoRoot "Configs\GameConfig")
try {
    & $DotnetExe "..\..\Tools\Luban\Luban.dll" `
        -t client -c cs-bin -d bin `
        --conf "luban.conf" `
        -x code.lineEnding=crlf `
        -x "outputCodeDir=$UnityProject\Assets\GameScripts\HotFix\GameProto\GameConfig" `
        -x "outputDataDir=$UnityProject\Assets\AssetRaw\Configs\bytes"
    if ($LASTEXITCODE -ne 0) { throw "Luban generation failed with exit code $LASTEXITCODE" }
}
finally {
    Pop-Location
}

$validationLog = Join-Path $repoRoot "Build\validation.log"
New-Item -ItemType Directory -Force -Path (Split-Path $validationLog) | Out-Null
& $UnityExe -batchmode -nographics -quit `
    -projectPath $UnityProject `
    -executeMethod TakeoverValidation.RunBatch `
    -logFile $validationLog
if ($LASTEXITCODE -ne 0) { throw "Takeover validation failed. See $validationLog" }

$soakLog = Join-Path $repoRoot "Build\chapter-soak.log"
& $UnityExe -batchmode -nographics -quit `
    -projectPath $UnityProject `
    -executeMethod TakeoverValidation.RunScenarioSoakBatch `
    -logFile $soakLog
if ($LASTEXITCODE -ne 0) { throw "First-chapter soft-lock scan failed. See $soakLog" }

$buildLog = Join-Path $repoRoot "Build\windows-build.log"
& $UnityExe -batchmode -nographics -quit `
    -projectPath $UnityProject `
    -executeMethod TEngine.ReleaseTools.AutomationBuild `
    -logFile $buildLog
if ($LASTEXITCODE -ne 0) { throw "Windows build failed. See $buildLog" }

$exe = Join-Path $UnityProject "Build\Windows\Release_Windows.exe"
if (-not (Test-Path $exe)) { throw "Build command succeeded but executable is missing: $exe" }

$playerDir = Split-Path $exe
$symbolDir = Join-Path $playerDir "Symbols"
New-Item -ItemType Directory -Force -Path $symbolDir | Out-Null
$scriptAssemblyRoot = Join-Path $UnityProject "Library\ScriptAssemblies"
Get-ChildItem $scriptAssemblyRoot -Filter *.pdb -ErrorAction SilentlyContinue |
    Copy-Item -Destination $symbolDir -Force
Copy-Item (Join-Path $PSScriptRoot "source-lock.json") $symbolDir -Force
Copy-Item (Join-Path $PSScriptRoot "asset-locations.csv") $symbolDir -Force

$symbols = Get-ChildItem $symbolDir -Filter *.pdb -ErrorAction SilentlyContinue
if (-not $symbols) { throw "Windows build has no symbol files" }

Write-Host "[build] PASS"
Write-Host "[build] Executable: $exe"
Write-Host "[build] Validation: $validationLog"
Write-Host "[build] Log: $buildLog"
