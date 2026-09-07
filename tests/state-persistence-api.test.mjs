import test, { after } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';

const stateDir = await fs.mkdtemp(path.join(os.tmpdir(), 'rhodes-state-api-'));
process.env.ARKNIGHTS_STATE_DIR = stateDir;
const { createAppServer } = await import('../app/server.mjs');
const statePath = path.join(stateDir, 'current-state.json');
after(() => fs.rm(stateDir, { recursive: true, force: true }));

async function withServer(run) {
  const remote = { sync: async () => {}, stop: async () => {} };
  const server = createAppServer({ tournamentRemoteHost: remote, tournamentQuickPublishManager: { stop: async () => {} } });
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolve);
  });
  try { await run(`http://127.0.0.1:${server.address().port}`); }
  finally { await new Promise(resolve => server.close(resolve)); }
}

test('concurrent first state reads initialize a single usable state', async () => {
  await fs.rm(statePath, { force: true });
  await withServer(async base => {
    const responses = await Promise.all(Array.from({ length: 12 }, () => fetch(`${base}/api/state`)));
    assert.deepEqual(responses.map(r => r.status), Array(12).fill(200));
    for (const response of responses) assert.equal((await response.json()).run.campaignId, 'is2_phantom');
  });
  assert.equal(JSON.parse(await fs.readFile(statePath, 'utf8')).run.campaignId, 'is2_phantom');
  assert.deepEqual((await fs.readdir(stateDir)).filter(name => name.endsWith('.tmp')), []);
});

test('unreadable JSON is reported without resetting or replacing the saved run', async () => {
  const damaged = '{"run":{"campaignId":"is5_sarkaz"},"relics":["preserve-this"],';
  await fs.writeFile(statePath, damaged);
  await withServer(async base => {
    for (const endpoint of ['/api/state', '/api/health']) {
      const response = await fetch(`${base}${endpoint}`);
      assert.equal(response.status, 503);
      const body = await response.json();
      assert.match(body.error, /保存状態/);
      assert.doesNotMatch(body.error, /preserve-this/);
      assert.equal(await fs.readFile(statePath, 'utf8'), damaged);
    }
  });
});

test('invalid saved state shapes are retained for recovery', async () => {
  await withServer(async base => {
    for (const invalid of ['null', '[]', '{"run":"invalid"}']) {
      await fs.writeFile(statePath, invalid);
      const response = await fetch(`${base}/api/state`);
      assert.equal(response.status, 503, invalid);
      await response.arrayBuffer();
      assert.equal(await fs.readFile(statePath, 'utf8'), invalid);
    }
  });
});

test('simultaneous saves never share a partial temporary file', async () => {
  await withServer(async base => {
    const states = Array.from({ length: 8 }, (_, id) => ({
      requestId: id,
      run: { campaignId: 'is5_sarkaz' },
      relics: [`test-relic-${id}`],
      payload: String(id).repeat(256 * 1024),
    }));
    const responses = await Promise.all(states.map(state => fetch(`${base}/api/state`, {
      method: 'PUT', headers: { 'content-type': 'application/json' }, body: JSON.stringify(state),
    })));
    const bodies = await Promise.all(responses.map(response => response.json()));
    assert.deepEqual(responses.map(r => r.status), Array(8).fill(200), JSON.stringify(bodies.map(body => body.error ?? null)));
    for (const [id, body] of bodies.entries()) {
      assert.equal(body.requestId, id);
      assert.equal(body.payload, states[id].payload);
    }
    const saved = JSON.parse(await fs.readFile(statePath, 'utf8'));
    assert.equal(saved.payload, states[saved.requestId].payload);
    assert.deepEqual(saved.relics, states[saved.requestId].relics);
  });
  assert.deepEqual((await fs.readdir(stateDir)).filter(name => name.endsWith('.tmp')), []);
});
