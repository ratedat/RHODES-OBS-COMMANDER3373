import { DEFAULT_PORT, normalizePort, readArg } from "./local-server.mjs";
import { normalizeStorageMode } from "./storage-config.mjs";

export const DESKTOP_SETTINGS_FILE = "desktop-settings.json";

export function explicitPortValue(args = [], env = {}) {
  return readArg(args, "--port", env.PORT || null);
}

export function shouldPromptForPort(args = [], env = {}, { smokeTest = false } = {}) {
  if (smokeTest) return false;
  return explicitPortValue(args, env) == null;
}

export function parseDesktopSettings(text) {
  try {
    const parsed = JSON.parse(text);
    return {
      port: parsed?.port ?? null,
      storageMode: normalizeStorageMode(parsed?.storageMode, null),
      storageDir: typeof parsed?.storageDir === "string" ? parsed.storageDir : "",
    };
  } catch {
    return { port: null, storageMode: null, storageDir: "" };
  }
}

export function serializeDesktopSettings({ port, storageMode = null, storageDir = "" }) {
  const payload = { port: normalizePort(port) };
  const normalizedStorageMode = normalizeStorageMode(storageMode, null);
  if (normalizedStorageMode) {
    payload.storageMode = normalizedStorageMode;
    payload.storageDir = storageDir || "";
  }
  return `${JSON.stringify(payload, null, 2)}\n`;
}

export function resolveStartupPort({ args = [], env = {}, savedPort = null, defaultPort = DEFAULT_PORT } = {}) {
  return normalizePort(explicitPortValue(args, env) ?? savedPort ?? defaultPort);
}
