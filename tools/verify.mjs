import fs from 'node:fs/promises';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
if (args.some(a => !['--desktop', '--no-restore'].includes(a))) {
  console.error('Usage: node tools/verify.mjs [--desktop] [--no-restore]');
  process.exit(1);
}
const noRestore = args.includes('--no-restore') ? ['--no-restore'] : [];
const stages = [];
if (!args.includes('--desktop')) stages.push(['Node tests', process.execPath, ['--test', 'tests/*.test.mjs']]);
for (const script of ['generate-maa-resource.mjs', 'update-maa-resource-hash.mjs', 'generate-maa-interface.mjs']) stages.push([script, process.execPath, [`tools/${script}`, '--check']]);
stages.push(['MAA contract', process.execPath, ['tools/check-maa-contract.mjs']]);
stages.push(['C# tests', 'dotnet', ['run', '--project', 'tests/rhodes-suki/RhodesSuki.ServiceTests.csproj', ...noRestore, '-p:RestoreLockedMode=true']]);
stages.push(['Desktop build', 'dotnet', ['build', 'apps/rhodes-suki/RhodesSuki.csproj', ...noRestore, '-p:RestoreLockedMode=true']]);
const startedAt = new Date().toISOString();
const results = [];
for (const [name, executable, arguments_] of stages) {
  console.log(`\n[verify] ${name}`);
  const started = Date.now();
  const result = spawnSync(executable, arguments_, { cwd: root, stdio: 'inherit', windowsHide: true });
  results.push({ name, exitCode: result.status ?? 1, durationMs: Date.now() - started, ...(result.error ? { error: result.error.message } : {}) });
  if (result.error || result.status !== 0) break;
}
const ok = results.length === stages.length && results.every(r => r.exitCode === 0);
const artifact = 'apps/rhodes-suki/bin/Debug/net8.0/RhodesSuki.exe';
const sha256 = ok ? createHash('sha256').update(await fs.readFile(path.join(root, artifact))).digest('hex') : null;
const assemblyPath = 'apps/rhodes-suki/bin/Debug/net8.0/RhodesSuki.dll';
const assemblySha256 = ok ? createHash('sha256').update(await fs.readFile(path.join(root, assemblyPath))).digest('hex') : null;
const report = { startedAt, finishedAt: new Date().toISOString(), ok, scope: args.includes('--desktop') ? 'desktop' : 'all', results, artifact: ok ? { path: artifact, sha256, assemblyPath, assemblySha256 } : null, uiVerified: false };
await fs.mkdir(path.join(root, 'outputs/verification'), { recursive: true });
await fs.writeFile(path.join(root, 'outputs/verification/latest.json'), `${JSON.stringify(report, null, 2)}\n`);
console.log(`\n[verify] ${ok ? 'PASS' : 'FAIL'}; report: outputs/verification/latest.json; UI verification is separate.`);
process.exitCode = ok ? 0 : 1;
