<#
.SYNOPSIS
    Packages Casualties Hub for release: a tarball for Linux and a zip for Windows.

.DESCRIPTION
    Both archives come from the same project, published once per runtime. The Linux archive is a
    .tar.gz rather than a .zip on purpose: zip does not record the Unix executable bit, so a
    zipped build hands every user "permission denied" as their first experience. tar preserves
    mode 0755, and Windows 10+ ships bsdtar as tar.exe, so no extra tooling is needed.

.EXAMPLE
    .\Build-CasualtiesHubRelease.ps1 -OutputDirectory "$HOME\Documents\Casualties Hub\Builds"

.EXAMPLE
    .\Build-CasualtiesHubRelease.ps1 -OutputDirectory "$HOME\Documents\Casualties Hub\Builds" -Platform windows
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [ValidatePattern('^v?\d+\.\d+\.\d+(-pre\.\d+(\.\d+)?)?$')]
    [string]$Version = '0.0.8-pre.6.1',

    [ValidateSet('linux', 'windows', 'both')]
    [string]$Platform = 'both',

    [switch]$Replace
)

$ErrorActionPreference = 'Stop'

$normalizedVersion = $Version.TrimStart('v')
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'Casualties Hub\Casualties Hub.csproj'
$packagingDir = Join-Path $repoRoot 'Casualties Hub\Packaging'
$icon = Join-Path $repoRoot 'Casualties Hub\Assets\CasualtiesHub.png'

if (-not (Test-Path $project)) { throw "Could not find the Hub project at $project" }

function Publish-Hub {
    param([string]$Runtime, [string]$Staging, [string]$BinaryName)

    Write-Host "Publishing $Runtime..." -ForegroundColor Cyan
    dotnet publish $project -c Release -r $Runtime -o $Staging --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish ($Runtime) failed with exit code $LASTEXITCODE." }

    $binary = Join-Path $Staging $BinaryName
    if (-not (Test-Path $binary)) { throw "Publish did not produce '$BinaryName'. Check AssemblyName in the csproj." }

    # The release must be one portable file. Loose assemblies mean PublishSingleFile silently
    # stopped bundling something, which previously shipped a build that started fine and then
    # failed at runtime.
    $loose = Get-ChildItem $Staging -Filter *.dll -ErrorAction SilentlyContinue
    if ($loose) { throw "Found $($loose.Count) loose DLL(s); PublishSingleFile did not bundle everything: $($loose.Name -join ', ')" }

    Get-ChildItem $Staging -Filter *.pdb -ErrorAction SilentlyContinue | Remove-Item -Force
}

function Assert-Fresh {
    param([string[]]$Paths)
    foreach ($path in $Paths) {
        if ((Test-Path $path) -and -not $Replace) { throw "$path already exists. Pass -Replace to overwrite." }
        Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Build-LinuxRelease {
    $releaseDir = Join-Path $OutputDirectory "Casualties Hub v$normalizedVersion linux-x64"
    $tarball = Join-Path $OutputDirectory "casualties-hub-v$normalizedVersion-linux-x64.tar.gz"
    Assert-Fresh @($releaseDir, $tarball)

    $staging = "$releaseDir - staging"
    Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue

    try {
        Publish-Hub -Runtime 'linux-x64' -Staging $staging -BinaryName 'casualties-hub'

        foreach ($file in @('README-linux.txt', 'casualties-hub.desktop', 'install-desktop-entry.sh')) {
            $source = Join-Path $packagingDir $file
            if (Test-Path $source) { Copy-Item $source (Join-Path $staging $file) }
            else { Write-Warning "Packaging file not found, skipping: $file" }
        }
        if (Test-Path $icon) { Copy-Item $icon (Join-Path $staging 'casualties-hub.png') }

        Rename-Item $staging $releaseDir

        Write-Host "Creating tarball..." -ForegroundColor Cyan

        # NTFS has no executable bit, and the bsdtar shipped with Windows has no --mode option to
        # fake one. A tarball built here would hand the user "permission denied" on first run.
        # WSL has a real filesystem and GNU tar, so build the archive there when it is available.
        $executableBitSet = $false
        if (Get-Command wsl.exe -ErrorAction SilentlyContinue) {
            $wslDir = (wsl wslpath -a ($releaseDir -replace '\\', '/')).Trim()
            $wslTarball = (wsl wslpath -a ($tarball -replace '\\', '/')).Trim()
            # Stage on the Linux filesystem before setting modes. Windows drives mount as 9p/drvfs
            # without the metadata option, which reports every file as 777 and makes chmod a silent
            # no-op - so archiving straight from /mnt/c gets the executable bit only by luck of the
            # mount options, and marks the README executable too.
            $shell = @"
set -e
STAGE=`$(mktemp -d)
trap 'rm -rf "`$STAGE"' EXIT
cp -r '$wslDir/.' "`$STAGE/"
cd "`$STAGE"
chmod 755 casualties-hub
[ -f install-desktop-entry.sh ] && chmod 755 install-desktop-entry.sh
for f in README-linux.txt casualties-hub.desktop casualties-hub.png; do
    [ -f "`$f" ] && chmod 644 "`$f"
done
tar -czf '$wslTarball' .
"@
            wsl -e bash -c $shell
            if ($LASTEXITCODE -eq 0) {
                $executableBitSet = $true
                Write-Host '  built via WSL; executable bit preserved' -ForegroundColor DarkGray
            }
            else {
                Write-Warning 'WSL tar failed; falling back to Windows tar.'
            }
        }

        if (-not $executableBitSet) {
            Push-Location $OutputDirectory
            try {
                tar --create --gzip --file $tarball --directory $releaseDir .
                if ($LASTEXITCODE -ne 0) { throw "tar failed with exit code $LASTEXITCODE." }
            }
            finally { Pop-Location }
            Write-Warning 'Built without WSL: the archive does NOT carry the executable bit.'
            Write-Warning 'The user must run "chmod +x casualties-hub" first (README-linux.txt covers this).'
        }

        $sizeMb = [math]::Round((Get-Item $tarball).Length / 1MB, 1)
        Write-Host "Linux  : $tarball ($sizeMb MB)" -ForegroundColor Green
    }
    finally {
        Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Build-WindowsRelease {
    $releaseDir = Join-Path $OutputDirectory "Casualties Hub v$normalizedVersion win-x64"
    $zip = Join-Path $OutputDirectory "casualties-hub-v$normalizedVersion-win-x64.zip"
    Assert-Fresh @($releaseDir, $zip)

    $staging = "$releaseDir - staging"
    Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue

    try {
        Publish-Hub -Runtime 'win-x64' -Staging $staging -BinaryName 'casualties-hub.exe'
        Rename-Item $staging $releaseDir

        Write-Host "Creating zip..." -ForegroundColor Cyan
        Compress-Archive -Path (Join-Path $releaseDir '*') -DestinationPath $zip -CompressionLevel Optimal

        $sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
        Write-Host "Windows: $zip ($sizeMb MB)" -ForegroundColor Green
    }
    finally {
        Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
    }
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

if ($Platform -in @('linux', 'both')) { Build-LinuxRelease }
if ($Platform -in @('windows', 'both')) { Build-WindowsRelease }

Write-Host ''
Write-Host "Release folders are in $OutputDirectory" -ForegroundColor Yellow
