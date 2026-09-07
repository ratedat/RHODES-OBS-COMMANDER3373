import { randomBytes, randomUUID } from "node:crypto";
import { isDeepStrictEqual } from "node:util";

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
  maxOperations = 500,
} = {}) {
  const sessions = new Map();
  const operationLimit = Number.isSafeInteger(maxOperations) && maxOperations > 0
    ? maxOperations
    : 500;

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
      snapshotGeneration: null,
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

  function readSnapshotGeneration(snapshot) {
    const value = snapshot?.snapshotGeneration;
    if (value === undefined || value === null) return null;
    if (!Number.isSafeInteger(value) || value < 1) {
      throw relayError("スナップショット世代が不正です。", 400, "invalid_snapshot_generation");
    }
    return value;
  }

  function acceptSnapshot(session, snapshot) {
    const generation = readSnapshotGeneration(snapshot);
    if (generation === null) {
      // Generation-less hosts keep their previous last-arrival-wins behavior until
      // this session receives its first generation-aware snapshot.
      if (session.snapshotGeneration !== null) return false;
    } else {
      if (session.snapshotGeneration !== null && generation <= session.snapshotGeneration) return false;
      session.snapshotGeneration = generation;
    }
    session.snapshot = clone(snapshot);
    return true;
  }

  function setSnapshot(sessionId, hostToken, snapshot) {
    const session = requireHost(sessionId, hostToken);
    const accepted = acceptSnapshot(session, snapshot);
    touch(session);
    return {
      updatedAt: session.updatedAt,
      accepted,
      snapshotGeneration: session.snapshotGeneration,
    };
  }

  function editorEntry(item) {
    return {
      id: item.id,
      sequence: item.sequence,
      clientOperationId: item.clientOperationId || "",
      editorClientId: item.editorClientId || "",
      operation: clone(item.operation),
      status: item.status,
      summary: item.summary || "",
      error: item.error || "",
      createdAt: item.createdAt,
      resolvedAt: item.resolvedAt || null,
    };
  }

  function getEditorBootstrap(sessionId, editorCode, {
    editorClientId = "",
    clientOperationId = "",
  } = {}) {
    const session = requireEditor(sessionId, editorCode);
    touch(session);
    const normalizedEditorClientId = String(editorClientId || "").trim();
    const normalizedClientOperationId = String(clientOperationId || "").trim();
    const submittedOperation = normalizedEditorClientId && normalizedClientOperationId
      ? session.operations.find((item) => (
        item.editorClientId === normalizedEditorClientId
        && item.clientOperationId === normalizedClientOperationId
      ))
      : null;
    return {
      sessionId: session.sessionId,
      playerLabel: session.playerLabel,
      expiresAt: session.expiresAt,
      snapshot: clone(session.snapshot),
      history: session.operations.slice(-100).reverse().map(editorEntry),
      pendingOperations: session.operations.filter((item) => item.status === "pending").map(editorEntry),
      submittedOperation: submittedOperation ? editorEntry(submittedOperation) : null,
      limits: {
        maxOperations: operationLimit,
        remainingOperations: Math.max(0, operationLimit - session.operations.length),
      },
    };
  }

  function enqueueOperation(sessionId, editorCode, operation, {
    clientOperationId = "",
    editorClientId = "",
  } = {}) {
    const session = requireEditor(sessionId, editorCode);
    if (!operation || typeof operation !== "object" || Array.isArray(operation)) {
      throw relayError("操作形式が不正です。", 400, "invalid_operation");
    }
    const normalizedClientOperationId = String(clientOperationId || "").trim();
    const normalizedEditorClientId = String(editorClientId || "").trim();
    if (Boolean(normalizedClientOperationId) !== Boolean(normalizedEditorClientId)) {
      throw relayError(
        "操作IDと入力端末IDは両方指定してください。",
        400,
        "invalid_client_operation_identity",
      );
    }
    if (normalizedClientOperationId.length > 128 || normalizedEditorClientId.length > 128) {
      throw relayError("操作IDまたは入力端末IDが長すぎます。", 400, "invalid_client_operation_identity");
    }
    if (normalizedClientOperationId) {
      const existing = session.operations.find((item) => item.clientOperationId === normalizedClientOperationId);
      if (existing) {
        if (
          existing.editorClientId !== normalizedEditorClientId
          || !isDeepStrictEqual(existing.operation, operation)
        ) {
          throw relayError(
            "同じ操作IDを異なる内容へ再利用できません。",
            409,
            "client_operation_conflict",
          );
        }
        touch(session);
        return clone(existing);
      }
    }
    if (session.operations.some((item) => item.status === "pending")) {
      throw relayError(
        "別の変更が配信PCで反映待ちです。完了後に再送してください。",
        409,
        "pending_operation_conflict",
      );
    }
    if (session.operations.length >= operationLimit) {
      throw relayError(
        `このセッションの操作上限（${operationLimit}件）に達しました。新しいセッションを開始してください。`,
        429,
        "operation_limit_reached",
      );
    }
    const entry = {
      id: randomUUID(),
      sequence: session.nextSequence++,
      clientOperationId: normalizedClientOperationId,
      editorClientId: normalizedEditorClientId,
      operation: clone(operation),
      status: "pending",
      summary: "",
      error: "",
      createdAt: now(),
      resolvedAt: null,
    };
    session.operations.push(entry);
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
    if (entry.status !== "pending") {
      if (entry.status !== status) {
        throw relayError("確定済みの操作結果は変更できません。", 409, "operation_already_resolved");
      }
      touch(session);
      return clone(entry);
    }
    if (result.snapshot) acceptSnapshot(session, result.snapshot);
    entry.status = status;
    entry.summary = String(result.summary || "").slice(0, 240);
    entry.error = String(result.error || "").slice(0, 500);
    entry.resolvedAt = now();
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
