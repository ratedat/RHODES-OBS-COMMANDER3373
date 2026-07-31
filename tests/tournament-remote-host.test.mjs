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
      return response(200, { operations: structuredClone(operations) });
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
