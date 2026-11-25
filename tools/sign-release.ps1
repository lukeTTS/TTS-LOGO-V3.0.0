param(
    [Parameter(Mandatory=$true)][string]$CertificateThumbprint,
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "publish\AlphaLogoUploader.exe"
$installer = Get-ChildItem (Join-Path $root "installer\output\*.exe") | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" | Sort-Object FullName -Descending | Select-Object -First 1

if (-not $signtool) { throw "signtool.exe was not found. Install the Windows 10/11 SDK." }
if (-not (Test-Path $exe)) { throw "Build the application first." }
if (-not $installer) { throw "Build the installer first." }

foreach ($file in @($exe, $installer.FullName)) {
    & $signtool.FullName sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $file
    & $signtool.FullName verify /pa /v $file
}

Write-Host "Application and installer are signed and verified."
