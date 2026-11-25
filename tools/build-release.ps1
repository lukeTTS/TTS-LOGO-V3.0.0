param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$InnoSetupCompiler = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "AlphaLogoUploader\AlphaLogoUploader.csproj"
$publish = Join-Path $root "publish"

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

dotnet restore $project
dotnet publish $project -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $publish

if (-not (Test-Path (Join-Path $publish "AlphaLogoUploader.exe"))) {
    throw "Publish did not produce AlphaLogoUploader.exe"
}

if (Test-Path $InnoSetupCompiler) {
    & $InnoSetupCompiler (Join-Path $root "installer\AlphaLogoUploader.iss")
    Write-Host "Installer created in installer\output"
} else {
    Write-Warning "Inno Setup was not found. Install Inno Setup 6, then run this script again."
}
