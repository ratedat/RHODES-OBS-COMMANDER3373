import * as selectableEffects from "./selectable-effects.js";
import { asCoinEntries, asEffectStackEntries, clampCoinCount, mergeCoinEntries, normalizeCoinFace } from "./special-values.js";

export function normalizeEffectStackEntry(field, entry, campaignId, selectableEffectSource = []) {
  return {
    ...entry,
    count: clampCoinCount(entry.count),
    stateId: selectableEffects.normalizeStackState(field, entry.stateId, campaignId, selectableEffectSource),
  };
}

function effectStackEntryKey(entry) {
  return `${entry.effectId}\u001f${entry.stateId || ""}`;
}

export function mergeEffectStackEntries(field, entries, campaignId, selectableEffectSource = []) {
  const merged = new Map();
  for (const rawEntry of asEffectStackEntries(entries)) {
    const entry = normalizeEffectStackEntry(field, rawEntry, campaignId, selectableEffectSource);
    const key = effectStackEntryKey(entry);
    if (merged.has(key)) {
      const current = merged.get(key);
      current.count = clampCoinCount(current.count + entry.count);
    } else {
      merged.set(key, entry);
    }
  }
  return [...merged.values()];
}

export function normalizeEffectStackEntries(field, campaignId, value, selectableEffectSource = []) {
  const validEffects = new Set(selectableEffects.getEffectStackOptions(selectableEffectSource, field, campaignId).map((item) => item.id));
  const normalized = asEffectStackEntries(value)
    .filter((entry) => validEffects.has(entry.effectId))
    .map((entry) => normalizeEffectStackEntry(field, entry, campaignId, selectableEffectSource));
  return mergeEffectStackEntries(field, normalized, campaignId, selectableEffectSource);
}

export function normalizeCoinLoadoutEntries(field, campaignId, value, selectableEffectSource = []) {
  const validCoins = new Set(selectableEffects.getCoinOptions(selectableEffectSource, field, campaignId).map((item) => item.id));
  const validStatuses = new Set(selectableEffects.getCoinStatusOptions(selectableEffectSource, field, campaignId).map((item) => item.id));
  const normalized = asCoinEntries(value)
    .filter((entry) => validCoins.has(entry.coinId))
    .map((entry) => ({
      ...entry,
      count: clampCoinCount(entry.count),
      statusId: validStatuses.has(entry.statusId) ? entry.statusId : null,
      face: normalizeCoinFace(entry.face),
    }));
  return mergeCoinEntries(normalized);
}