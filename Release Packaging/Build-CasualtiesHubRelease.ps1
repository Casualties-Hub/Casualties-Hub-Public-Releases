<#
Builds a clean GitHub-release ZIP without changing the Hub source project.

The resulting archive always has one top-level folder. The Hub and the
emergency uninstaller remain immediately visible at its root. The standalone
installer is intentionally published separately by Publish-CHInstaller.ps1.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$HubPublishDirectory,

    [Parameter(Mandatory)]
    [ValidatePattern('^v?\d+\.\d+\.\d+(-pre\.\d+)?$')]
    [string]$Version,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

$workspace = Split-Path -Parent $PSScriptRoot
$uninstaller = Join-Path $workspace 'CH Uninstaller.cmd'
$normalizedVersion = $Version.TrimStart('v')
$releaseName = "Casualties Hub v$normalizedVersion"
$resolvedHubPublish = (Resolve-Path -LiteralPath $HubPublishDirectory).Path

if (-not (Test-Path -LiteralPath $uninstaller)) {
    throw "CH Uninstaller.cmd was not found: $uninstaller"
}

if (-not (Test-Path -LiteralPath (Join-Path $resolvedHubPublish 'Casualties Hub.exe'))) {
    throw 'HubPublishDirectory must be the published Hub folder containing Casualties Hub.exe.'
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$stagingDirectory = Join-Path $outputDirectory "$releaseName - staging"
$releaseDirectory = Join-Path $stagingDirectory $releaseName
$zipPath = Join-Path $outputDirectory "$releaseName.zip"

if ((Test-Path -LiteralPath $stagingDirectory) -or (Test-Path -LiteralPath $zipPath)) {
    throw "Release output already exists. Choose a new output folder or remove the old staging/ZIP: $releaseName"
}

try {
    New-Item -ItemType Directory -Path $releaseDirectory | Out-Null
    Get-ChildItem -LiteralPath $resolvedHubPublish -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $releaseDirectory -Recurse -Force
    }
    Copy-Item -LiteralPath $uninstaller -Destination (Join-Path $releaseDirectory 'CH Uninstaller.cmd') -Force

    @(
        'Unpack this ZIP anywhere outside Program Files.',
        'Run Casualties Hub.exe to use the mod manager.',
        'The standalone Casualties Hub Installer is distributed separately from this Hub ZIP.',
        'CH Uninstaller.cmd is available if you need to remove the Hub later.'
    ) | Set-Content -LiteralPath (Join-Path $releaseDirectory 'INSTALLATION.txt') -Encoding utf8

    Compress-Archive -LiteralPath $releaseDirectory -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Host "Release ZIP created: $zipPath" -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}
