import test from "node:test";
import assert from "node:assert/strict";

import { createTournamentRemoteHost } from "../app/domain/tournament-remote-host.js";

function response(status, value) {
  return new Response(value === null ? null : JSON.stringify(value), {
    status,
    headers: { "content-type": "application/json" },
  });
}

function createRelayFetch() {
  const calls = [];
  const operations = [];
  const fetchImpl = async (url, options = {}) => {
    const parsed = new URL(url);
    const body = options.body ? JSON.parse(options.body) : null;
    calls.push({
      url: parsed,
      method: options.method || "GET",
      headers: new Headers(options.headers),
      body,
    });

    if (parsed.pathname === "/api/sessions" && options.method === "POST") {
      return response(201, {
        sessionId: "session-1",
        hostToken: "host-secret",
        editorCode: "ABC123",
        inputUrl: "/input/session-1?code=ABC123",
        expiresAt: 123456789,
      });
    }
    if (parsed.pathname === "/api/sessions/session-1/snapshot" && options.method === "PUT") {
      return response(200, { updatedAt: 1000 });
    }
    if (parsed.pathname === "/api/sessions/session-1/operations" && (options.method || "GET") === "GET") {
      const after = Math.max(0, Number(parsed.searchParams.get("after")) || 0);
      return response(200, { operations: structuredClone(operations.filter((item) => item.sequence > after)) });
    }
    if (/\/operations\/[^/]+\/result$/.test(parsed.pathname) && options.method === "POST") {
      return response(200, { ok: true });
    }
    if (parsed.pathname === "/api/sessions/session-1" && options.method === "DELETE") {
      return response(204, null);
    }
    return response(404, { error: "not found" });
  };
  return { fetchImpl, calls, operations };
}

function baseState() {
  return {
    version: 1,
    updatedAt: "2026-07-30T00:00:00.000Z",
    run: {
      campaignId: "campaign-a",
      ingot: 10,
      special: { "campaign-a": {} },
    },
    operators: [],
    operatorCounts: {},
    relics: [],
    usedRelicIds: [],
    bossFlags: [],
    bossSelections: {},
    adb: { serial: "private-device" },
    preferences: { localOnly: true },
  };
}

function baseMaster() {
  return {
    campaigns: [{ id: "campaign-a", title: "Campaign A", specialFields: [], bossFlags: [] }],
    squads: [],
    operators: [],
    relics: [],
    performances: [],
    selectableEffects: [],
    difficultyTiers: {},
  };
}

function deferred() {
  let resolve;
  const promise = new Promise((done) => { resolve = done; });
  return { promise, resolve };
}

test("remote host retries a failed state sync after relay polling recovers", async () => {
  const relay = createRelayFetch();
  let state = baseState();
  let failSnapshot = false;
  const host = createTournamentRemoteHost({
    fetchImpl: async (url, options) => {
      if (failSnapshot && new URL(url).pathname.endsWith("/snapshot")) {
        failSnapshot = false;
        throw new Error("temporary sync failure");
      }
      return relay.fetchImpl(url, options);
    },
    getState: async () => state,
    getMaster: async () => baseMaster(),
    saveState: async (next) => { state = next; },
    autoPoll: false,
  });
  await host.start({ relayUrl: "https://relay.example.test" });
  state = { ...state, run: { ...state.run, ingot: 99 } };
  failSnapshot = true;
  await assert.rejects(host.sync(), /temporary sync failure/);
  assert.equal(host.status().syncPending, true);
  await host.pollNow();

  const published = relay.calls.filter((item) => item.url.pathname.endsWith("/snapshot"));
  assert.equal(published.at(-1).body.snapshot.state.run.ingot, 99);
  assert.equal(host.status().lastError, "");
  assert.equal(host.status().syncPending, false);
});

test("remote host ignores operations returned after the session is stopped", async () => {
  const relay = createRelayFetch();
  const requested = deferred();
  const release = deferred();
  let saves = 0;
  relay.operations.push({ id: "late-operation", sequence: 1, status: "pending", operation: { type: "run.set", field: "ingot", value: 42 } });
  const host = createTournamentRemoteHost({
    fetchImpl: async (url, options) => {
      if (new URL(url).pathname.endsWith("/operations")) {
        requested.resolve();
        await release.promise;
      }
      return relay.fetchImpl(url, options);
    },
    getState: async () => baseState(),
    getMaster: async () => baseMaster(),
    saveState: async () => { saves += 1; },
    autoPoll: false,
  });
  await host.start({ relayUrl: "https://relay.example.test" });
  const polling = host.pollNow();
  await requested.promise;
  const stopping = host.stop();
  release.resolve();
  await Promise.allSettled([polling, stopping]);
  assert.equal(saves, 0);
  assert.equal(host.status().active, false);
  assert.equal(relay.calls.filter((call) => call.url.pathname.endsWith("/result")).length, 0);
});

