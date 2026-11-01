param(
    [string]$GameDataRoot = "E:\GameData\城邦争霸_Takeover",
    [string]$FfdecJar = "",
    [string]$FfdecExe = "",
    [string]$JavaExe = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$swf = Join-Path $GameDataRoot "takeover104.swf"
$sourceUrl = "http://sxiao.4399.com/4399swf/upload_swf/ftp8/chenweihong/20120920/takeover104.swf"
$expectedBytes = 20934803
$expectedHash = "b9640da8118cf924d87a759834a39197db62c3273b4aeeef4eb5f990d37a1463"
$referer = "http://www.4399.com/flash/98278.htm"
$ffdecOut = Join-Path $GameDataRoot "ffdec_asset_export"

if (-not (Test-Path $GameDataRoot)) {
    New-Item -ItemType Directory -Force -Path $GameDataRoot | Out-Null
}

if (-not (Test-Path $swf)) {
    Write-Host "[extract] Downloading Takeover 1.0.4"
    Invoke-WebRequest -Uri $sourceUrl -Headers @{ Referer = $referer } -OutFile $swf
}

$size = (Get-Item $swf).Length
if ($size -ne $expectedBytes) {
    throw "SWF size mismatch. Expected $expectedBytes bytes, got $size. The source is not the locked 1.0.4 build."
}

$hash = (Get-FileHash $swf -Algorithm SHA256).Hash.ToLowerInvariant()
if ($hash -ne $expectedHash) {
    throw "SWF SHA-256 mismatch. Expected $expectedHash, got $hash."
}
$lock = [ordered]@{
    version = "1.0.4"
    sourceUrl = $sourceUrl
    sourceReferer = $referer
    sizeBytes = $size
    sha256 = $hash
    generatedUtc = [DateTime]::UtcNow.ToString("o")
}
$lock | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $GameDataRoot "source-lock.json")
Write-Host "[extract] Source locked: $hash"

if (-not (Test-Path $ffdecOut)) {
    New-Item -ItemType Directory -Force -Path $ffdecOut | Out-Null
}

if ($FfdecJar -and $JavaExe) {
    & $JavaExe -Xmx4g -Djava.awt.headless=true -jar $FfdecJar -onerror ignore `
        -export script,image,shape,morphshape,font,font4,button,binaryData,symbolClass,text `
        $ffdecOut $swf
    if ($LASTEXITCODE -ne 0) { throw "FFDec source extraction failed with exit code $LASTEXITCODE" }
    & $JavaExe -Xmx4g -Djava.awt.headless=true -jar $FfdecJar -onerror ignore `
        -format sprite:png -export sprite (Join-Path $ffdecOut "sprites") $swf
    if ($LASTEXITCODE -ne 0) { throw "FFDec sprite extraction failed with exit code $LASTEXITCODE" }
    & $JavaExe -Xmx4g -Djava.awt.headless=true -jar $FfdecJar -onerror ignore `
        -format sound:wav -export sound (Join-Path $ffdecOut "sounds_wav") $swf
    if ($LASTEXITCODE -ne 0) { throw "FFDec sound extraction failed with exit code $LASTEXITCODE" }
}
elseif ($FfdecExe) {
    & $FfdecExe -onerror ignore `
        -export script,image,shape,morphshape,font,font4,button,binaryData,symbolClass,text `
        $ffdecOut $swf
    if ($LASTEXITCODE -ne 0) { throw "FFDec source extraction failed with exit code $LASTEXITCODE" }
    & $FfdecExe -onerror ignore `
        -format sprite:png -export sprite (Join-Path $ffdecOut "sprites") $swf
    if ($LASTEXITCODE -ne 0) { throw "FFDec sprite extraction failed with exit code $LASTEXITCODE" }
    & $FfdecExe -onerror ignore `
        -format sound:wav -export sound (Join-Path $ffdecOut "sounds_wav") $swf
    if ($LASTEXITCODE -ne 0) { throw "FFDec sound extraction failed with exit code $LASTEXITCODE" }
}
else {
    throw "FFDec is required to finish asset extraction. Pass -FfdecJar/-JavaExe or -FfdecExe. This script intentionally installs nothing."
}

$scriptCount = (Get-ChildItem $ffdecOut -Recurse -Filter *.as -ErrorAction SilentlyContinue).Count
$imageCount = (Get-ChildItem $ffdecOut -Recurse -Include *.png,*.jpg,*.jpeg -ErrorAction SilentlyContinue).Count
$soundCount = (Get-ChildItem $ffdecOut -Recurse -Include *.mp3,*.wav,*.ogg -ErrorAction SilentlyContinue).Count
if ($scriptCount -lt 1400) { throw "Expected at least 1400 ActionScript files, got $scriptCount" }
if ($imageCount -lt 1) { throw "No image assets extracted" }

Write-Host "[extract] Scripts=$scriptCount Images=$imageCount Sounds=$soundCount"
Write-Host "[extract] Output: $ffdecOut"
Write-Host "[extract] Next: python Tools/TakeoverPipeline/import_chapter_assets.py"
