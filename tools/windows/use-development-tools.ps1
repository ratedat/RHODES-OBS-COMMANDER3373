# Select optional project-local tools for this PowerShell and its child processes.
# This does not install tools or change the user's permanent PATH.
$developmentRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$developmentTools = Join-Path $developmentRoot 'outputs\development-tools'
$developmentSdk = (Get-Content -LiteralPath (Join-Path $developmentRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
$developmentDotnet = Join-Path $developmentTools "dotnet-$developmentSdk"
$developmentNodeVersion = (Get-Content -LiteralPath (Join-Path $developmentRoot '.node-version') -Raw).Trim()
$developmentNode = Join-Path $developmentTools "node-v$developmentNodeVersion-win-x64"
$developmentPaths = @()
if ((Test-Path -LiteralPath (Join-Path $developmentDotnet 'dotnet.exe') -PathType Leaf) -and
    (Test-Path -LiteralPath (Join-Path $developmentDotnet "sdk\$developmentSdk") -PathType Container)) {
    $developmentPaths += $developmentDotnet
    $env:DOTNET_ROOT = $developmentDotnet
    $env:DOTNET_ROOT_X64 = $developmentDotnet
}
if (Test-Path -LiteralPath (Join-Path $developmentNode 'node.exe') -PathType Leaf) {
    $developmentPaths += $developmentNode
}
if ($developmentPaths.Count -gt 0) {
    $env:PATH = (@($developmentPaths) + @($env:PATH -split ';' | Where-Object { $_ -notin $developmentPaths })) -join ';'
}