test("remote host stop drains an already-started save without applying the next operation", async () => {
  const relay = createRelayFetch();
  const saving = deferred();
  const release = deferred();
  let state = baseState();
  let saves = 0;
  let stopped = false;
  for (let sequence = 1; sequence <= 2; sequence += 1) {
    relay.operations.push({ id: `operation-${sequence}`, sequence, status: "pending", operation: { type: "run.set", field: "ingot", value: 40 + sequence } });
  }
  const host = createTournamentRemoteHost({
    fetchImpl: relay.fetchImpl,
    getState: async () => state,
    getMaster: async () => baseMaster(),
    saveState: async (next) => {
      saves += 1;
      saving.resolve();
      await release.promise;
      state = next;
    },
    autoPoll: false,
  });
  await host.start({ relayUrl: "https://relay.example.test" });
  const polling = host.pollNow();
  await saving.promise;
  const stopping = host.stop().then(() => { stopped = true; });
  try {
    await new Promise((resolve) => setImmediate(resolve));
    assert.equal(stopped, false, "stop must not return while a state save can still complete");
  } finally {
    release.resolve();
    await Promise.allSettled([polling, stopping]);
  }
  assert.equal(saves, 1);
  assert.equal(state.run.ingot, 41);
});

test("remote host times out a stalled relay response body", async () => {
  const relay = createRelayFetch();
  let releaseBody;
  const host = createTournamentRemoteHost({
    fetchImpl: async (url, options) => {
      if (new URL(url).pathname.endsWith("/operations")) {
        return {
          status: 200,
          ok: true,
          text: () => new Promise((_resolve, reject) => {
            releaseBody = () => reject(new Error("test cleanup"));
            options.signal.addEventListener("abort", () => reject(new Error("request aborted")), { once: true });
          }),
        };
      }
      return relay.fetchImpl(url, options);
    },
    getState: async () => baseState(),
    getMaster: async () => baseMaster(),
    saveState: async () => {},
    autoPoll: false,
    requestTimeoutMs: 20,
  });
  await host.start({ relayUrl: "https://relay.example.test" });
  const polling = host.pollNow().then(() => "completed", (error) => error.message);
  let deadline;
  try {
    const result = await Promise.race([
      polling,
      new Promise((resolve) => { deadline = setTimeout(() => resolve("still waiting"), 200); }),
    ]);
    assert.match(result, /中継サーバーが応答しませんでした/);
  } finally {
    clearTimeout(deadline);
    releaseBody?.();
    await polling;
  }
});

test("remote host creates a session and publishes a sanitized snapshot", async () => {
  const relay = createRelayFetch();
  let state = baseState();
  const host = createTournamentRemoteHost({
    fetchImpl: relay.fetchImpl,
    getState: async () => state,
    getMaster: async () => baseMaster(),
    saveState: async (next) => {
      state = next;
      return state;
    },
    autoPoll: false,
  });

  const status = await host.start({
    relayUrl: "https://relay.example.test/",
    playerLabel: "Player A",
    adminToken: "admin-secret",
  });

  assert.equal(status.active, true);
  assert.equal(status.inputUrl, "https://relay.example.test/input/session-1?code=ABC123");
  assert.equal(status.editorCode, "ABC123");
  assert.equal(status.expiresAt, "1970-01-02T10:17:36.789Z");
  assert.equal(status.hostToken, undefined);
  assert.equal(status.adminToken, undefined);

  const createCall = relay.calls.find((item) => item.url.pathname === "/api/sessions");
  assert.equal(createCall.headers.get("x-admin-token"), "admin-secret");
  const snapshotCall = relay.calls.find((item) => item.url.pathname.endsWith("/snapshot"));
  assert.equal(snapshotCall.body.snapshot.state.adb, undefined);
  assert.equal(snapshotCall.body.snapshot.state.preferences, undefined);
});

test("remote host records recovery only after the initial relay sync succeeds", async () => {
  const relay = createRelayFetch();
  let failSync = true;
  let recovered = 0;
  const host = createTournamentRemoteHost({
    fetchImpl: async (url, options) => {
      if (failSync && new URL(url).pathname.endsWith("/snapshot")) throw new Error("initial sync failed");
      return relay.fetchImpl(url, options);
    },
    getState: async () => baseState(),
    getMaster: async () => baseMaster(),
    saveState: async () => {},
    onSessionStarted: async () => { recovered += 1; },
    autoPoll: false,
  });
  await assert.rejects(host.start({ relayUrl: "https://relay.example.test" }), /initial sync failed/);
  assert.equal(recovered, 0);
  assert.equal(host.status().active, false);
  failSync = false;
  await host.start({ relayUrl: "https://relay.example.test" });
  assert.equal(recovered, 1);
});

