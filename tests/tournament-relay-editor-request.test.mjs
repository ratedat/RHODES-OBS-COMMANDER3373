import test from "node:test";
import assert from "node:assert/strict";

import { requestEditorJson } from "../services/tournament-relay/public/editor-request.js";
import {
  createEditorPersistence,
  loadEditorPersistence,
  prepareEditorSubmission,
  saveEditorPersistence,
} from "../services/tournament-relay/public/editor-persistence.js";

function memoryStorage() {
  const values = new Map();
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, String(value)),
  };
}

test("editor request timeout covers stalled response body reads", async () => {
  let observedSignal = null;
  const fetchImpl = async (_url, options) => {
    observedSignal = options.signal;
    return {
      ok: true,
      status: 200,
      json: () => new Promise((resolve, reject) => {
        options.signal.addEventListener("abort", () => reject(options.signal.reason), { once: true });
      }),
    };
  };

  await assert.rejects(
    requestEditorJson("/stalled", {}, { fetchImpl, timeoutMs: 5 }),
    (error) => error.code === "request_timeout" && /タイムアウト/.test(error.message),
  );
  assert.equal(observedSignal.aborted, true);
});

test("editor request preserves relay errors after reading the JSON response", async () => {
  const fetchImpl = async () => ({
    ok: false,
    status: 409,
    json: async () => ({ error: "別の変更が反映待ちです。", code: "pending_operation_conflict" }),
  });

  await assert.rejects(
    requestEditorJson("/conflict", {}, { fetchImpl, timeoutMs: 50 }),
    (error) => error.code === "pending_operation_conflict" && error.status === 409,
  );
});

test("editor request rejects an unreadable successful response", async () => {
  const fetchImpl = async () => ({
    ok: true,
    status: 202,
    json: async () => { throw new SyntaxError("truncated"); },
  });

  await assert.rejects(
    requestEditorJson("/truncated", {}, { fetchImpl, timeoutMs: 50 }),
    (error) => error.code === "invalid_relay_response" && error.status === 202,
  );
});

test("a timed out submission reloads and retries with its preflight-persisted id", async () => {
  const storage = memoryStorage();
  let state = createEditorPersistence("editor-a");
  state.pendingOperations = [{ type: "run.clear" }];
  state = prepareEditorSubmission(state, () => "operation-before-request");
  saveEditorPersistence(storage, "session-a", state);

  const fetchImpl = async (_url, options) => ({
    ok: true,
    status: 202,
    json: () => new Promise((resolve, reject) => {
      options.signal.addEventListener("abort", () => reject(options.signal.reason), { once: true });
    }),
  });
  await assert.rejects(
    requestEditorJson("/stalled", {}, { fetchImpl, timeoutMs: 5 }),
    (error) => error.code === "request_timeout",
  );

  const restored = loadEditorPersistence(storage, "session-a");
  const retry = prepareEditorSubmission(restored, () => "must-not-be-used");
  assert.equal(retry.submission.clientOperationId, "operation-before-request");
});
