<#
Builds a GitHub-release folder containing the single Casualties Hub EXE, and a matching ZIP
beside it. The EXE carries its own catalogs, Hub content, and release notes, so nothing else
belongs in the release folder.

A separate developer console and uninstaller are planned as their own downloads. Neither is
bundled here, and the Hub does not depend on either one.
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
    [string]$OutputDirectory,

    [switch]$Replace
)

$ErrorActionPreference = 'Stop'

$normalizedVersion = $Version.TrimStart('v')
$releaseName = "Casualties Hub v$normalizedVersion"
$resolvedHubPublish = (Resolve-Path -LiteralPath $HubPublishDirectory).Path
$publishedExe = Join-Path $resolvedHubPublish 'Casualties Hub.exe'

if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw 'HubPublishDirectory must be the published Hub folder containing Casualties Hub.exe.'
}

# A single-file publish leaves no loose managed DLLs. Anything else here means the publish was
# not self-contained single-file, which would make the shipped EXE fail on a clean machine.
$strayDlls = @(Get-ChildItem -LiteralPath $resolvedHubPublish -Filter '*.dll' -File -ErrorAction SilentlyContinue)
if ($strayDlls.Count -gt 0) {
    throw "The published folder contains $($strayDlls.Count) loose DLL(s). Publish with -c Release so PublishSingleFile applies."
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$releaseDirectory = Join-Path $outputDirectory $releaseName
$zipPath = Join-Path $outputDirectory "$releaseName.zip"

foreach ($existing in @($releaseDirectory, $zipPath)) {
    if ((Test-Path -LiteralPath $existing) -and -not $Replace) {
        throw "Release output already exists. Re-run with -Replace to overwrite it: $existing"
    }
}

if (Test-Path -LiteralPath $releaseDirectory) { Remove-Item -LiteralPath $releaseDirectory -Recurse -Force }
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

New-Item -ItemType Directory -Path $releaseDirectory | Out-Null
Copy-Item -LiteralPath $publishedExe -Destination (Join-Path $releaseDirectory 'Casualties Hub.exe') -Force

$reportedVersion = (Get-Item -LiteralPath $publishedExe).VersionInfo.ProductVersion
if ($reportedVersion -and ($reportedVersion -split '\+')[0] -ne $normalizedVersion) {
    throw "The published EXE reports version '$reportedVersion' but the release is '$normalizedVersion'. Bump the csproj and republish."
}

Compress-Archive -Path (Join-Path $releaseDirectory '*') -DestinationPath $zipPath -Force

Write-Host "Release folder created: $releaseDirectory" -ForegroundColor Green
Write-Host "Release ZIP created:    $zipPath" -ForegroundColor Green