test("remote host applies queued operations in sequence and reports results", async () => {
  const relay = createRelayFetch();
  let state = baseState();
  relay.operations.push(
    {
      id: "operation-1",
      sequence: 1,
      status: "pending",
      operation: { type: "run.set", field: "ingot", value: 42 },
    },
    {
      id: "operation-2",
      sequence: 2,
      status: "pending",
      operation: { type: "run.set", field: "adb", value: "forbidden" },
    },
  );
  const saved = [];
  const host = createTournamentRemoteHost({
    fetchImpl: relay.fetchImpl,
    getState: async () => state,
    getMaster: async () => baseMaster(),
    saveState: async (next) => {
      state = { ...next, updatedAt: "2026-07-30T00:01:00.000Z" };
      saved.push(structuredClone(state));
      return state;
    },
    autoPoll: false,
  });
  await host.start({ relayUrl: "https://relay.example.test", playerLabel: "Player A" });

  const result = await host.pollNow();

  assert.equal(state.run.ingot, 42);
  assert.equal(saved.length, 1);
  assert.equal(result.applied, 1);
  assert.equal(result.rejected, 1);
  assert.equal(host.status().cursor, 2);

  const resultCalls = relay.calls.filter((item) => item.url.pathname.endsWith("/result"));
  assert.equal(resultCalls.length, 2);
  assert.equal(resultCalls[0].body.status, "applied");
  assert.equal(resultCalls[1].body.status, "rejected");
  assert.match(resultCalls[1].body.error, /許可されていない/);
});

test("remote host retries an unconfirmed applied result without saving or rejecting twice", async () => {
  const relay = createRelayFetch();
  let state = baseState();
  let saveCount = 0;
  let loseFirstAppliedResponse = true;
  relay.operations.push({
    id: "operation-1",
    sequence: 1,
    status: "pending",
    operation: { type: "run.set", field: "ingot", value: 42 },
  });
  const fetchImpl = async (url, options = {}) => {
    const result = await relay.fetchImpl(url, options);
    const body = options.body ? JSON.parse(options.body) : null;
    if (new URL(url).pathname.endsWith("/result") && body?.status === "applied" && loseFirstAppliedResponse) {
      loseFirstAppliedResponse = false;
      throw new Error("result response lost");
    }
    return result;
  };
  const host = createTournamentRemoteHost({
    fetchImpl,
    getState: async () => state,
    getMaster: async () => baseMaster(),
    saveState: async (next) => {
      saveCount += 1;
      state = next;
      return state;
    },
    autoPoll: false,
  });
  await host.start({ relayUrl: "https://relay.example.test", playerLabel: "Player A" });

  await assert.rejects(host.pollNow(), /result response lost/);
  assert.equal(state.run.ingot, 42);
  assert.equal(saveCount, 1);
  assert.equal(host.status().appliedSequence, 1, "the desktop can import a saved operation before its relay acknowledgement returns");
  assert.equal(host.status().cursor, 0);
  assert.deepEqual(
    relay.calls.filter((item) => item.url.pathname.endsWith("/result")).map((item) => item.body.status),
    ["applied"],
  );

  const retried = await host.pollNow();
  assert.equal(retried.applied, 1);
  assert.equal(host.status().cursor, 1);
  assert.equal(saveCount, 1);
  const resultCalls = relay.calls.filter((item) => item.url.pathname.endsWith("/result"));
  assert.deepEqual(resultCalls.map((item) => item.body.status), ["applied", "applied"]);
  assert.deepEqual(resultCalls.map((item) => item.body.snapshot.snapshotGeneration), [2, 2]);
  assert.deepEqual(resultCalls.map((item) => item.body.snapshot.revision), [1, 1]);
});

