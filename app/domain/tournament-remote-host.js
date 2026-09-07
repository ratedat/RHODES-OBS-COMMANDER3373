import {
  applyTournamentRemoteOperation,
  buildTournamentRemoteSnapshot,
} from "./tournament-remote-operations.js";

const DEFAULT_POLL_INTERVAL_MS = 750;
const DEFAULT_REQUEST_TIMEOUT_MS = 8_000;

function normalizeRelayUrl(value) {
  const raw = String(value || "").trim();
  if (!raw) throw new Error("中継サーバーURLを入力してください。");
  const url = new URL(raw);
  if (url.protocol !== "http:" && url.protocol !== "https:") {
    throw new Error("中継サーバーURLはhttpまたはhttpsで指定してください。");
  }
  url.pathname = url.pathname.replace(/\/+$/, "");
  url.search = "";
  url.hash = "";
  return url.toString().replace(/\/+$/, "");
}

function normalizeTimestamp(value) {
  if (value === null || value === undefined || value === "") return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

function publicError(error) {
  return error instanceof Error ? error.message : String(error);
}

async function relayRequest(fetchImpl, url, {
  method = "GET",
  hostToken = "",
  adminToken = "",
  body,
  timeoutMs = DEFAULT_REQUEST_TIMEOUT_MS,
} = {}) {
  const headers = { accept: "application/json" };
  if (hostToken) headers.authorization = `Bearer ${hostToken}`;
  if (adminToken) headers["x-admin-token"] = adminToken;
  if (body !== undefined) headers["content-type"] = "application/json";
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const response = await fetchImpl(url, {
      method,
      headers,
      signal: controller.signal,
      ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
    });
    const text = response.status === 204 ? "" : await response.text();
    let payload = {};
    if (text) {
      try {
        payload = JSON.parse(text);
      } catch {
        payload = { error: text };
      }
    }
    if (!response.ok) {
      throw Object.assign(new Error(payload?.error || `中継サーバーがHTTP ${response.status}を返しました。`), {
        status: response.status,
        code: payload?.code || "relay_error",
      });
    }
    return payload;
  } catch (error) {
    if (controller.signal.aborted) throw new Error("中継サーバーが応答しませんでした。");
    throw error;
  } finally {
    clearTimeout(timeout);
  }
}

