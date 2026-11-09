param(
    [Parameter(Mandatory=$true)]
    [string]$Archive,

    [string]$TargetDir = "restored"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $scriptDir

if (-not (Test-Path $Archive)) {
    throw "Archive not found: $Archive"
}

$shaFile = "$Archive.sha256"
if (-not (Test-Path $shaFile)) {
    throw "SHA-256 file not found: $shaFile"
}

$expected = (Get-Content $shaFile).Split()[0]
$actual = (Get-FileHash $Archive -Algorithm SHA256).Hash.ToLower()

Write-Host "Expected SHA-256: $expected"
Write-Host "Actual SHA-256:   $actual"

if ($expected -ne $actual) {
    throw "SHA-256 mismatch - restore aborted"
}

$targetPath = Join-Path $root "data\PK25-Cloud-Backup\$TargetDir"
New-Item -ItemType Directory -Force -Path $targetPath | Out-Null
Expand-Archive -Path $Archive -DestinationPath $targetPath -Force

Write-Host "Restored to: $targetPath"