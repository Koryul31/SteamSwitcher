# Compile SteamSwitcher.exe with the compiler included in Windows.
# No installation or administrator permission is required.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root 'SteamSwitcher.cs'
$manifest = Join-Path $root 'app.manifest'
$icon = Join-Path $root 'SteamSwitcher.ico'
$output = Join-Path $root 'SteamSwitcher.exe'

$cscCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)

$csc = $cscCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $csc) {
    throw 'csc.exe (.NET Framework 4) was not found.'
}

if (-not (Test-Path -LiteralPath $source)) {
    throw "Source file not found: $source"
}

$arguments = @(
    '/nologo'
    '/target:winexe'
    '/optimize+'
    '/debug-'
    '/highentropyva+'
    '/filealign:512'
    '/codepage:65001'
    "/out:$output"
    "/win32manifest:$manifest"
    "/win32icon:$icon"
    '/reference:System.dll'
    '/reference:System.Core.dll'
    '/reference:System.Windows.Forms.dll'
    '/reference:System.Drawing.dll'
    $source
)

& $csc $arguments

if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Compiled successfully: $output"
