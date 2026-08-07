<#
.SYNOPSIS
    Packages the Casualties Hub Linux Edition into a tarball a tester can extract and run.

.DESCRIPTION
    The Windows script (Build-CasualtiesHubRelease.ps1) is untouched and still owns the Windows
    release. This is its Linux counterpart and shares nothing with it.

    A .tar.gz rather than a .zip on purpose: zip does not record the Unix executable bit, so a
    zipped build hands every tester "permission denied" as their first experience. tar preserves
    mode 0755, and Windows 10+ ships bsdtar as tar.exe, so no extra tooling is needed.

.EXAMPLE
    .\Build-CasualtiesHubLinuxRelease.ps1 -OutputDirectory "$HOME\Documents\Casualties Hub\Builds"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [ValidatePattern('^v?\d+\.\d+\.\d+(-pre\.\d+(\.\d+)?)?$')]
    [string]$Version = '0.0.8-pre.6.1',

    [switch]$Replace
)

$ErrorActionPreference = 'Stop'

$normalizedVersion = $Version.TrimStart('v')
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'Casualties Hub Linux\Casualties Hub Linux.csproj'
$packagingDir = Join-Path $repoRoot 'Casualties Hub Linux\Packaging'

if (-not (Test-Path $project)) { throw "Could not find the Linux project at $project" }

$releaseName = "Casualties Hub v$normalizedVersion Linux Edition"
$releaseDir = Join-Path $OutputDirectory $releaseName
$tarball = Join-Path $OutputDirectory "casualties-hub-v$normalizedVersion-linux-x64.tar.gz"

if ((Test-Path $releaseDir) -and -not $Replace) { throw "$releaseDir already exists. Pass -Replace to overwrite." }
if ((Test-Path $tarball) -and -not $Replace) { throw "$tarball already exists. Pass -Replace to overwrite." }

Remove-Item $releaseDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $tarball -Force -ErrorAction SilentlyContinue

$staging = "$releaseDir - staging"
Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue

try {
    Write-Host "Publishing linux-x64..." -ForegroundColor Cyan
    dotnet publish $project -c Release -o $staging --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

    $binary = Join-Path $staging 'casualties-hub'
    if (-not (Test-Path $binary)) { throw "Publish did not produce 'casualties-hub'. Check AssemblyName in the csproj." }

    # The release must be one portable file. Loose assemblies mean PublishSingleFile silently
    # stopped bundling something, which on Windows previously shipped a build that started fine
    # and then failed at runtime.
    $loose = Get-ChildItem $staging -Filter *.dll -ErrorAction SilentlyContinue
    if ($loose) { throw "Found $($loose.Count) loose DLL(s); PublishSingleFile did not bundle everything: $($loose.Name -join ', ')" }

    Get-ChildItem $staging -Filter *.pdb -ErrorAction SilentlyContinue | Remove-Item -Force

    foreach ($file in @('README-linux.txt', 'casualties-hub.desktop', 'install-desktop-entry.sh')) {
        $source = Join-Path $packagingDir $file
        if (Test-Path $source) { Copy-Item $source (Join-Path $staging $file) }
        else { Write-Warning "Packaging file not found, skipping: $file" }
    }

    $icon = Join-Path $repoRoot 'Casualties Hub\Assets\CasualtiesHub.png'
    if (Test-Path $icon) { Copy-Item $icon (Join-Path $staging 'casualties-hub.png') }

    Rename-Item $staging $releaseDir

    Write-Host "Creating tarball..." -ForegroundColor Cyan

    # NTFS has no executable bit, and the bsdtar shipped with Windows has no --mode option to
    # fake one. A tarball built here would hand the tester "permission denied" on first run.
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
        Write-Warning 'The tester must run "chmod +x casualties-hub" first (README-linux.txt covers this).'
    }

    $sizeMb = [math]::Round((Get-Item $tarball).Length / 1MB, 1)
    Write-Host ''
    Write-Host "Folder : $releaseDir" -ForegroundColor Green
    Write-Host "Tarball: $tarball ($sizeMb MB)" -ForegroundColor Green
    Write-Host ''
    Write-Host 'Tester instructions:' -ForegroundColor Yellow
    Write-Host "  tar -xzf $(Split-Path -Leaf $tarball)"
    Write-Host '  ./casualties-hub --diagnostics > hub-report.txt 2>&1'
}
finally {
    Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
}
