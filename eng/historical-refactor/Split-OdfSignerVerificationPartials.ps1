#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$coreDir = Join-Path $PSScriptRoot '..\OdfKit\Core'
$sourcePath = Join-Path $coreDir 'OdfSigner.Verification.cs'

if (-not (Test-Path $sourcePath)) {
    Write-Host 'OdfSigner.Verification.cs already split or missing; skipping removal.'
    exit 0
}

Remove-Item -Path $sourcePath -Force
Write-Host 'Removed OdfSigner.Verification.cs (replaced by Entry/Single/Dsig/Revocation/Timestamp partials).'
Write-Host 'Done.'