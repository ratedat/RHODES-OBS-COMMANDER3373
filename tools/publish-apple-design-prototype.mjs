import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, rmSync, statSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const output = join(root, 'outputs', 'apple-design-prototype');
const project = join(root, 'apps', 'rhodes-apple-design', 'RhodesAppleDesign.csproj');
const executable = join(output, 'RhodesAppleDesignPrototype.exe');

const safeOutputRoot = resolve(root, 'outputs');
if (!resolve(output).startsWith(`${safeOutputRoot}\\`)) {
  throw new Error(`Refusing to clean output outside ${safeOutputRoot}`);
}

rmSync(output, { recursive: true, force: true });
mkdirSync(output, { recursive: true });

const result = spawnSync(
  'dotnet',
  [
    'publish',
    project,
    '-c',
    'Release',
    '-r',
    'win-x64',
    '--self-contained',
    'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    '-o',
    output,
  ],
  { cwd: root, encoding: 'utf8', stdio: 'inherit' },
);

if (result.status !== 0) {
  process.exit(result.status ?? 1);
}

for (const name of readdirSync(output)) {
  if (name.toLowerCase().endsWith('.pdb')) {
    rmSync(join(output, name), { force: true });
  }
}

if (!existsSync(executable)) {
  throw new Error(`Published executable was not generated: ${executable}`);
}

const megabytes = (statSync(executable).size / 1024 / 1024).toFixed(1);
console.log(`Apple Design prototype: ${executable} (${megabytes} MB)`);
