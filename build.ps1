param(
    [switch]$Publish,
    [switch]$SelfContained,
    [switch]$Zip
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$solution = Join-Path $root 'SteamSwitcher.Setup.sln'
$testProject = Join-Path $root 'tests\SteamSwitcher.Core.Tests\SteamSwitcher.Core.Tests.csproj'
$setupProject = Join-Path $root 'src\SteamSwitcher.Setup\SteamSwitcher.Setup.csproj'
$cliProject = Join-Path $root 'src\SteamSwitcher.Cli\SteamSwitcher.Cli.csproj'

$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = 'dotnet'
}

Write-Host "Building SteamSwitcher 2.0 Solution..." -ForegroundColor Cyan
& $dotnet build $solution -c Debug --nologo
if ($LASTEXITCODE -ne 0) {
    throw 'The build failed.'
}

Write-Host "Running Unit Tests..." -ForegroundColor Cyan
& $dotnet test $testProject -c Debug --no-build --nologo
if ($LASTEXITCODE -ne 0) {
    throw 'Unit tests failed.'
}

if ($Publish) {
    $publishDir = Join-Path $root 'dist\SteamSwitcher'
    if (Test-Path -LiteralPath $publishDir) {
        Remove-Item -Recurse -Force $publishDir
    }

    Write-Host "Publishing Setup application (framework-dependent)..." -ForegroundColor Cyan
    & $dotnet publish $setupProject -c Release -r win-x64 --self-contained false -o $publishDir --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'Publishing Setup failed.'
    }

    Write-Host "Publishing CLI / Shortcut Runner..." -ForegroundColor Cyan
    & $dotnet publish $cliProject -c Release -r win-x64 --self-contained false -o $publishDir --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'Publishing CLI failed.'
    }

    Write-Host "Published successfully to: $publishDir" -ForegroundColor Green
}

if ($SelfContained) {
    $scDir = Join-Path $root 'dist\SteamSwitcher-2.0.0-portable'
    if (Test-Path -LiteralPath $scDir) {
        Remove-Item -Recurse -Force $scDir
    }

    Write-Host "Publishing Self-Contained Setup application..." -ForegroundColor Cyan
    & $dotnet publish $setupProject -c Release -r win-x64 --self-contained true -o $scDir --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'Publishing Self-Contained Setup failed.'
    }

    Write-Host "Publishing Self-Contained CLI Runner..." -ForegroundColor Cyan
    & $dotnet publish $cliProject -c Release -r win-x64 --self-contained true -o $scDir --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'Publishing Self-Contained CLI failed.'
    }

    if ($Zip) {
        $zipPath = Join-Path $root 'dist\SteamSwitcher-2.0.0-portable.zip'
        if (Test-Path -LiteralPath $zipPath) {
            Remove-Item -Force $zipPath
        }
        Write-Host "Compressing to $zipPath..." -ForegroundColor Cyan
        Compress-Archive -Path "$scDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
        Write-Host "Created portable archive: $zipPath" -ForegroundColor Green
    }
}

Write-Host ''
Write-Host 'Setup Executable:' -ForegroundColor Yellow
Write-Host (Join-Path $root 'src\SteamSwitcher.Setup\bin\Debug\net9.0-windows\SteamSwitcher.Setup.exe')
Write-Host 'Runner Executable:' -ForegroundColor Yellow
Write-Host (Join-Path $root 'src\SteamSwitcher.Cli\bin\Debug\net9.0-windows\SteamSwitcher.exe')
