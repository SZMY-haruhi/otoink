# Requires PowerShell 7+ or Windows PowerShell 5.1 with tar.exe.
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$folderName = "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17"
$dest = Join-Path $repoRoot "models\$folderName"
$int8 = Join-Path $dest "model.int8.onnx"
$tokens = Join-Path $dest "tokens.txt"

function Test-ModelPresent([string] $dir) {
    return (Test-Path (Join-Path $dir "model.int8.onnx")) -and (Test-Path (Join-Path $dir "tokens.txt"))
}

if (Test-ModelPresent $dest) {
    Write-Host "SenseVoice int8 already in $dest"
    exit 0
}

New-Item -ItemType Directory -Force -Path $dest | Out-Null

$local = Join-Path $env:LOCALAPPDATA "otoink\models\$folderName"
if (Test-ModelPresent $local) {
    Copy-Item (Join-Path $local "model.int8.onnx") $int8 -Force
    Copy-Item (Join-Path $local "tokens.txt") $tokens -Force
    Write-Host "Copied SenseVoice int8 from LocalAppData into repo models/ (runtime will not read LocalAppData)."
    exit 0
}

$url = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17.tar.bz2"
$tmp = Join-Path $env:TEMP ("otoink-sense-voice-" + [guid]::NewGuid().ToString("N") + ".tar.bz2")
$extract = Join-Path $env:TEMP ("otoink-sense-voice-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $extract | Out-Null

Write-Host "Downloading SenseVoice int8 into repo models/ (not into LocalAppData)..."
Invoke-WebRequest -Uri $url -OutFile $tmp -UseBasicParsing
tar -xjf $tmp -C $extract

$foundInt8 = Get-ChildItem $extract -Recurse -Filter "model.int8.onnx" | Select-Object -First 1
$foundTokens = Get-ChildItem $extract -Recurse -Filter "tokens.txt" | Select-Object -First 1
if (-not $foundInt8 -or -not $foundTokens) {
    throw "Archive did not contain model.int8.onnx and tokens.txt"
}

Copy-Item $foundInt8.FullName $int8 -Force
Copy-Item $foundTokens.FullName $tokens -Force

Remove-Item $tmp -Force -ErrorAction SilentlyContinue
Remove-Item $extract -Recurse -Force -ErrorAction SilentlyContinue

if (-not (Test-ModelPresent $dest)) {
    throw "Failed to install SenseVoice int8 into $dest"
}

Write-Host "Installed SenseVoice int8 to $dest"
