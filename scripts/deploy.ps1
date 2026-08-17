param(
    [string]$GameRoot = "D:\Games\steam\steamapps\common\BlackMythWukong",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$modName = "WukongCombatKit"
$loaderRoot = Join-Path $GameRoot "b1\Binaries\Win64"
$modDir = Join-Path $loaderRoot "CSharpLoader\Mods\$modName"

function Copy-IfExists($source, $destination) {
    if (Test-Path $source) {
        New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
        Copy-Item $source $destination -Force
        Write-Host "copied $source -> $destination"
    }
}

$loaderSource = "C:\Users\zhanh\AppData\Local\Temp\opencode\b1cs_release\b1\Binaries\Win64"
if (-not (Test-Path (Join-Path $loaderRoot "version.dll"))) {
    if (Test-Path $loaderSource) {
        Copy-Item (Join-Path $loaderSource "version.dll") (Join-Path $loaderRoot "version.dll") -Force
        Copy-Item (Join-Path $loaderSource "CSharpLoader") (Join-Path $loaderRoot "CSharpLoader") -Recurse -Force
        Write-Host "Installed B1CSharpLoader into $loaderRoot"
    } else {
        Write-Warning "B1CSharpLoader source not found. Install it before launching the game."
    }
}

New-Item -ItemType Directory -Force -Path $modDir | Out-Null
Copy-IfExists (Join-Path $repoRoot "src\$modName\bin\$modName.dll") (Join-Path $modDir "$modName.dll")
Copy-IfExists (Join-Path $repoRoot "src\$modName\bin\$modName.pdb") (Join-Path $modDir "$modName.pdb")
if (-not (Test-Path (Join-Path $modDir "config.json"))) {
    Copy-IfExists (Join-Path $repoRoot "src\$modName\config.json") (Join-Path $modDir "config.json")
}

Write-Host "Deployed $modName to $modDir"
