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
  let response;
  try {
    response = await fetchImpl(url, {
      method,
      headers,
      signal: controller.signal,
      ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
    });
  } catch (error) {
    if (controller.signal.aborted) throw new Error("中継サーバーが応答しませんでした。");
    throw error;
  } finally {
    clearTimeout(timeout);
  }
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
    throw new Error(payload?.error || `中継サーバーがHTTP ${response.status}を返しました。`);
  }
  return payload;
}

export function createTournamentRemoteHost({
  fetchImpl = globalThis.fetch,
  getState,
  getMaster,
  saveState,
  autoPoll = true,
  pollIntervalMs = DEFAULT_POLL_INTERVAL_MS,
  setTimer = setTimeout,
  clearTimer = clearTimeout,
  now = () => new Date(),
} = {}) {
  if (typeof fetchImpl !== "function") throw new Error("fetch implementation is required");
  if (typeof getState !== "function") throw new Error("getState is required");
  if (typeof getMaster !== "function") throw new Error("getMaster is required");
  if (typeof saveState !== "function") throw new Error("saveState is required");

  let session = null;
  let timer = null;
  let polling = false;

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
      startedAt: session?.startedAt || null,
      lastSyncedAt: session?.lastSyncedAt || null,
      lastOperationAt: session?.lastOperationAt || null,
      lastError: session?.lastError || "",
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

  async function snapshot() {
    return buildTournamentRemoteSnapshot(await getState(), await getMaster());
  }

  async function sync() {
    if (!session) return status();
    try {
      await relayRequest(fetchImpl, `${session.relayUrl}/api/sessions/${encodeURIComponent(session.sessionId)}/snapshot`, {
        method: "PUT",
        hostToken: session.hostToken,
        body: { snapshot: await snapshot() },
      });
      session.lastSyncedAt = now().toISOString();
      session.lastError = "";
      return status();
    } catch (error) {
      session.lastError = publicError(error);
      throw error;
    }
  }

  async function resolveOperation(entry, result) {
    await relayRequest(
      fetchImpl,
      `${session.relayUrl}/api/sessions/${encodeURIComponent(session.sessionId)}/operations/${encodeURIComponent(entry.id)}/result`,
      {
        method: "POST",
        hostToken: session.hostToken,
        body: result,
      },
    );
  }

  async function pollNow() {
    if (!session || polling) return { applied: 0, rejected: 0, cursor: session?.cursor || 0 };
    polling = true;
    let applied = 0;
    let rejected = 0;
    try {
      const result = await relayRequest(
        fetchImpl,
        `${session.relayUrl}/api/sessions/${encodeURIComponent(session.sessionId)}/operations?after=${session.cursor}`,
        { hostToken: session.hostToken },
      );
      const entries = Array.isArray(result.operations)
        ? [...result.operations].sort((a, b) => Number(a.sequence || 0) - Number(b.sequence || 0))
        : [];
      const master = await getMaster();
      for (const entry of entries) {
        const sequence = Math.max(0, Number(entry.sequence) || 0);
        try {
          if (entry.status !== "pending") continue;
          const current = await getState();
          const operationResult = applyTournamentRemoteOperation(current, master, entry.operation);
          const saved = await saveState(operationResult.state);
          await resolveOperation(entry, {
            status: "applied",
            summary: operationResult.summary,
            snapshot: buildTournamentRemoteSnapshot(saved || operationResult.state, master),
          });
          applied += 1;
          session.lastOperationAt = now().toISOString();
          session.lastSyncedAt = session.lastOperationAt;
        } catch (error) {
          await resolveOperation(entry, {
            status: "rejected",
            error: publicError(error),
            snapshot: await snapshot(),
          });
          rejected += 1;
        } finally {
          session.cursor = Math.max(session.cursor, sequence);
        }
      }
      session.lastError = "";
      return { applied, rejected, cursor: session.cursor };
    } catch (error) {
      session.lastError = publicError(error);
      throw error;
    } finally {
      polling = false;
    }
  }

  async function stop() {
    cancelTimer();
    const closing = session;
    session = null;
    if (!closing) return status();
    try {
      await relayRequest(fetchImpl, `${closing.relayUrl}/api/sessions/${encodeURIComponent(closing.sessionId)}`, {
        method: "DELETE",
        hostToken: closing.hostToken,
      });
    } catch {
      // Local stop must succeed even when the relay is no longer reachable.
    }
    return status();
  }

  async function start({ relayUrl, playerLabel = "Player", adminToken = "" } = {}) {
    await stop();
    const normalizedRelayUrl = normalizeRelayUrl(relayUrl);
    const created = await relayRequest(fetchImpl, `${normalizedRelayUrl}/api/sessions`, {
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
      startedAt: now().toISOString(),
      lastSyncedAt: null,
      lastOperationAt: null,
      lastError: "",
    };
    try {
      await sync();
      schedulePoll();
      return status();
    } catch (error) {
      await stop();
      throw error;
    }
  }

  return {
    start,
    stop,
    sync,
    pollNow,
    status,
  };
}
