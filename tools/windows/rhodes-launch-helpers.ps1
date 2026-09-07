function New-RhodesSourceLaunchPlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [switch]$NoRestore
    )

    $resolvedRoot = [System.IO.Path]::GetFullPath($Root)
    $projectPath = Join-Path $resolvedRoot 'apps\rhodes-suki\RhodesSuki.csproj'
    $executablePath = Join-Path $resolvedRoot 'apps\rhodes-suki\bin\Debug\net8.0\RhodesSuki.exe'
    $buildArguments = @('build', $projectPath)
    if ($NoRestore) {
        $buildArguments += '--no-restore'
    }

    [pscustomobject]@{
        Root = $resolvedRoot
        ProjectPath = $projectPath
        ExecutablePath = $executablePath
        BuildArguments = $buildArguments
    }
}

function Invoke-RhodesSourceLaunch {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [switch]$SmokeTest,
        [switch]$NoRestore,
        [string]$DotnetPath = 'dotnet',
        [Parameter(Mandatory = $true)]
        [scriptblock]$RunBuild,
        [Parameter(Mandatory = $true)]
        [scriptblock]$StartApplication
    )

    $plan = New-RhodesSourceLaunchPlan -Root $Root -NoRestore:$NoRestore
    [int]$buildExitCode = & $RunBuild `
        -FilePath $DotnetPath `
        -ArgumentList $plan.BuildArguments `
        -WorkingDirectory $plan.Root

    if ($buildExitCode -ne 0) {
        return [pscustomobject]@{
            ExitCode = $buildExitCode
            BuildSucceeded = $false
            AppStarted = $false
            Root = $plan.Root
            ProjectPath = $plan.ProjectPath
            ExecutablePath = $plan.ExecutablePath
            Message = "dotnet build failed with exit code $buildExitCode."
        }
    }

    if ($SmokeTest) {
        return [pscustomobject]@{
            ExitCode = 0
            BuildSucceeded = $true
            AppStarted = $false
            Root = $plan.Root
            ProjectPath = $plan.ProjectPath
            ExecutablePath = $plan.ExecutablePath
            Message = 'Build verification succeeded.'
        }
    }

    if (-not (Test-Path -LiteralPath $plan.ExecutablePath -PathType Leaf)) {
        return [pscustomobject]@{
            ExitCode = 2
            BuildSucceeded = $true
            AppStarted = $false
            Root = $plan.Root
            ProjectPath = $plan.ProjectPath
            ExecutablePath = $plan.ExecutablePath
            Message = "Build succeeded, but the expected executable was not found: $($plan.ExecutablePath)"
        }
    }

    $null = & $StartApplication -FilePath $plan.ExecutablePath -WorkingDirectory $plan.Root
    return [pscustomobject]@{
        ExitCode = 0
        BuildSucceeded = $true
        AppStarted = $true
        Root = $plan.Root
        ProjectPath = $plan.ProjectPath
        ExecutablePath = $plan.ExecutablePath
        Message = 'Build succeeded and the current checkout executable was started.'
    }
}
