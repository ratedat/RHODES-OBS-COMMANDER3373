import assert from "node:assert/strict";
import { mkdtemp, mkdir, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import test from "node:test";
import { fileURLToPath } from "node:url";

const repoRoot = fileURLToPath(new URL("../", import.meta.url));
const helperPath = path.join(repoRoot, "tools", "windows", "rhodes-launch-helpers.ps1");
const launcherPath = path.join(repoRoot, "tools", "windows", "start-app.ps1");

const driver = String.raw`
$ErrorActionPreference = 'Stop'
. $env:RHODES_LAUNCH_HELPER

$events = [System.Collections.Generic.List[object]]::new()
$root = $env:RHODES_FIXTURE_ROOT
$exe = Join-Path $root 'apps\rhodes-suki\bin\Debug\net8.0\RhodesSuki.exe'
if ($env:RHODES_EXE_EXISTS -eq '1') {
    New-Item -ItemType Directory -Path (Split-Path -Parent $exe) -Force | Out-Null
    [System.IO.File]::WriteAllText($exe, 'artificial executable fixture')
}

$runBuild = {
    param([string]$FilePath, [string[]]$ArgumentList, [string]$WorkingDirectory)
    $events.Add([ordered]@{
        kind = 'build'
        filePath = $FilePath
        arguments = @($ArgumentList)
        workingDirectory = $WorkingDirectory
    }) | Out-Null
    return [int]$env:RHODES_BUILD_EXIT_CODE
}
$startApplication = {
    param([string]$FilePath, [string]$WorkingDirectory)
    $events.Add([ordered]@{
        kind = 'launch'
        filePath = $FilePath
        workingDirectory = $WorkingDirectory
    }) | Out-Null
}

$launchParameters = @{
    Root = $root
    SmokeTest = ($env:RHODES_SMOKE_TEST -eq '1')
    NoRestore = ($env:RHODES_NO_RESTORE -eq '1')
    RunBuild = $runBuild
    StartApplication = $startApplication
}
$result = Invoke-RhodesSourceLaunch @launchParameters

[ordered]@{ result = $result; events = @($events) } | ConvertTo-Json -Depth 8 -Compress
`;

async function runScenario({
  rootName,
  buildExitCode = 0,
  exeExists = true,
  smokeTest = false,
  noRestore = false,
}) {
  const fixtureBase = await mkdtemp(path.join(tmpdir(), "rhodes launcher test "));
  const fixtureRoot = path.join(fixtureBase, rootName);
  const driverPath = path.join(fixtureBase, "launch driver.ps1");
  await mkdir(path.join(fixtureRoot, "apps", "rhodes-suki"), { recursive: true });
  await writeFile(driverPath, driver, "utf8");

  try {
    const result = spawnSync("pwsh", ["-NoLogo", "-NoProfile", "-NonInteractive", "-File", driverPath], {
      cwd: fixtureBase,
      encoding: "utf8",
      env: {
        ...process.env,
        RHODES_LAUNCH_HELPER: helperPath,
        RHODES_FIXTURE_ROOT: fixtureRoot,
        RHODES_BUILD_EXIT_CODE: String(buildExitCode),
        RHODES_EXE_EXISTS: exeExists ? "1" : "0",
        RHODES_SMOKE_TEST: smokeTest ? "1" : "0",
        RHODES_NO_RESTORE: noRestore ? "1" : "0",
      },
    });
    assert.equal(result.error, undefined, result.error?.message);
    assert.equal(result.status, 0, `${result.stdout}\n${result.stderr}`);
    return { fixtureRoot, output: JSON.parse(result.stdout.trim()) };
  } finally {
    await rm(fixtureBase, { recursive: true, force: true });
  }
}

test("source launch builds the current checkout before starting its executable", async () => {
  const { fixtureRoot, output } = await runScenario({ rootName: "checkout with spaces", exeExists: true });
  const project = path.join(fixtureRoot, "apps", "rhodes-suki", "RhodesSuki.csproj");
  const exe = path.join(fixtureRoot, "apps", "rhodes-suki", "bin", "Debug", "net8.0", "RhodesSuki.exe");

  assert.equal(output.result.ExitCode, 0);
  assert.equal(output.result.AppStarted, true);
  assert.deepEqual(output.events.map(({ kind }) => kind), ["build", "launch"]);
  assert.deepEqual(output.events[0], {
    kind: "build",
    filePath: "dotnet",
    arguments: ["build", project],
    workingDirectory: fixtureRoot,
  });
  assert.deepEqual(output.events[1], { kind: "launch", filePath: exe, workingDirectory: fixtureRoot });
});

test("a failed build never starts an existing executable", async () => {
  const { output } = await runScenario({ rootName: "failed build checkout", buildExitCode: 23, exeExists: true });

  assert.equal(output.result.ExitCode, 23);
  assert.equal(output.result.AppStarted, false);
  assert.deepEqual(output.events.map(({ kind }) => kind), ["build"]);
});

test("SmokeTest only builds and never launches or stops another process", async () => {
  const { output } = await runScenario({ rootName: "smoke checkout", smokeTest: true, exeExists: true });

  assert.equal(output.result.ExitCode, 0);
  assert.equal(output.result.AppStarted, false);
  assert.deepEqual(output.events.map(({ kind }) => kind), ["build"]);
});

test("NoRestore is forwarded as one build argument without breaking spaced roots", async () => {
  for (const rootName of ["first checkout", "second separate checkout"] ) {
    const { fixtureRoot, output } = await runScenario({ rootName, noRestore: true, exeExists: true });
    assert.deepEqual(output.events[0].arguments, [
      "build",
      path.join(fixtureRoot, "apps", "rhodes-suki", "RhodesSuki.csproj"),
      "--no-restore",
    ]);
    assert.equal(output.events[1].workingDirectory, fixtureRoot);
  }
});

test("a successful build without the expected checkout executable does not launch", async () => {
  const { output } = await runScenario({ rootName: "missing output checkout", exeExists: false });

  assert.notEqual(output.result.ExitCode, 0);
  assert.equal(output.result.AppStarted, false);
  assert.deepEqual(output.events.map(({ kind }) => kind), ["build"]);
});

test("the public launcher contains no global process-stop path", async () => {
  const [source, helpers] = await Promise.all([
    readFile(launcherPath, "utf8"),
    readFile(helperPath, "utf8"),
  ]);

  assert.doesNotMatch(`${source}\n${helpers}`, /Stop-Process|Get-CimInstance|Win32_Process|WScript\.Shell/u);
  assert.match(source, /rhodes-launch-helpers\.ps1/u);
});
