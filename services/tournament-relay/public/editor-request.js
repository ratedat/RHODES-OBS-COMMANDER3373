export async function requestEditorJson(path, options = {}, {
  fetchImpl = globalThis.fetch,
  timeoutMs = 10_000,
} = {}) {
  const controller = new AbortController();
  const timeout = setTimeout(() => {
    controller.abort(Object.assign(new Error("通信がタイムアウトしました。"), {
      code: "request_timeout",
    }));
  }, timeoutMs);

  try {
    const response = await fetchImpl(path, {
      cache: "no-store",
      ...options,
      signal: controller.signal,
      headers: {
        ...(options.body ? { "content-type": "application/json" } : {}),
        ...(options.headers || {}),
      },
    });
    let payload;
    try {
      payload = await response.json();
    } catch (error) {
      if (controller.signal.aborted) throw controller.signal.reason || error;
      throw Object.assign(new Error("中継サーバーの応答を読み取れませんでした。", { cause: error }), {
        code: "invalid_relay_response",
        status: response.status,
      });
    }
    if (!response.ok) {
      throw Object.assign(new Error(payload.error || `HTTP ${response.status}`), {
        code: payload.code || "relay_error",
        status: response.status,
      });
    }
    return payload;
  } catch (error) {
    if (controller.signal.aborted) {
      throw Object.assign(new Error("通信がタイムアウトしました。再確認後も未反映なら同じ内容を再送できます。", {
        cause: error,
      }), {
        code: "request_timeout",
      });
    }
    throw error;
  } finally {
    clearTimeout(timeout);
  }
}
