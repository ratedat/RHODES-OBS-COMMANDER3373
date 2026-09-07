export const EDITOR_PERSISTENCE_VERSION = 1;

function storageKey(sessionId) {
  return `rhodes-tournament-editor-state:${sessionId}`;
}

function clone(value) {
  return value === undefined ? undefined : structuredClone(value);
}

function persistenceError(action, cause) {
  return Object.assign(new Error(`入力内容の端末保存を${action}できません。`, { cause }), {
    code: "editor_persistence_failed",
  });
}

function validateOperation(operation) {
  return Boolean(
    operation
    && typeof operation === "object"
    && !Array.isArray(operation)
    && typeof operation.type === "string",
  );
}

function validatePersistence(value) {
  if (!value || typeof value !== "object" || Array.isArray(value)) return false;
  if (value.version !== EDITOR_PERSISTENCE_VERSION) return false;
  if (
    typeof value.editorClientId !== "string"
    || !value.editorClientId.trim()
    || value.editorClientId.length > 128
  ) return false;
  if (!Array.isArray(value.pendingOperations) || !value.pendingOperations.every(validateOperation)) return false;
  if (value.submission === null) return true;
  return Boolean(
    value.submission
    && typeof value.submission === "object"
    && !Array.isArray(value.submission)
    && typeof value.submission.clientOperationId === "string"
    && value.submission.clientOperationId.trim()
    && value.submission.clientOperationId.length <= 128
    && value.pendingOperations.length > 0
    && (value.submission.relayOperationId === undefined
      || typeof value.submission.relayOperationId === "string"),
  );
}

export function createEditorPersistence(editorClientId) {
  const normalizedEditorClientId = String(editorClientId || "").trim();
  if (!normalizedEditorClientId) throw new TypeError("editorClientId is required");
  return {
    version: EDITOR_PERSISTENCE_VERSION,
    editorClientId: normalizedEditorClientId,
    pendingOperations: [],
    submission: null,
  };
}

export function loadEditorPersistence(storage, sessionId) {
  try {
    const serialized = storage.getItem(storageKey(sessionId));
    if (serialized === null) return null;
    const value = JSON.parse(serialized);
    if (!validatePersistence(value)) throw new TypeError("invalid editor persistence");
    return clone(value);
  } catch (error) {
    throw persistenceError("読み込み", error);
  }
}

export function saveEditorPersistence(storage, sessionId, value) {
  if (!validatePersistence(value)) {
    throw persistenceError("保存", new TypeError("invalid editor persistence"));
  }
  try {
    storage.setItem(storageKey(sessionId), JSON.stringify(value));
  } catch (error) {
    throw persistenceError("保存", error);
  }
}

export function prepareEditorSubmission(value, createId) {
  if (!validatePersistence(value)) throw new TypeError("invalid editor persistence");
  if (value.submission) return clone(value);
  if (!value.pendingOperations.length) throw new TypeError("pendingOperations is empty");
  const clientOperationId = String(createId?.() || "").trim();
  if (!clientOperationId) throw new TypeError("clientOperationId is required");
  return {
    ...clone(value),
    submission: {
      clientOperationId,
      relayOperationId: "",
    },
  };
}

export function reconcileEditorSubmission(value, operation) {
  if (!validatePersistence(value)) throw new TypeError("invalid editor persistence");
  if (!value.submission || !operation) return clone(value);
  if (operation.status === "applied") return createEditorPersistence(value.editorClientId);
  if (operation.status === "rejected") {
    return {
      ...clone(value),
      submission: null,
    };
  }
  if (operation.status === "pending") {
    return {
      ...clone(value),
      submission: {
        ...value.submission,
        relayOperationId: String(operation.id || value.submission.relayOperationId || ""),
      },
    };
  }
  return clone(value);
}

export function returnEditorSubmissionToDraft(value) {
  if (!validatePersistence(value)) throw new TypeError("invalid editor persistence");
  return {
    ...clone(value),
    submission: null,
  };
}

function operationCount(entry) {
  if (entry?.operation?.type === "batch" && Array.isArray(entry.operation.operations)) {
    return entry.operation.operations.length;
  }
  return entry?.operation ? 1 : 0;
}

export function describeEditorPendingState(value, pendingEntries = [], limits = {}) {
  if (!validatePersistence(value)) throw new TypeError("invalid editor persistence");
  const pending = pendingEntries.filter((entry) => entry?.status === "pending");
  const ownPending = pending.find((entry) => (
    (value.submission?.clientOperationId
      && entry.clientOperationId === value.submission.clientOperationId)
    || (entry.editorClientId && entry.editorClientId === value.editorClientId)
  ));
  const foreignPending = pending.find((entry) => entry !== ownPending);
  const draftCount = value.pendingOperations.length;

  if (ownPending) {
    const count = draftCount || operationCount(ownPending);
    return {
      mode: "own-pending",
      summary: count ? `${count}件の変更を送信済み` : "変更を送信済み",
      detail: "配信PCでの反映完了を待っています。",
      sendLabel: "反映待ち",
      disableEditor: true,
      disableSend: true,
      disableDiscard: true,
    };
  }
  if (foreignPending) {
    return {
      mode: "foreign-pending",
      summary: "前担当の変更が反映待ちです",
      detail: "反映結果が確定すると、この端末から変更を送信できます。",
      sendLabel: "前担当の反映待ち",
      disableEditor: true,
      disableSend: true,
      disableDiscard: true,
    };
  }
  if (value.submission) {
    return {
      mode: "retry",
      summary: draftCount ? `${draftCount}件の送信結果を確認中` : "送信結果を確認中",
      detail: "中継で結果を確認できません。同じ内容と操作IDで再送できます。",
      sendLabel: "同じ内容を再送",
      disableEditor: true,
      disableSend: draftCount === 0,
      disableDiscard: true,
    };
  }
  if (limits.remainingOperations === 0) {
    const maxOperations = Number.isSafeInteger(limits.maxOperations) && limits.maxOperations > 0
      ? limits.maxOperations
      : null;
    return {
      mode: "operation-limit",
      summary: maxOperations
        ? `このセッションは${maxOperations}件の操作上限に達しました`
        : "このセッションは操作上限に達しました",
      detail: "下書きは保持されています。配信担当者が新しいセッションを開始する必要があります。",
      sendLabel: "新しいセッションが必要",
      disableEditor: false,
      disableSend: true,
      disableDiscard: draftCount === 0,
    };
  }
  if (draftCount) {
    return {
      mode: "draft",
      summary: `${draftCount}件の未送信変更`,
      detail: "内容を確認し、「変更を送信」でまとめて即時反映します。",
      sendLabel: "変更を送信",
      disableEditor: false,
      disableSend: false,
      disableDiscard: false,
    };
  }
  return {
    mode: "empty",
    summary: "未送信の変更はありません",
    detail: "入力内容は、この端末に保存されます。",
    sendLabel: "変更を送信",
    disableEditor: false,
    disableSend: true,
    disableDiscard: true,
  };
}
