#####################################################
# Build OTC Data Service and MSI installer(s)
# Requires: .NET SDK, Visual Studio MSBuild, WiX Toolset 3.14
#####################################################

[CmdletBinding()]
param (
    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    [ValidateSet('x86', 'x64', 'Both')]
    [string] $Platform = 'Both',

    [string] $OutputDir = '.\publish\OtcDataService',

    [switch] $SkipAppPublish,

    [switch] $Clean
)

$ErrorActionPreference = 'Stop'

$RepoRoot = $PSScriptRoot
$AppProject = Join-Path $RepoRoot 'OtcDataService\OtcDataService.csproj'
$SetupProject = Join-Path $RepoRoot 'OtcDataService.Setup\OtcDataService.Setup.wixproj'
$OutputDirFull = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $OutputDir))

$PlatformTargets = if ($Platform -eq 'Both') { @('x86', 'x64') } else { @($Platform) }

$PublishConfig = @{
    x86 = @{
        Rid        = 'win-x86'
        PublishDir = Join-Path $RepoRoot 'OtcDataService\bin\Release\net8.0-windows\publish\win-x86'
        MsiName    = 'OtcDataService-setup-x86.msi'
    }
    x64 = @{
        Rid        = 'win-x64'
        PublishDir = Join-Path $RepoRoot 'OtcDataService\bin\Release\net8.0-windows\publish\win-x64'
        MsiName    = 'OtcDataService-setup-x64.msi'
    }
}

function Write-Step([string]$Message) {
    Write-Host ''
    Write-Host "===== $Message =====" -ForegroundColor Cyan
}

function Get-VersionFromCsproj([string]$CsprojPath) {
    [xml]$proj = Get-Content $CsprojPath
    $version = $proj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if (-not $version) {
        $version = $proj.Project.PropertyGroup.AssemblyVersion | Where-Object { $_ } | Select-Object -First 1
    }
    if (-not $version) { return '0.0.0' }
    return "$version".Trim()
}

function Invoke-External([string]$Label, [scriptblock]$Action) {
    Write-Host ">> $Label"
    try {
        & $Action
        if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
            throw "Command failed with exit code $LASTEXITCODE"
        }
    } catch {
        Write-Host "FAILED: $Label" -ForegroundColor Red
        throw
    }
}

function Publish-App([string]$Arch) {
    $cfg = $PublishConfig[$Arch]
    $publishExe = Join-Path $cfg.PublishDir 'OtcDataService.exe'

    if ($Configuration -ne 'Release') {
        Invoke-External "dotnet build (Debug)" {
            dotnet build $AppProject -c Debug
        }
        return $publishExe
    }

    $dotnetOk = $false
    try {
        Invoke-External "dotnet publish (Release, $($cfg.Rid), self-contained)" {
            dotnet publish $AppProject `
                -c Release `
                -r $cfg.Rid `
                --self-contained true `
                -o $cfg.PublishDir
        }
        $dotnetOk = (Test-Path $publishExe)
    } catch {
        Write-Host "dotnet failed: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host 'Falling back to MSBuild Publish...' -ForegroundColor Yellow
    }

    if (-not $dotnetOk) {
        $publishProps = "Configuration=$Configuration;Platform=AnyCPU;RuntimeIdentifier=$($cfg.Rid);SelfContained=true;PublishDir=$($cfg.PublishDir)"
        Invoke-External 'MSBuild Publish' {
            & $msbuild $AppProject /t:Publish /p:$publishProps /v:m
        }
    }

    if (-not (Test-Path $publishExe)) {
        throw @"
OtcDataService.exe not found at:
  $publishExe

Build or publish OtcDataService ($Arch) first.
"@
    }

    Write-Host "App output OK ($Arch): $publishExe"
    return $publishExe
}

