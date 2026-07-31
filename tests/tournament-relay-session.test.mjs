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

  store.resolveOperation(created.sessionId, created.hostToken, first.id, {
    status: "applied",
    summary: "源石錐を12に変更",
    snapshot: { revision: 3, state: { run: { ingot: 12 } } },
  });
  const editor = store.getEditorBootstrap(created.sessionId, created.editorCode);
  assert.equal(editor.history.find((item) => item.id === first.id).status, "applied");
  assert.equal(editor.snapshot.state.run.ingot, 12);
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
