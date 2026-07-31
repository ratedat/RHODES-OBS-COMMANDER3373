import { randomBytes, randomUUID } from "node:crypto";

function relayError(message, status = 400, code = "relay_error") {
  return Object.assign(new Error(message), { status, code });
}

function randomEditorCode() {
  const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  const bytes = randomBytes(6);
  return [...bytes].map((value) => alphabet[value % alphabet.length]).join("");
}

function clone(value) {
  return value === undefined ? undefined : structuredClone(value);
}

export function createTournamentRelaySessionStore({
  now = () => Date.now(),
  sessionTtlMs = 12 * 60 * 60 * 1_000,
  publicBaseUrl = "",
} = {}) {
  const sessions = new Map();

  function requireSession(sessionId) {
    const session = sessions.get(sessionId);
    if (!session) throw relayError("遠隔入力セッションが見つかりません。", 404, "session_not_found");
    if (session.expiresAt <= now()) {
      sessions.delete(sessionId);
      throw relayError("遠隔入力セッションは期限切れです。", 410, "session_expired");
    }
    return session;
  }

  function requireHost(sessionId, hostToken) {
    const session = requireSession(sessionId);
    if (!hostToken || hostToken !== session.hostToken) throw relayError("ホスト認証に失敗しました。", 401, "host_auth_failed");
    return session;
  }

  function requireEditor(sessionId, editorCode) {
    const session = requireSession(sessionId);
    if (!editorCode || String(editorCode).toUpperCase() !== session.editorCode) {
      throw relayError("入力担当者の認証に失敗しました。", 401, "editor_auth_failed");
    }
    return session;
  }

  function touch(session) {
    session.expiresAt = now() + sessionTtlMs;
    session.updatedAt = now();
  }

  function createSession({ playerLabel = "Player" } = {}) {
    const sessionId = randomUUID().toLowerCase();
    const hostToken = randomBytes(32).toString("base64url");
    let editorCode = randomEditorCode();
    while ([...sessions.values()].some((session) => session.editorCode === editorCode)) editorCode = randomEditorCode();
    const createdAt = now();
    const session = {
      sessionId,
      hostToken,
      editorCode,
      playerLabel: String(playerLabel || "Player").trim().slice(0, 80) || "Player",
      createdAt,
      updatedAt: createdAt,
      expiresAt: createdAt + sessionTtlMs,
      nextSequence: 1,
      snapshot: null,
      operations: [],
    };
    sessions.set(sessionId, session);
    const base = String(publicBaseUrl || "").replace(/\/+$/, "");
    return {
      sessionId,
      hostToken,
      editorCode,
      inputUrl: base ? `${base}/input/${sessionId}?code=${editorCode}` : `/input/${sessionId}?code=${editorCode}`,
      expiresAt: session.expiresAt,
    };
  }

  function setSnapshot(sessionId, hostToken, snapshot) {
    const session = requireHost(sessionId, hostToken);
    session.snapshot = clone(snapshot);
    touch(session);
    return { updatedAt: session.updatedAt };
  }

  function getEditorBootstrap(sessionId, editorCode) {
    const session = requireEditor(sessionId, editorCode);
    touch(session);
    return {
      sessionId: session.sessionId,
      playerLabel: session.playerLabel,
      expiresAt: session.expiresAt,
      snapshot: clone(session.snapshot),
      history: session.operations.slice(-100).reverse().map((item) => ({
        id: item.id,
        sequence: item.sequence,
        operation: clone(item.operation),
        status: item.status,
        summary: item.summary || "",
        error: item.error || "",
        createdAt: item.createdAt,
        resolvedAt: item.resolvedAt || null,
      })),
    };
  }

  function enqueueOperation(sessionId, editorCode, operation) {
    const session = requireEditor(sessionId, editorCode);
    if (!operation || typeof operation !== "object" || Array.isArray(operation)) {
      throw relayError("操作形式が不正です。", 400, "invalid_operation");
    }
    const entry = {
      id: randomUUID(),
      sequence: session.nextSequence++,
      operation: clone(operation),
      status: "pending",
      summary: "",
      error: "",
      createdAt: now(),
      resolvedAt: null,
    };
    session.operations.push(entry);
    if (session.operations.length > 500) session.operations.splice(0, session.operations.length - 500);
    touch(session);
    return clone(entry);
  }

  function listOperations(sessionId, hostToken, { after = 0 } = {}) {
    const session = requireHost(sessionId, hostToken);
    touch(session);
    const cursor = Math.max(0, Number(after) || 0);
    return session.operations.filter((item) => item.sequence > cursor).map(clone);
  }

  function resolveOperation(sessionId, hostToken, operationId, result = {}) {
    const session = requireHost(sessionId, hostToken);
    const entry = session.operations.find((item) => item.id === operationId);
    if (!entry) throw relayError("操作履歴が見つかりません。", 404, "operation_not_found");
    const status = result.status === "applied" ? "applied" : "rejected";
    entry.status = status;
    entry.summary = String(result.summary || "").slice(0, 240);
    entry.error = String(result.error || "").slice(0, 500);
    entry.resolvedAt = now();
    if (result.snapshot) session.snapshot = clone(result.snapshot);
    touch(session);
    return clone(entry);
  }

  function closeSession(sessionId, hostToken) {
    requireHost(sessionId, hostToken);
    sessions.delete(sessionId);
  }

  return {
    createSession,
    setSnapshot,
    getEditorBootstrap,
    enqueueOperation,
    listOperations,
    resolveOperation,
    closeSession,
  };
}
