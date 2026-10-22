param(
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path $root 'src\SteamSwitcher.Setup\SteamSwitcher.Setup.csproj'
$solution = Join-Path $root 'SteamSwitcher.Setup.sln'

$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = 'dotnet'
}

& $dotnet build $solution -c Debug --nologo
if ($LASTEXITCODE -ne 0) {
    throw 'The build failed.'
}

if ($Publish) {
    $publishDir = Join-Path $root 'dist\SteamSwitcher.Setup'
    & $dotnet publish $project -c Release -r win-x64 --self-contained false -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        throw 'Publishing failed.'
    }
    Write-Host "Published to: $publishDir"
}

Write-Host ''
Write-Host 'Executable:'
Write-Host (Join-Path $root 'src\SteamSwitcher.Setup\bin\Debug\net9.0-windows\SteamSwitcher.Setup.exe')
