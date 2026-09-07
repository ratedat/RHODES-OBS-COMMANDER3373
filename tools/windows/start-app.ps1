param(
    [switch]$SmokeTest,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$helperPath = Join-Path $PSScriptRoot 'rhodes-launch-helpers.ps1'
. $helperPath

$dotnet = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $dotnet) {
    [Console]::Error.WriteLine('The .NET SDK was not found. See global.json and docs/development-setup.md, or use a packaged application.')
    exit 1
}

$runBuild = {
    param([string]$FilePath, [string[]]$ArgumentList, [string]$WorkingDirectory)

    Push-Location -LiteralPath $WorkingDirectory
    try {
        & $FilePath @ArgumentList | Out-Host
        return [int]$LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}
$startApplication = {
    param([string]$FilePath, [string]$WorkingDirectory)

    Start-Process -FilePath $FilePath -WorkingDirectory $WorkingDirectory | Out-Null
}

try {
    $result = Invoke-RhodesSourceLaunch `
        -Root $root `
        -SmokeTest:$SmokeTest `
        -NoRestore:$NoRestore `
        -DotnetPath $dotnet.Source `
        -RunBuild $runBuild `
        -StartApplication $startApplication
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}

if ($result.ExitCode -ne 0) {
    [Console]::Error.WriteLine($result.Message)
}
exit [int]$result.ExitCode
