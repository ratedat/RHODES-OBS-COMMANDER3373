import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { prepareBuildAssets } from '../tools/prepare-build-assets.mjs';

async function fixture(t, assetPath = 'models/test.onnx') {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'rhodes-assets-'));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const bytes = Buffer.from('artificial model fixture');
  await fs.mkdir(path.join(root, 'data/recognition'), { recursive: true });
  await fs.writeFile(path.join(root, 'data/recognition/maa-build-assets.lock.json'), JSON.stringify({ schemaVersion: 1, assets: [{ path: assetPath, size: bytes.length, sha256: createHash('sha256').update(bytes).digest('hex'), url: `https://api.github.com/repos/MaaAssistantArknights/MaaAssistantArknights/git/blobs/${'a'.repeat(40)}` }] }));
  return { root, bytes, target: path.join(root, assetPath) };
}

test('asset check reports missing without fetching or creating files', async t => {
  const { root, target } = await fixture(t);
  const result = await prepareBuildAssets({ root, fetcher: () => assert.fail('network access') });
  assert.equal(result.ok, false);
  assert.equal(result.assets[0].status, 'MISSING');
  await assert.rejects(fs.stat(target), { code: 'ENOENT' });
});
test('explicit fetch validates hash and a later check uses local bytes', async t => {
  const { root, bytes, target } = await fixture(t);
  let calls = 0;
  const result = await prepareBuildAssets({ root, fetchMissing: true, fetcher: async () => { calls++; return new Response(bytes); } });
  assert.equal(result.ok, true);
  assert.deepEqual(await fs.readFile(target), bytes);
  assert.equal((await prepareBuildAssets({ root, fetchMissing: true, fetcher: () => assert.fail('unexpected fetch') })).ok, true);
  assert.equal(calls, 1);
});
test('bad downloads leave no model and existing mismatches are preserved', async t => {
  const { root, target } = await fixture(t);
  await assert.rejects(prepareBuildAssets({ root, fetchMissing: true, fetcher: async () => new Response('wrong') }), /hash mismatch/);
  await assert.rejects(fs.stat(target), { code: 'ENOENT' });
  await fs.mkdir(path.dirname(target), { recursive: true });
  await fs.writeFile(target, 'existing');
  const result = await prepareBuildAssets({ root, fetchMissing: true, fetcher: () => assert.fail('mismatch must not fetch') });
  assert.equal(result.assets[0].status, 'MISMATCH');
  assert.equal(await fs.readFile(target, 'utf8'), 'existing');
});
test('asset manifest cannot traverse outside the checkout', async t => {
  const { root } = await fixture(t, '../escape.onnx');
  await assert.rejects(prepareBuildAssets({ root }), /Invalid asset path/);
});
test('asset path cannot write through a directory junction', async t => {
  const { root } = await fixture(t);
  const outside = await fs.mkdtemp(path.join(os.tmpdir(), 'rhodes-asset-outside-'));
  t.after(() => fs.rm(outside, { recursive: true, force: true }));
  await fs.symlink(outside, path.join(root, 'models'), process.platform === 'win32' ? 'junction' : 'dir');
  await assert.rejects(prepareBuildAssets({ root, fetchMissing: true, fetcher: () => assert.fail('network access') }), /Redirected asset path/);
  assert.deepEqual(await fs.readdir(outside), []);
});
