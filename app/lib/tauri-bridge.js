export function getTauriInvoke(root = globalThis) {
  const invoke = root?.__TAURI__?.core?.invoke || root?.__TAURI__?.tauri?.invoke;
  return typeof invoke === "function" ? invoke : null;
}

export function isTauriRuntime(root = globalThis) {
  return Boolean(getTauriInvoke(root));
}

export async function readTauriStorageTarget(root = globalThis) {
  const invoke = getTauriInvoke(root);
  if (!invoke) return null;
  return invoke("rhodes_storage_target");
}
