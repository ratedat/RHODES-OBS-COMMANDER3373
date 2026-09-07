import test, { after } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { randomUUID } from "node:crypto";
import { createTournamentRelayServer } from "../services/tournament-relay/server.mjs";
import { createTournamentRemoteHost } from "../app/domain/tournament-remote-host.js";

const stateDir = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-remote-recovery-"));
assert.equal(path.dirname(stateDir), path.resolve(os.tmpdir()));
process.env.ARKNIGHTS_STATE_DIR = stateDir;
const { createAppServer } = await import("../app/server.mjs");
after(() => fs.rm(stateDir, { recursive: true, force: true }));

async function listen(server) {
  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", resolve);
  });
  return `http://127.0.0.1:${server.address().port}`;
}

async function json(url, method = "GET", body) {
  const response = await fetch(url, {
    method,
    ...(body === undefined ? {} : { headers: { "content-type": "application/json" }, body: JSON.stringify(body) }),
  });
  const value = await response.json();
  assert.ok(response.ok, value.error || `HTTP ${response.status}`);
  return value;
}

async function withFixture(run, { defaultHost = false } = {}) {
  const relay = createTournamentRelayServer({ adminToken: "" });
  const relayUrl = await listen(relay);
  let appUrl;
  let saveCount = 0;
  let loseResult = false;
  const host = defaultHost ? null : createTournamentRemoteHost({
    autoPoll: false,
    fetchImpl: async (url, options) => {
      const response = await fetch(url, options);
      if (loseResult && new URL(url).pathname.endsWith("/result")) {
        loseResult = false;
        await response.arrayBuffer();
        throw new Error("artificial result response loss");
      }
      return response;
    },
    getState: () => json(`${appUrl}/api/state`),
    getMaster: async () => ({
      campaigns: [{ id: "is2_phantom", title: "Test campaign", specialFields: [], bossFlags: [] }],
      squads: [], relics: [], operators: [], performances: [], selectableEffects: [], difficultyTiers: {},
    }),
    saveState: async (state) => {
      await json(`${appUrl}/api/state`, "PUT", state);
      saveCount += 1;
    },
  });
  const app = createAppServer({
    ...(host ? { tournamentRemoteHost: host } : {}),
    tournamentQuickPublishManager: { status: async () => ({ active: false, starting: false }), stop: async () => {} },
  });
  try {
    appUrl = await listen(app);
    await json(`${appUrl}/api/state`, "PUT", {
      run: { campaignId: "is2_phantom", ingot: 10 },
      operators: [], relics: [], tournament: { recoverOnStartup: false },
    });
    const started = await json(`${appUrl}/api/tournament/remote/start`, "POST", { relayUrl, playerLabel: "Test player" });
    const endpoint = `${relayUrl}/api/sessions/${started.sessionId}`;
    const codeQuery = `code=${encodeURIComponent(started.editorCode)}`;
    await run({
      appUrl, host, started,
      state: () => json(`${appUrl}/api/state`),
      bootstrap: () => json(`${endpoint}/bootstrap?${codeQuery}`),
      submit: (body) => json(`${endpoint}/operations?${codeQuery}`, "POST", body),
      loseNextResult: () => { loseResult = true; },
      saveCount: () => saveCount,
    });
  } finally {
    if (appUrl) await json(`${appUrl}/api/tournament/remote/stop`, "POST").catch(() => {});
    await Promise.all([
      new Promise((resolve) => app.close(resolve)),
      new Promise((resolve) => relay.close(resolve)),
    ]);
  }
}

function update(value, editorClientId = randomUUID()) {
  return {
    clientOperationId: randomUUID(),
    editorClientId,
    operation: { type: "batch", operations: [{ type: "run.set", field: "ingot", value }] },
  };
}

test("the real local API retains tournament recovery after stopping the remote session", async () => {
  await withFixture(async (fixture) => {
    assert.equal((await fixture.state()).tournament.recoverOnStartup, true);
    assert.equal((await fixture.bootstrap()).snapshot.state.tournament, undefined);
    await json(`${fixture.appUrl}/api/tournament/remote/stop`, "POST");
    const saved = JSON.parse(await fs.readFile(path.join(stateDir, "current-state.json"), "utf8"));
    assert.equal(saved.tournament.recoverOnStartup, true);
    assert.equal(saved.run.ingot, 10);
    assert.equal((await json(`${fixture.appUrl}/api/tournament/remote/status`)).active, false);
  }, { defaultHost: true });
});

test("HTTP recovery preserves a newer correction after a result acknowledgement is lost", async () => {
  await withFixture(async (fixture) => {
    await fixture.submit(update(42));
    fixture.loseNextResult();
    await assert.rejects(fixture.host.pollNow(), /artificial result response loss/);
    assert.equal((await fixture.state()).run.ingot, 42);
    assert.equal(fixture.host.status().cursor, 0);
    assert.equal(fixture.host.status().appliedSequence, 1);

    const manual = await fixture.state();
    manual.run.ingot = 99;
    await json(`${fixture.appUrl}/api/state`, "PUT", manual);
    await fixture.host.sync();
    await fixture.host.pollNow();

    assert.equal(fixture.saveCount(), 1);
    assert.equal((await fixture.state()).run.ingot, 99);
    const current = await fixture.bootstrap();
    assert.equal(current.snapshot.state.run.ingot, 99);
    assert.equal(current.history.length, 1);
    assert.equal(current.history[0].status, "applied");
    assert.equal(fixture.host.status().cursor, 1);
  });
});

test("an editor retry cannot reapply an old edit after another editor corrects it", async () => {
  await withFixture(async (fixture) => {
    const original = update(42);
    const first = await fixture.submit(original);
    await fixture.host.pollNow();
    await fixture.submit(update(99));
    await fixture.host.pollNow();

    const retried = await fixture.submit(original);
    assert.equal(retried.id, first.id);
    assert.equal(retried.status, "applied");
    await fixture.host.pollNow();
    assert.equal(fixture.saveCount(), 2);
    assert.equal((await fixture.state()).run.ingot, 99);
    assert.equal((await fixture.bootstrap()).history.length, 2);
  });
});
