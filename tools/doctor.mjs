import fs from 'node:fs/promises';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { prepareBuildAssets } from './prepare-build-assets.mjs';

const defaultRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
export async function diagnoseEnvironment({ root = defaultRoot } = {}) {
  const checks = [];
  const check = async (name, inspect, remedy) => {
    try { checks.push({ name, status: 'OK', detail: await inspect() }); }
    catch (error) { checks.push({ name, status: 'MISSING', detail: error.message, remedy }); }
  };
  const command = (program, args) => execFileSync(program, args, { cwd: root, encoding: 'utf8', windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] }).trim();
  await check('Node.js', async () => {
    const expected = (await fs.readFile(path.join(root, '.node-version'), 'utf8')).trim();
    if (process.versions.node !== expected) throw new Error(`Expected ${expected}; actual ${process.versions.node}`);
    return expected;
  }, 'Install the Node.js version in .node-version.');
  await check('.NET SDK', async () => {
    const expected = JSON.parse(await fs.readFile(path.join(root, 'global.json'), 'utf8')).sdk.version;
    const actual = command('dotnet', ['--version']);
    if (actual !== expected) throw new Error(`Expected ${expected}; actual ${actual}`);
    return actual;
  }, 'Install the SDK in global.json.');
  await check('.NET 8 runtime', async () => {
    const runtime = command('dotnet', ['--list-runtimes']).split(/\r?\n/).filter(line => /^Microsoft\.NETCore\.App 8\./.test(line));
    if (!runtime.length) throw new Error('Microsoft.NETCore.App 8.x is required to run the desktop app and C# tests.');
    return runtime.map(line => line.split(' [')[0]).join(', ');
  }, 'Install the .NET 8 runtime for the machine architecture.');
  await check('Dependency locks', async () => {
    for (const relative of ['package-lock.json', 'apps/rhodes-suki/packages.lock.json', 'tests/rhodes-suki/packages.lock.json']) JSON.parse(await fs.readFile(path.join(root, relative), 'utf8'));
    return 'npm and both .NET lock files are present.';
  }, 'Restore the tracked dependency lock files.');
  await check('Build assets', async () => {
    const result = await prepareBuildAssets({ root });
    if (!result.ok) throw new Error(result.assets.filter(a => a.status !== 'OK').map(a => `${a.status} ${a.path}`).join('; '));
    return `${result.assets.length} locked assets match SHA-256.`;
  }, 'npm run assets:fetch (missing assets only); inspect mismatches before replacing.');
  await check('Initial state template', async () => {
    JSON.parse(await fs.readFile(path.join(root, 'data/overlay-state.example.json'), 'utf8'));
    return 'Tracked template is available; current-state.json is not required for a build.';
  }, 'Restore data/overlay-state.example.json.');
  await check('Publication hooks', async () => command(process.execPath, [path.join(root, 'tools/install-publication-hooks.mjs'), '--check', '--root', root]), 'npm run hooks:install; preserve any required local policy.');
  return { ok: checks.every(c => c.status === 'OK'), checks };
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const args = process.argv.slice(2);
  if (args.some(arg => arg !== '--json')) { console.error('Usage: node tools/doctor.mjs [--json]'); process.exitCode = 1; }
  else {
    const result = await diagnoseEnvironment();
    console.log(args.includes('--json') ? JSON.stringify(result, null, 2) : result.checks.map(c => `${c.status} ${c.name}: ${c.detail}${c.remedy ? `\n  ${c.remedy}` : ''}`).join('\n'));
    process.exitCode = result.ok ? 0 : 1;
  }
}
