param(
    [string]$Source = "attendance.db",
    [int]$KeepDays = 7
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $scriptDir
$backupDir = Join-Path $root "data\PK25-Cloud-Backup\backups"
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$zipName = "attendance_$ts.zip"
$zipPath = Join-Path $backupDir $zipName

Compress-Archive -Path $Source -DestinationPath $zipPath -Force

$hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLower()
"$hash  $zipName" | Out-File "$zipPath.sha256" -Encoding ASCII

Get-ChildItem "$backupDir\*.zip" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepDays) } |
    Remove-Item -Force

$logLine = "- $ts  $zipName  SHA-256=$hash"
Add-Content (Join-Path $root "data\PK25-Cloud-Backup\backup_log.md") $logLine

Write-Host "Created: $zipPath"
Write-Host "SHA-256: $hash"
Write-Host "Log:     backup_log.md"