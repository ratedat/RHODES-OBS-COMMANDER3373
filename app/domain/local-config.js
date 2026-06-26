import { normalizeAdbSettings } from "./adb-settings.js";
import { normalizePreferences } from "../lib/preferences.js";

export function extractLocalConfig(state = {}) {
  return {
    adb: normalizeAdbSettings(state?.adb),
    preferences: normalizePreferences({ ...(state?.preferences || {}) }),
  };
}

export function applyLocalConfig(state = {}, config = {}) {
  const next = structuredClone(state || {});
  if (config?.adb) next.adb = normalizeAdbSettings(config.adb);
  if (config?.preferences) next.preferences = normalizePreferences({ ...config.preferences });
  return next;
}

export function preserveLocalConfigOnReset(resetState = {}, previousState = {}) {
  return applyLocalConfig(resetState, extractLocalConfig(previousState));
}