export function createTournamentRemoteHost({
  fetchImpl = globalThis.fetch,
  getState,
  getMaster,
  saveState,
  onSessionStarted = async () => {},
  autoPoll = true,
  pollIntervalMs = DEFAULT_POLL_INTERVAL_MS,
  requestTimeoutMs = DEFAULT_REQUEST_TIMEOUT_MS,
  setTimer = setTimeout,
  clearTimer = clearTimeout,
  now = () => new Date(),
} = {}) {
  if (typeof fetchImpl !== "function") throw new Error("fetch implementation is required");
  if (typeof getState !== "function") throw new Error("getState is required");
  if (typeof getMaster !== "function") throw new Error("getMaster is required");
  if (typeof saveState !== "function") throw new Error("saveState is required");
  if (typeof onSessionStarted !== "function") throw new Error("onSessionStarted must be a function");

  let session = null;
  let timer = null;
  let lifecycle = Promise.resolve();
  const request = (url, options) => relayRequest(fetchImpl, url, { timeoutMs: requestTimeoutMs, ...options });

  function transition(action) {
    const next = lifecycle.then(action, action);
    lifecycle = next.catch(() => {});
    return next;
  }

  function status() {
    return {
      active: Boolean(session),
      relayUrl: session?.relayUrl || "",
      sessionId: session?.sessionId || null,
      editorCode: session?.editorCode || "",
      inputUrl: session?.inputUrl || "",
      playerLabel: session?.playerLabel || "",
      expiresAt: normalizeTimestamp(session?.expiresAt),
      cursor: session?.cursor || 0,
      appliedSequence: session?.appliedSequence || 0,
      startedAt: session?.startedAt || null,
      lastSyncedAt: session?.lastSyncedAt || null,
      lastOperationAt: session?.lastOperationAt || null,
      lastError: session?.lastError || "",
      syncPending: Boolean(session && session.syncCompleted < session.syncRequested),
    };
  }

  function cancelTimer() {
    if (timer !== null) clearTimer(timer);
    timer = null;
  }

  function schedulePoll() {
    cancelTimer();
    if (!autoPoll || !session) return;
    timer = setTimer(async () => {
      timer = null;
      try {
        await pollNow();
      } catch {
        // The public status keeps the error. A temporary relay failure must not end the session.
      } finally {
        schedulePoll();
      }
    }, pollIntervalMs);
  }

  async function snapshot(activeSession, state, master) {
    const snapshotGeneration = ++activeSession.snapshotGeneration;
    const currentState = state === undefined ? await getState() : state;
    const currentMaster = master === undefined ? await getMaster() : master;
    return {
      ...buildTournamentRemoteSnapshot(currentState, currentMaster),
      snapshotGeneration,
    };
  }

  async function sync() {
    if (!session) return status();
    const activeSession = session;
    activeSession.syncRequested += 1;
    return publishRequestedSync(activeSession);
  }

  async function publishRequestedSync(activeSession) {
    const revision = activeSession.syncRequested;
    try {
      const currentSnapshot = await snapshot(activeSession);
      if (session !== activeSession) return status();
      await request(`${activeSession.relayUrl}/api/sessions/${encodeURIComponent(activeSession.sessionId)}/snapshot`, {
        method: "PUT",
        hostToken: activeSession.hostToken,
        body: { snapshot: currentSnapshot },
      });
      activeSession.syncCompleted = Math.max(activeSession.syncCompleted, revision);
      activeSession.lastSyncedAt = now().toISOString();
      if (activeSession.syncCompleted >= activeSession.syncRequested) activeSession.lastError = "";
      return status();
    } catch (error) {
      if (activeSession.syncCompleted < revision) activeSession.lastError = publicError(error);
      throw error;
    }
  }

  async function resolveOperation(activeSession, entry, result) {
    await request(
      `${activeSession.relayUrl}/api/sessions/${encodeURIComponent(activeSession.sessionId)}/operations/${encodeURIComponent(entry.id)}/result`,
      {
        method: "POST",
        hostToken: activeSession.hostToken,
        body: result,
      },
    );
  }

  async function confirmResult(activeSession) {
    const pending = activeSession.unconfirmedResult;
    if (!pending) return null;
    if (!pending.result.snapshot) {
      pending.result.snapshot = await snapshot(activeSession);
    }
    if (session !== activeSession) return null;
    await resolveOperation(activeSession, pending.entry, pending.result);
    activeSession.cursor = Math.max(activeSession.cursor, pending.sequence);
    activeSession.unconfirmedResult = null;
    activeSession.lastSyncedAt = now().toISOString();
    return pending.outcome;
  }

  function pollNow() {
    if (!session) return Promise.resolve({ applied: 0, rejected: 0, cursor: 0 });
    const activeSession = session;
    if (activeSession.pollTask) return activeSession.pollTask;
    activeSession.pollTask = runPoll(activeSession).finally(() => { activeSession.pollTask = null; });
    return activeSession.pollTask;
  }

  async function runPoll(activeSession) {
    let applied = 0;
    let rejected = 0;
    const resultCounts = () => ({ applied, rejected, cursor: activeSession.cursor });
    try {
      const retriedOutcome = await confirmResult(activeSession);
      if (retriedOutcome === "applied") applied += 1;
      else if (retriedOutcome === "rejected") rejected += 1;
      if (session !== activeSession) return resultCounts();
      const result = await request(
        `${activeSession.relayUrl}/api/sessions/${encodeURIComponent(activeSession.sessionId)}/operations?after=${activeSession.cursor}`,
        { hostToken: activeSession.hostToken },
      );
      if (session !== activeSession) return resultCounts();
      const entries = Array.isArray(result.operations)
        ? [...result.operations].sort((a, b) => Number(a.sequence || 0) - Number(b.sequence || 0))
        : [];
      const master = await getMaster();
      for (const entry of entries) {
        if (session !== activeSession) return resultCounts();
        const sequence = Math.max(0, Number(entry.sequence) || 0);
        if (entry.status !== "pending") {
          activeSession.cursor = Math.max(activeSession.cursor, sequence);
          continue;
        }
        let pending;
        try {
          const current = await getState();
          if (session !== activeSession) return resultCounts();
          const operationResult = applyTournamentRemoteOperation(current, master, entry.operation);
          await saveState(operationResult.state);
          activeSession.appliedSequence = Math.max(activeSession.appliedSequence, sequence);
          activeSession.lastOperationAt = now().toISOString();
          if (session !== activeSession) return resultCounts();
          pending = {
            entry,
            sequence,
            outcome: "applied",
            result: {
              status: "applied",
              summary: operationResult.summary,
            },
          };
        } catch (error) {
          if (session !== activeSession) return resultCounts();
          pending = {
            entry,
            sequence,
            outcome: "rejected",
            result: {
              status: "rejected",
              error: publicError(error),
            },
          };
        }
        activeSession.unconfirmedResult = pending;
        const outcome = await confirmResult(activeSession);
        if (outcome === "applied") applied += 1;
        else if (outcome === "rejected") rejected += 1;
      }
      if (session !== activeSession) return resultCounts();
      if (activeSession.syncCompleted < activeSession.syncRequested) await publishRequestedSync(activeSession);
      if (activeSession.syncCompleted >= activeSession.syncRequested) activeSession.lastError = "";
      return resultCounts();
    } catch (error) {
      activeSession.lastError = publicError(error);
      throw error;
    }
  }

  async function stopSession() {
    cancelTimer();
    const closing = session;
    session = null;
    if (!closing) return status();
    // Let a save that already started finish before a new session can use its state.
    await closing.pollTask?.catch(() => {});
    try {
      await request(`${closing.relayUrl}/api/sessions/${encodeURIComponent(closing.sessionId)}`, {
        method: "DELETE",
        hostToken: closing.hostToken,
      });
    } catch {
      // Local stop must succeed even when the relay is no longer reachable.
    }
    return status();
  }

  async function startSession({ relayUrl, playerLabel = "Player", adminToken = "" } = {}) {
    await stopSession();
    const normalizedRelayUrl = normalizeRelayUrl(relayUrl);
    const created = await request(`${normalizedRelayUrl}/api/sessions`, {
      method: "POST",
      adminToken: String(adminToken || ""),
      body: { playerLabel: String(playerLabel || "Player").trim().slice(0, 80) || "Player" },
    });
    const inputUrl = new URL(created.inputUrl, `${normalizedRelayUrl}/`).toString();
    session = {
      relayUrl: normalizedRelayUrl,
      sessionId: created.sessionId,
      hostToken: created.hostToken,
      editorCode: created.editorCode,
      inputUrl,
      playerLabel: String(playerLabel || "Player").trim().slice(0, 80) || "Player",
      expiresAt: normalizeTimestamp(created.expiresAt),
      cursor: 0,
      appliedSequence: 0,
      snapshotGeneration: 0,
      syncRequested: 0,
      syncCompleted: 0,
      pollTask: null,
      unconfirmedResult: null,
      startedAt: now().toISOString(),
      lastSyncedAt: null,
      lastOperationAt: null,
      lastError: "",
    };
    try {
      await sync();
      await onSessionStarted();
      schedulePoll();
      return status();
    } catch (error) {
      await stopSession();
      throw error;
    }
  }

  return {
    start: (options) => transition(() => startSession(options)),
    stop: () => transition(stopSession),
    sync,
    pollNow,
    status,
  };
}
