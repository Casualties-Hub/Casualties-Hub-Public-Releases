<#
Builds a clean, unzipped GitHub-release folder without changing the Hub source
project. The publisher can ZIP this one folder afterwards if desired.

The root stays intentionally small: the Hub EXE, two helper CMD files, the
read-me, and a Data folder for editable catalogs and local release notes.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$HubPublishDirectory,

    [Parameter(Mandatory)]
    [ValidatePattern('^v?\d+\.\d+\.\d+(-pre\.\d+(\.\d+)?)?$')]
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
$releaseDirectory = Join-Path $outputDirectory $releaseName

if (Test-Path -LiteralPath $releaseDirectory) {
    throw "Release output already exists. Choose a new output folder or remove the old release folder: $releaseName"
}

New-Item -ItemType Directory -Path $releaseDirectory | Out-Null

$requiredRootFiles = @('Casualties Hub.exe', 'Developer Console.cmd', '00 - READ ME FIRST.txt')
foreach ($file in $requiredRootFiles) {
    $source = Join-Path $resolvedHubPublish $file
    if (-not (Test-Path -LiteralPath $source)) {
        throw "The published Hub folder is missing required release file: $file"
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $releaseDirectory $file) -Force
}

$dataSource = Join-Path $resolvedHubPublish 'Data'
if (-not (Test-Path -LiteralPath $dataSource)) {
    throw 'The published Hub folder is missing Data. Publish the Release configuration before packaging.'
}
Copy-Item -LiteralPath $dataSource -Destination (Join-Path $releaseDirectory 'Data') -Recurse -Force
Copy-Item -LiteralPath $uninstaller -Destination (Join-Path $releaseDirectory 'CH Uninstaller.cmd') -Force

Write-Host "Release folder created: $releaseDirectory" -ForegroundColor Green
