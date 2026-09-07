import { createHash, randomUUID } from 'node:crypto';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const defaultRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const hash = (bytes) => createHash('sha256').update(bytes).digest('hex');

async function safeTarget(root, relative) {
  if (typeof relative !== 'string' || relative.includes('\\') || path.isAbsolute(relative) || relative.split('/').some(p => !p || p === '..' || p === '.')) {
    throw new Error('Invalid asset path.');
  }
  let current = root;
  for (const part of relative.split('/')) {
    current = path.join(current, part);
    try {
      const stat = await fs.lstat(current);
      if (stat.isSymbolicLink()) throw new Error(`Redirected asset path: ${relative}`);
    } catch (error) { if (error.code !== 'ENOENT') throw error; }
  }
  return current;
}

export async function prepareBuildAssets({ root = defaultRoot, fetchMissing = false, fetcher = fetch } = {}) {
  root = await fs.realpath(root);
  const manifest = JSON.parse(await fs.readFile(path.join(root, 'data/recognition/maa-build-assets.lock.json'), 'utf8'));
  if (manifest.schemaVersion !== 1 || !Array.isArray(manifest.assets) || !manifest.assets.length) throw new Error('Invalid asset lock file.');
  const results = [];
  for (const asset of manifest.assets) {
    if (!/^[a-f0-9]{64}$/.test(asset.sha256) || !Number.isSafeInteger(asset.size) || asset.size < 1 ||
        !/^https:\/\/api\.github\.com\/repos\/MaaAssistantArknights\/MaaAssistantArknights\/git\/blobs\/[a-f0-9]{40}$/.test(asset.url)) {
      throw new Error('Invalid locked asset metadata.');
    }
    const target = await safeTarget(root, asset.path);
    let bytes;
    try { bytes = await fs.readFile(target); } catch (error) { if (error.code !== 'ENOENT') throw error; }
    if (!bytes && fetchMissing) {
      const response = await fetcher(asset.url, { headers: { Accept: 'application/vnd.github.raw+json' }, signal: AbortSignal.timeout(120000) });
      if (!response.ok) throw new Error(`Asset download failed: HTTP ${response.status} (${asset.path})`);
      bytes = Buffer.from(await response.arrayBuffer());
      if (bytes.length !== asset.size || hash(bytes) !== asset.sha256) throw new Error(`Downloaded asset hash mismatch: ${asset.path}`);
      await fs.mkdir(path.dirname(target), { recursive: true });
      await safeTarget(root, asset.path);
      const temporary = `${target}.${randomUUID()}.tmp`;
      try {
        await fs.writeFile(temporary, bytes, { flag: 'wx' });
        // A concurrent writer must not be silently replaced.
        await fs.copyFile(temporary, target, (await import('node:fs')).constants.COPYFILE_EXCL);
      } finally { await fs.rm(temporary, { force: true }); }
    }
    const status = !bytes ? 'MISSING' : bytes.length === asset.size && hash(bytes) === asset.sha256 ? 'OK' : 'MISMATCH';
    results.push({ path: asset.path, status, expectedSha256: asset.sha256 });
  }
  return { ok: results.every(r => r.status === 'OK'), assets: results };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const args = process.argv.slice(2);
    if (args.some(arg => !['--check', '--fetch', '--json'].includes(arg)) || (args.includes('--check') && args.includes('--fetch'))) throw new Error('Usage: node tools/prepare-build-assets.mjs [--check | --fetch] [--json]');
    const result = await prepareBuildAssets({ fetchMissing: args.includes('--fetch') });
    console.log(args.includes('--json') ? JSON.stringify(result, null, 2) : result.assets.map(a => `${a.status} ${a.path}`).join('\n'));
    if (!result.ok) console.error('For missing files: npm run assets:fetch. For mismatches, preserve and inspect the existing file before replacing it.');
    process.exitCode = result.ok ? 0 : 1;
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}
