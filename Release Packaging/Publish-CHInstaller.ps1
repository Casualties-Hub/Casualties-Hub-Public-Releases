<#
Publishes the standalone Casualties Hub Setup Wizard into its own CHInstaller
folder. This is deliberately separate from individual Hub release ZIPs.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory,

    [ValidatePattern('^\d+\.\d+(?:-pre\.\d+)?$')]
    [string]$WizardVersion = '0.0-pre.1',

    [switch]$Replace
)

$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$installerProject = Join-Path $workspace 'Casualties Hub Installer\Casualties Hub Installer.csproj'
$helpDocument = Join-Path $PSScriptRoot 'Setup Wizard Help.txt'

if (-not (Test-Path -LiteralPath $installerProject)) {
    throw "Installer project was not found: $installerProject"
}
if (-not (Test-Path -LiteralPath $helpDocument)) {
    throw "Setup Wizard help document was not found: $helpDocument"
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$releaseFolderName = "Casualties Hub Setup Wizard $WizardVersion"
$targetDirectory = Join-Path $outputDirectory $releaseFolderName
$stagingDirectory = Join-Path $outputDirectory "$releaseFolderName - staging"

if (Test-Path -LiteralPath $stagingDirectory) {
    throw "A previous installer staging folder exists: $stagingDirectory"
}

if ((Test-Path -LiteralPath $targetDirectory) -and -not $Replace) {
    throw "$releaseFolderName already exists. Re-run with -Replace to replace only that Setup Wizard folder."
}

try {
    & dotnet publish $installerProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $stagingDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Installer publish failed with exit code $LASTEXITCODE."
    }

    @(
        'Casualties Hub Setup Wizard',
        '',
        'Run Casualties Hub Setup Wizard.exe to browse official GitHub releases, install or update Casualties Hub, and remove selected Hub copies.',
        'This Setup Wizard is separate from individual Casualties Hub release ZIPs. Removing a Hub copy does not delete shared settings or protected assets.'
    ) | Set-Content -LiteralPath (Join-Path $stagingDirectory 'README.txt') -Encoding utf8
    Copy-Item -LiteralPath $helpDocument -Destination (Join-Path $stagingDirectory 'Setup Wizard Help.txt') -Force

    if (Test-Path -LiteralPath $targetDirectory) {
        Remove-Item -LiteralPath $targetDirectory -Recurse -Force
    }

    Move-Item -LiteralPath $stagingDirectory -Destination $targetDirectory
    Write-Host "Setup Wizard folder created: $targetDirectory" -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}
