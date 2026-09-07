import test from "node:test";
import assert from "node:assert/strict";

import { createTournamentRelaySessionStore } from "../services/tournament-relay/session-store.mjs";

test("relay sessions separate host credentials from editor access", () => {
  const store = createTournamentRelaySessionStore({ now: () => 1_000 });
  const created = store.createSession({ playerLabel: "Player A" });

  assert.match(created.sessionId, /^[a-z0-9-]+$/);
  assert.ok(created.hostToken.length >= 32);
  assert.match(created.editorCode, /^[A-Z0-9]{6}$/);

  const editor = store.getEditorBootstrap(created.sessionId, created.editorCode);
  assert.equal(editor.playerLabel, "Player A");
  assert.equal(editor.hostToken, undefined);
  assert.equal(editor.editorCode, undefined);
});

test("relay queues ordered operations and records host results", () => {
  const store = createTournamentRelaySessionStore({ now: () => 2_000 });
  const created = store.createSession({ playerLabel: "Player A" });

  const first = store.enqueueOperation(created.sessionId, created.editorCode, {
    type: "run.set",
    field: "ingot",
    value: 12,
  });
  store.resolveOperation(created.sessionId, created.hostToken, first.id, {
    status: "applied",
    summary: "源石錐を12に変更",
    snapshot: { revision: 3, state: { run: { ingot: 12 } } },
  });
  const second = store.enqueueOperation(created.sessionId, created.editorCode, {
    type: "run.set",
    field: "difficulty",
    value: 5,
  });

  assert.equal(first.sequence, 1);
  assert.equal(second.sequence, 2);
  assert.deepEqual(
    store.listOperations(created.sessionId, created.hostToken, { after: 0 }).map((item) => item.sequence),
    [1, 2],
  );

  const editor = store.getEditorBootstrap(created.sessionId, created.editorCode);
  assert.equal(editor.history.find((item) => item.id === first.id).status, "applied");
  assert.equal(editor.snapshot.state.run.ingot, 12);
});

test("relay reuses a client operation result and rejects id reuse with different content", () => {
  const store = createTournamentRelaySessionStore({ now: () => 2_500 });
  const created = store.createSession({ playerLabel: "Player A" });
  const operation = { type: "run.set", field: "ingot", value: 12 };
  const identity = {
    clientOperationId: "operation-a",
    editorClientId: "editor-a",
  };

  const first = store.enqueueOperation(created.sessionId, created.editorCode, operation, identity);
  store.resolveOperation(created.sessionId, created.hostToken, first.id, {
    status: "applied",
    summary: "源石錐を12に変更",
  });
  const replayed = store.enqueueOperation(created.sessionId, created.editorCode, operation, identity);

  assert.equal(replayed.id, first.id);
  assert.equal(replayed.sequence, first.sequence);
  assert.equal(replayed.status, "applied");
  assert.equal(store.listOperations(created.sessionId, created.hostToken).length, 1);
  assert.throws(
    () => store.enqueueOperation(created.sessionId, created.editorCode, {
      ...operation,
      value: 13,
    }, identity),
    (error) => error.code === "client_operation_conflict" && error.status === 409,
  );
});

test("relay rejects a second operation while any editor operation is pending", () => {
  const store = createTournamentRelaySessionStore({ now: () => 2_750 });
  const created = store.createSession({ playerLabel: "Player A" });
  const first = store.enqueueOperation(created.sessionId, created.editorCode, {
    type: "run.set",
    field: "ingot",
    value: 12,
  }, {
    clientOperationId: "operation-a",
    editorClientId: "editor-a",
  });

  assert.throws(
    () => store.enqueueOperation(created.sessionId, created.editorCode, {
      type: "run.set",
      field: "ingot",
      value: 13,
    }, {
      clientOperationId: "operation-b",
      editorClientId: "editor-b",
    }),
    (error) => error.code === "pending_operation_conflict" && error.status === 409,
  );

  store.resolveOperation(created.sessionId, created.hostToken, first.id, { status: "applied" });
  const second = store.enqueueOperation(created.sessionId, created.editorCode, {
    type: "run.set",
    field: "ingot",
    value: 13,
  }, {
    clientOperationId: "operation-b",
    editorClientId: "editor-b",
  });
  assert.equal(second.sequence, 2);
});

