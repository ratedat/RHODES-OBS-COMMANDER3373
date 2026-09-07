import test from "node:test";
import assert from "node:assert/strict";

import {
  createEditorPersistence,
  describeEditorPendingState,
  loadEditorPersistence,
  prepareEditorSubmission,
  reconcileEditorSubmission,
  returnEditorSubmissionToDraft,
  saveEditorPersistence,
} from "../services/tournament-relay/public/editor-persistence.js";

function memoryStorage() {
  const values = new Map();
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, String(value)),
  };
}

test("editor persistence restores a session draft and unresolved submission", () => {
  const storage = memoryStorage();
  let state = createEditorPersistence("editor-a");
  state.pendingOperations = [{ type: "run.set", field: "ingot", value: 17 }];
  state = prepareEditorSubmission(state, () => "operation-a");

  saveEditorPersistence(storage, "session-a", state);
  const restored = loadEditorPersistence(storage, "session-a");

  assert.deepEqual(restored, state);
  assert.equal(restored.submission.clientOperationId, "operation-a");
});

test("editor submission preparation reuses the persisted id until a result is known", () => {
  let created = 0;
  const makeId = () => `operation-${++created}`;
  let state = createEditorPersistence("editor-a");
  state.pendingOperations = [{ type: "run.clear" }];

  const first = prepareEditorSubmission(state, makeId);
  const retry = prepareEditorSubmission(first, makeId);

  assert.equal(first.submission.clientOperationId, "operation-1");
  assert.equal(retry.submission.clientOperationId, "operation-1");
  assert.equal(created, 1);
});

test("applied submissions clear the draft while rejected submissions return it for editing", () => {
  let state = createEditorPersistence("editor-a");
  state.pendingOperations = [{ type: "run.set", field: "ingot", value: 17 }];
  state = prepareEditorSubmission(state, () => "operation-a");

  const pending = reconcileEditorSubmission(state, { id: "relay-a", status: "pending" });
  assert.equal(pending.submission.relayOperationId, "relay-a");
  assert.equal(pending.pendingOperations.length, 1);

  const rejected = reconcileEditorSubmission(pending, { id: "relay-a", status: "rejected" });
  assert.equal(rejected.submission, null);
  assert.equal(rejected.pendingOperations.length, 1);

  const applied = reconcileEditorSubmission(pending, { id: "relay-a", status: "applied" });
  assert.equal(applied.submission, null);
  assert.deepEqual(applied.pendingOperations, []);
  assert.equal(applied.editorClientId, "editor-a");
});

test("editor persistence reports storage and format failures instead of treating them as saved", () => {
  const unavailable = {
    getItem() { throw new Error("blocked"); },
    setItem() { throw new Error("blocked"); },
  };
  assert.throws(() => loadEditorPersistence(unavailable, "session-a"), /端末保存/);
  assert.throws(
    () => saveEditorPersistence(unavailable, "session-a", createEditorPersistence("editor-a")),
    /端末保存/,
  );

  const malformed = memoryStorage();
  malformed.setItem("rhodes-tournament-editor-state:session-a", "{broken");
  assert.throws(() => loadEditorPersistence(malformed, "session-a"), /端末保存/);
});

test("editor pending presentation distinguishes unsent, own submitted, retry, and predecessor work", () => {
  const draft = createEditorPersistence("editor-a");
  draft.pendingOperations = [{ type: "run.clear" }];
  assert.deepEqual(describeEditorPendingState(draft, []), {
    mode: "draft",
    summary: "1件の未送信変更",
    detail: "内容を確認し、「変更を送信」でまとめて即時反映します。",
    sendLabel: "変更を送信",
    disableEditor: false,
    disableSend: false,
    disableDiscard: false,
  });

  const submitted = prepareEditorSubmission(draft, () => "operation-a");
  assert.equal(describeEditorPendingState(submitted, [{
    id: "relay-a",
    clientOperationId: "operation-a",
    editorClientId: "editor-a",
    status: "pending",
  }]).mode, "own-pending");

  const retry = describeEditorPendingState(submitted, []);
  assert.equal(retry.mode, "retry");
  assert.equal(retry.sendLabel, "同じ内容を再送");
  assert.equal(retry.disableSend, false);
  assert.equal(retry.disableEditor, true);

  const handoff = describeEditorPendingState(draft, [{
    id: "relay-b",
    clientOperationId: "operation-b",
    editorClientId: "editor-b",
    status: "pending",
  }]);
  assert.equal(handoff.mode, "foreign-pending");
  assert.equal(handoff.summary, "前担当の変更が反映待ちです");
  assert.equal(handoff.disableEditor, true);
  assert.equal(handoff.disableSend, true);
});

test("editor operation limit blocks new drafts but still allows an existing id to retry", () => {
  const draft = createEditorPersistence("editor-a");
  draft.pendingOperations = [{ type: "run.clear" }];

  const limitedDraft = describeEditorPendingState(draft, [], { remainingOperations: 0, maxOperations: 500 });
  assert.equal(limitedDraft.mode, "operation-limit");
  assert.equal(limitedDraft.summary, "このセッションは500件の操作上限に達しました");
  assert.equal(limitedDraft.disableSend, true);
  assert.equal(limitedDraft.disableEditor, false);

  const submitted = prepareEditorSubmission(draft, () => "operation-a");
  const retry = describeEditorPendingState(submitted, [], { remainingOperations: 0, maxOperations: 500 });
  assert.equal(retry.mode, "retry");
  assert.equal(retry.disableSend, false);

  const returned = returnEditorSubmissionToDraft(submitted);
  assert.equal(returned.submission, null);
  assert.deepEqual(returned.pendingOperations, draft.pendingOperations);
});
