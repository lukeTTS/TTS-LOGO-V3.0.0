param(
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$InstallerUrl,
    [string]$ReleaseNotes = "Maintenance and reliability improvements."
)
$root = Split-Path -Parent $PSScriptRoot
$installer = Get-ChildItem (Join-Path $root "installer\output\*.exe") | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $installer) { throw "No installer was found." }
$hash = (Get-FileHash $installer.FullName -Algorithm SHA256).Hash
@{
    version = $Version
    installerUrl = $InstallerUrl
    sha256 = $hash
    releaseNotes = $ReleaseNotes
} | ConvertTo-Json | Set-Content (Join-Path $root "update.json") -Encoding UTF8
Write-Host "Created update.json with SHA-256 $hash"
