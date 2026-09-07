import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, rmSync, statSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createPublicationGuard } from './publication-boundary.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const boundary = await createPublicationGuard(root);
const output = join(root, 'outputs', 'apple-design-prototype');
const project = join(root, 'apps', 'rhodes-apple-design', 'RhodesAppleDesign.csproj');
const executable = join(output, 'RhodesAppleDesignPrototype.exe');

const safeOutputRoot = resolve(root, 'outputs');
if (!resolve(output).startsWith(`${safeOutputRoot}\\`)) {
  throw new Error(`Refusing to clean output outside ${safeOutputRoot}`);
}

await boundary.assertDestination(output);
await boundary.checkGitWorktree();
for (const [directory, ignoreDirectoryNames] of [
  [join(root, 'apps', 'rhodes-apple-design'), ['bin', 'obj']],
  [join(root, 'assets', 'operators', 'wikiru', 'img'), []],
  [join(root, 'assets', 'relics', 'wikiru', 'img'), []],
  [join(root, 'assets', 'bosses', 'wikiru', 'img'), []],
  [join(root, 'assets', 'selectable-effects', 'is3_mizuki', 'hordeCall'), []],
]) {
  await boundary.checkTree(directory, {
    ignoreTopLevel: [],
    ignoreDirectoryNames,
    omitTransient: true,
  });
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

await boundary.checkTree(output, {
  ignoreTopLevel: [],
  ignoreDirectoryNames: [],
  omitTransient: false,
});

if (!existsSync(executable)) {
  throw new Error(`Published executable was not generated: ${executable}`);
}

const megabytes = (statSync(executable).size / 1024 / 1024).toFixed(1);
console.log(`Apple Design prototype: ${executable} (${megabytes} MB)`);
