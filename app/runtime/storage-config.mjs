import path from "node:path";

export const STORAGE_POINTER_FILE = "storage-location.json";
export const PORTABLE_STORAGE_DIRNAME = "RHODES OBS COMMANDER3373 Data";
export const DEV_STORAGE_DIRNAME = "user-data";
export const DOCUMENTS_STORAGE_DIRNAME = "RHODES OBS COMMANDER3373";
export const storageModeOptions = Object.freeze(["portable", "documents", "custom"]);

export function normalizeStorageMode(value, fallback = "portable") {
  return storageModeOptions.includes(value) ? value : fallback;
}

function cleanPath(value) {
  return typeof value === "string" ? value.trim() : "";
}

export function defaultDocumentsPath({ homeDir = "" } = {}) {
  return path.join(homeDir || process.cwd(), "Documents");
}

export function portableStorageDir({ appRoot = process.cwd(), execPath = process.execPath, isPackaged = false } = {}) {
  const base = isPackaged && execPath ? path.dirname(execPath) : appRoot;
  return path.join(base, isPackaged ? PORTABLE_STORAGE_DIRNAME : DEV_STORAGE_DIRNAME);
}

export function documentsStorageDir({ documentsPath = defaultDocumentsPath() } = {}) {
  return path.join(documentsPath, DOCUMENTS_STORAGE_DIRNAME);
}

export function storageTarget({ mode = "portable", storageDir = "", appRoot, execPath, documentsPath, isPackaged } = {}) {
  const normalizedMode = normalizeStorageMode(mode);
  const baseDir = cleanPath(storageDir)
    || (normalizedMode === "documents" ? documentsStorageDir({ documentsPath }) : portableStorageDir({ appRoot, execPath, isPackaged }));
  return {
    mode: normalizedMode,
    storageDir: baseDir,
    settingsFile: path.join(baseDir, "desktop-settings.json"),
    stateDir: normalizedMode === "custom" ? baseDir : path.join(baseDir, "state"),
    userDataDir: normalizedMode === "custom" ? path.join(baseDir, "electron") : path.join(baseDir, "electron"),
  };
}

export function storagePointerPath(context = {}) {
  return path.join(portableStorageDir(context), STORAGE_POINTER_FILE);
}

export function parseStoragePointer(text) {
  try {
    const parsed = JSON.parse(text);
    const mode = normalizeStorageMode(parsed?.mode, "");
    return mode ? { mode, storageDir: cleanPath(parsed?.storageDir) } : null;
  } catch {
    return null;
  }
}

export function serializeStoragePointer(target) {
  return `${JSON.stringify({ mode: normalizeStorageMode(target?.mode), storageDir: cleanPath(target?.storageDir) }, null, 2)}\n`;
}

export function targetFromStoredSelection(selection, context = {}) {
  if (!selection) return null;
  const mode = normalizeStorageMode(selection.mode, "");
  if (!mode) return null;
  return storageTarget({ ...context, mode, storageDir: selection.storageDir });
}