test("relay rejects new operations at its finite session limit without forgetting dedupe ids", () => {
  const store = createTournamentRelaySessionStore({ now: () => 2_900, maxOperations: 2 });
  const created = store.createSession({ playerLabel: "Player A" });
  const identities = ["operation-a", "operation-b"].map((clientOperationId) => ({
    clientOperationId,
    editorClientId: "editor-a",
  }));
  const operations = identities.map((identity, index) => {
    const entry = store.enqueueOperation(created.sessionId, created.editorCode, {
      type: "run.set",
      field: "ingot",
      value: index + 1,
    }, identity);
    store.resolveOperation(created.sessionId, created.hostToken, entry.id, { status: "applied" });
    return entry;
  });

  assert.throws(
    () => store.enqueueOperation(created.sessionId, created.editorCode, {
      type: "run.set",
      field: "ingot",
      value: 3,
    }, {
      clientOperationId: "operation-c",
      editorClientId: "editor-a",
    }),
    (error) => error.code === "operation_limit_reached" && error.status === 429,
  );
  const replayed = store.enqueueOperation(created.sessionId, created.editorCode, {
    type: "run.set",
    field: "ingot",
    value: 1,
  }, identities[0]);
  assert.equal(replayed.id, operations[0].id);
  assert.equal(store.getEditorBootstrap(created.sessionId, created.editorCode).limits.maxOperations, 2);
});

test("relay keeps the newest generated snapshot when requests arrive in reverse order", () => {
  const store = createTournamentRelaySessionStore({ now: () => 3_000 });
  const created = store.createSession({ playerLabel: "Player A" });

  const newest = store.setSnapshot(created.sessionId, created.hostToken, {
    revision: 1,
    snapshotGeneration: 2,
    state: { run: { ingot: 20 } },
  });
  const stale = store.setSnapshot(created.sessionId, created.hostToken, {
    revision: 1,
    snapshotGeneration: 1,
    state: { run: { ingot: 10 } },
  });

  assert.equal(newest.accepted, true);
  assert.equal(stale.accepted, false);
  const editor = store.getEditorBootstrap(created.sessionId, created.editorCode);
  assert.equal(editor.snapshot.snapshotGeneration, 2);
  assert.equal(editor.snapshot.state.run.ingot, 20);
});

test("relay resolves an operation idempotently and never flips applied to rejected", () => {
  const store = createTournamentRelaySessionStore({ now: () => 4_000 });
  const created = store.createSession({ playerLabel: "Player A" });
  const operation = store.enqueueOperation(created.sessionId, created.editorCode, {
    type: "run.set",
    field: "ingot",
    value: 20,
  });
  const applied = {
    status: "applied",
    summary: "源石錐を20に変更",
    snapshot: { revision: 1, snapshotGeneration: 2, state: { run: { ingot: 20 } } },
  };

  store.resolveOperation(created.sessionId, created.hostToken, operation.id, applied);
  const retried = store.resolveOperation(created.sessionId, created.hostToken, operation.id, applied);
  assert.equal(retried.status, "applied");
  assert.throws(
    () => store.resolveOperation(created.sessionId, created.hostToken, operation.id, {
      status: "rejected",
      snapshot: { revision: 1, snapshotGeneration: 1, state: { run: { ingot: 10 } } },
    }),
    (error) => error.code === "operation_already_resolved" && error.status === 409,
  );
  const editor = store.getEditorBootstrap(created.sessionId, created.editorCode);
  assert.equal(editor.history[0].status, "applied");
  assert.equal(editor.snapshot.state.run.ingot, 20);
});

test("relay keeps legacy generationless snapshots compatible until a generated snapshot is seen", () => {
  const store = createTournamentRelaySessionStore({ now: () => 4_500 });
  const legacy = store.createSession({ playerLabel: "Legacy host" });
  assert.equal(store.setSnapshot(legacy.sessionId, legacy.hostToken, { state: { run: { ingot: 1 } } }).accepted, true);
  assert.equal(store.setSnapshot(legacy.sessionId, legacy.hostToken, { state: { run: { ingot: 2 } } }).accepted, true);
  assert.equal(store.getEditorBootstrap(legacy.sessionId, legacy.editorCode).snapshot.state.run.ingot, 2);

  const upgraded = store.createSession({ playerLabel: "Upgraded host" });
  store.setSnapshot(upgraded.sessionId, upgraded.hostToken, {
    snapshotGeneration: 1,
    state: { run: { ingot: 3 } },
  });
  const ignoredLegacy = store.setSnapshot(upgraded.sessionId, upgraded.hostToken, {
    state: { run: { ingot: 0 } },
  });
  assert.equal(ignoredLegacy.accepted, false);
  assert.equal(store.getEditorBootstrap(upgraded.sessionId, upgraded.editorCode).snapshot.state.run.ingot, 3);
});

test("relay rejects invalid credentials and expired sessions", () => {
  let now = 5_000;
  const store = createTournamentRelaySessionStore({
    now: () => now,
    sessionTtlMs: 1_000,
  });
  const created = store.createSession({ playerLabel: "Player A" });

  assert.throws(
    () => store.enqueueOperation(created.sessionId, "WRONG1", { type: "run.clear" }),
    /認証/,
  );
  now = 7_000;
  assert.throws(
    () => store.getEditorBootstrap(created.sessionId, created.editorCode),
    /期限切れ/,
  );
});
