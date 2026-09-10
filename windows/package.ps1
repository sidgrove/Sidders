<# Produces a complete, double-click installable ZIP from an already published app. #>
param(
    [string]$PublishDirectory = (Join-Path $PSScriptRoot 'dist'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts\release')
)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $PublishDirectory).Path
foreach ($required in @('Acapella.exe', 'Murmur.Platform.Windows.dll', 'NAudio.Core.dll', 'NAudio.WinMM.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $required))) { throw "Missing app component: $required" }
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$stage = Join-Path $OutputDirectory ('package-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $stage 'dist') -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination (Join-Path $stage 'dist') -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install.ps1'), (Join-Path $PSScriptRoot 'Install.cmd') -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\docs\INSTALL-WINDOWS.md') -Destination (Join-Path $stage 'READ-ME-FIRST.md')
$archive = Join-Path $OutputDirectory 'Acapella-Windows-x64.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Value "$hash  Acapella-Windows-x64.zip" -Encoding ascii
Write-Output $archive