test("remote host reserves snapshot generations before asynchronous state reads", async () => {
  const relay = createRelayFetch();
  let state = baseState();
  let readCount = 0;
  let releaseSlowRead;
  let slowReadStarted;
  const slowReadReady = new Promise((resolve) => { slowReadStarted = resolve; });
  const slowState = structuredClone(state);
  const host = createTournamentRemoteHost({
    fetchImpl: relay.fetchImpl,
    getState: async () => {
      readCount += 1;
      if (readCount === 2) {
        slowReadStarted();
        await new Promise((resolve) => { releaseSlowRead = resolve; });
        return slowState;
      }
      return structuredClone(state);
    },
    getMaster: async () => baseMaster(),
    saveState: async (next) => next,
    autoPoll: false,
  });
  await host.start({ relayUrl: "https://relay.example.test", playerLabel: "Player A" });

  const slowSync = host.sync();
  await slowReadReady;
  state.run.ingot = 99;
  await host.sync();
  releaseSlowRead();
  await slowSync;

  const snapshots = relay.calls
    .filter((item) => item.url.pathname.endsWith("/snapshot"))
    .map((item) => item.body.snapshot);
  const oldSnapshot = snapshots.find((item) => item.state.run.ingot === 10 && item.snapshotGeneration > 1);
  const newSnapshot = snapshots.find((item) => item.state.run.ingot === 99);
  assert.equal(oldSnapshot.snapshotGeneration, 2);
  assert.equal(newSnapshot.snapshotGeneration, 3);
});

test("remote host snapshots current state after a delayed save completes", async () => {
  const relay = createRelayFetch();
  let state = baseState();
  let releaseSave;
  let saveStarted;
  const saveReady = new Promise((resolve) => { saveStarted = resolve; });
  relay.operations.push({
    id: "operation-1",
    sequence: 1,
    status: "pending",
    operation: { type: "run.set", field: "ingot", value: 42 },
  });
  const host = createTournamentRemoteHost({
    fetchImpl: relay.fetchImpl,
    getState: async () => structuredClone(state),
    getMaster: async () => baseMaster(),
    saveState: async (next) => {
      state = structuredClone(next);
      saveStarted();
      await new Promise((resolve) => { releaseSave = resolve; });
      return structuredClone(next);
    },
    autoPoll: false,
  });
  await host.start({ relayUrl: "https://relay.example.test", playerLabel: "Player A" });

  const polling = host.pollNow();
  await saveReady;
  state.run.ingot = 99;
  await host.sync();
  releaseSave();
  await polling;

  const snapshots = relay.calls
    .filter((item) => item.url.pathname.endsWith("/snapshot"))
    .map((item) => item.body.snapshot);
  const resultSnapshot = relay.calls.find((item) => item.url.pathname.endsWith("/result")).body.snapshot;
  assert.equal(snapshots.at(-1).state.run.ingot, 99);
  assert.equal(snapshots.at(-1).snapshotGeneration, 2);
  assert.equal(resultSnapshot.state.run.ingot, 99);
  assert.equal(resultSnapshot.snapshotGeneration, 3);
});

test("remote host keeps an applied result when its post-save snapshot read fails", async () => {
  const relay = createRelayFetch();
  let state = baseState();
  let stateReads = 0;
  let saveCount = 0;
  relay.operations.push({
    id: "operation-1",
    sequence: 1,
    status: "pending",
    operation: { type: "run.set", field: "ingot", value: 42 },
  });
  const host = createTournamentRemoteHost({
    fetchImpl: relay.fetchImpl,
    getState: async () => {
      stateReads += 1;
      if (stateReads === 3) throw new Error("post-save state read failed");
      return structuredClone(state);
    },
    getMaster: async () => baseMaster(),
    saveState: async (next) => {
      saveCount += 1;
      state = structuredClone(next);
      return state;
    },
    autoPoll: false,
  });
  await host.start({ relayUrl: "https://relay.example.test", playerLabel: "Player A" });

  await assert.rejects(host.pollNow(), /post-save state read failed/);
  assert.equal(host.status().cursor, 0);
  assert.equal(saveCount, 1);
  assert.equal(stateReads, 3);
  assert.equal(relay.calls.some((item) => item.url.pathname.endsWith("/result")), false);

  const retried = await host.pollNow();
  assert.equal(retried.applied, 1);
  assert.equal(retried.rejected, 0);
  assert.equal(host.status().cursor, 1);
  assert.equal(saveCount, 1);
  const resultCalls = relay.calls.filter((item) => item.url.pathname.endsWith("/result"));
  assert.equal(resultCalls.length, 1);
  assert.equal(resultCalls[0].body.status, "applied");
  assert.equal(resultCalls[0].body.snapshot.state.run.ingot, 42);
});

test("remote host closes the relay session and clears public status", async () => {
  const relay = createRelayFetch();
  const host = createTournamentRemoteHost({
    fetchImpl: relay.fetchImpl,
    getState: async () => baseState(),
    getMaster: async () => baseMaster(),
    saveState: async (state) => state,
    autoPoll: false,
  });
  await host.start({ relayUrl: "https://relay.example.test", playerLabel: "Player A" });

  await host.stop();

  assert.equal(host.status().active, false);
  assert.equal(host.status().sessionId, null);
  assert.ok(relay.calls.some((item) => item.method === "DELETE"));
});