function Build-Msi([string]$Arch) {
    $cfg = $PublishConfig[$Arch]
    $msiPath = Join-Path $RepoRoot "OtcDataService.Setup\bin\$Configuration\$Arch\$($cfg.MsiName)"

    Write-Step "Building MSI ($Configuration | $Arch)"

    Invoke-External "MSBuild WiX setup project ($Arch)" {
        & $msbuild $SetupProject /t:Rebuild /p:Configuration=$Configuration /p:Platform=$Arch /v:q /nologo
    }

    if (-not (Test-Path $msiPath)) {
        throw "MSI not found at expected path: $msiPath"
    }

    return $msiPath
}

Write-Step 'Checking prerequisites'

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw 'vswhere.exe not found. Install Visual Studio with MSBuild workload.'
}

$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) {
    throw 'MSBuild.exe not found. Install Visual Studio with MSBuild workload.'
}
Write-Host "MSBuild: $msbuild"

$wixHeat = Join-Path ${env:ProgramFiles(x86)} 'WiX Toolset v3.14\bin\heat.exe'
if (-not (Test-Path $wixHeat)) {
    throw @"
WiX Toolset 3.14 not found at: $wixHeat
Download and install: https://wixtoolset.org/releases/v3.14/stable
"@
}
Write-Host "WiX:     $(Split-Path $wixHeat -Parent)"

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw 'dotnet CLI not found. Install .NET SDK.'
}
Write-Host "dotnet:  $($dotnet.Source)"

if (-not (Test-Path $AppProject)) {
    throw "App project not found: $AppProject"
}
if (-not (Test-Path $SetupProject)) {
    throw "Setup project not found: $SetupProject"
}

$AppVersion = Get-VersionFromCsproj $AppProject
Write-Host "Version: $AppVersion"
Write-Host "Targets: $($PlatformTargets -join ', ')"

if ($Clean) {
    Write-Step 'Cleaning'
    $pathsToClean = @(
        (Join-Path $RepoRoot 'OtcDataService\bin'),
        (Join-Path $RepoRoot 'OtcDataService\obj'),
        (Join-Path $RepoRoot 'OtcDataService.Setup\bin'),
        (Join-Path $RepoRoot 'OtcDataService.Setup\obj'),
        (Join-Path $RepoRoot '_publish_test'),
        $OutputDirFull
    )
    foreach ($p in $pathsToClean) {
        if (Test-Path $p) {
            Remove-Item $p -Recurse -Force
            Write-Host "Removed: $p"
        }
    }
}

$builtMsis = @()

foreach ($arch in $PlatformTargets) {
    if (-not $SkipAppPublish) {
        Write-Step "Publishing application ($Configuration | $arch)"
        Publish-App $arch | Out-Null
    } else {
        Write-Host "Skipped app publish for $arch (-SkipAppPublish)."
        $publishExe = Join-Path $PublishConfig[$arch].PublishDir 'OtcDataService.exe'
        if (-not (Test-Path $publishExe)) {
            throw "OtcDataService.exe not found for $arch at: $publishExe"
        }
    }

    Build-Msi $arch | Out-Null
    $msiPath = Join-Path $RepoRoot "OtcDataService.Setup\bin\$Configuration\$arch\$($PublishConfig[$arch].MsiName)"
    $builtMsis += [PSCustomObject]@{
        Arch   = $arch
        Source = $msiPath
    }
}

Write-Step 'Copying MSI files to publish folder'

if (-not (Test-Path $OutputDirFull)) {
    New-Item -ItemType Directory -Path $OutputDirFull -Force | Out-Null
}

foreach ($item in $builtMsis) {
    $msiBase = [System.IO.Path]::GetFileNameWithoutExtension($item.Source)
    $msiVersioned = Join-Path $OutputDirFull "$msiBase-$AppVersion.msi"
    $msiLatest = Join-Path $OutputDirFull ([System.IO.Path]::GetFileName($item.Source))

    Copy-Item $item.Source $msiVersioned -Force
    Copy-Item $item.Source $msiLatest -Force

    $msiInfo = Get-Item $msiLatest
    Write-Host "$($item.Arch): $msiLatest ($([math]::Round($msiInfo.Length / 1MB, 2)) MB)"
}

Write-Step 'Done'
Write-Host ''
Write-Host 'Build completed successfully.' -ForegroundColor Green

exit 0
