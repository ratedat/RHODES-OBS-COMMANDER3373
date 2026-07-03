param(
  [switch]$SmokeTest
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSCommandPath))
$shell = New-Object -ComObject WScript.Shell
Set-Location $root

function Stop-StaleLocalServers {
  $servers = Get-CimInstance Win32_Process -Filter "name = 'node.exe'" |
    Where-Object { $_.CommandLine -match 'app[\\/]server\.mjs --port (5173|5174|5200)' }

  foreach ($server in $servers) {
    try {
      Stop-Process -Id $server.ProcessId -Force -ErrorAction Stop
    } catch {
      # Best effort only. A stale process should not block the normal launcher path.
    }
  }
}
function Show-Message($message, $title = "RHODES OBS COMMANDER3373", $icon = 64) {
  $shell.Popup($message, 0, $title, $icon) | Out-Null
}

Stop-StaleLocalServers

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  Show-Message ".NET SDK/Runtime が見つかりません。配布版の exe を使うか、開発用に .NET 8 SDK をインストールしてください。" "起動できません" 16
  exit 1
}

$project = Join-Path $root "apps\rhodes-suki\RhodesSuki.csproj"
$exe = Join-Path $root "apps\rhodes-suki\bin\Debug\net8.0\RhodesSuki.exe"

if ($SmokeTest) {
  $run = Start-Process -FilePath "dotnet" -ArgumentList @("build", $project) -WorkingDirectory $root -Wait -PassThru -WindowStyle Hidden
  exit $run.ExitCode
}

if (-not (Test-Path $exe)) {
  $shell.Popup("Suki/Avaloniaアプリをビルドしています。完了するとアプリが起動します。", 5, "RHODES OBS COMMANDER3373", 64) | Out-Null
  $build = Start-Process -FilePath "dotnet" -ArgumentList @("build", $project) -WorkingDirectory $root -Wait -PassThru -WindowStyle Hidden
  if ($build.ExitCode -ne 0) {
    Show-Message "Suki/Avaloniaアプリのビルドに失敗しました。.NET 8 SDK とリポジトリ状態を確認してください。" "ビルド失敗" 16
    exit $build.ExitCode
  }
}

Start-Process -FilePath $exe -WorkingDirectory $root
