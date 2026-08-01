import * as selectableEffects from "./selectable-effects.js";
import { asCoinEntries, asEffectStackEntries, asRevelationBoardValue, clampCoinCount, mergeCoinEntries } from "./special-values.js";

export function normalizeEffectStackEntry(field, entry, campaignId, selectableEffectSource = []) {
  return {
    ...entry,
    count: clampCoinCount(entry.count),
    stateId: field?.hideStateInput ? null : selectableEffects.normalizeStackState(field, entry.stateId, campaignId, selectableEffectSource),
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
      coinId: entry.coinId,
      count: clampCoinCount(entry.count),
      statusId: validStatuses.has(entry.statusId) ? entry.statusId : null,
    }));
  return mergeCoinEntries(normalized);
}
function revelationBoardOptionSet(field, campaignId, group, selectableEffectSource) {
  return new Set(selectableEffects.getRevelationBoardOptions(selectableEffectSource, field, campaignId, group).map((item) => item.id));
}

export function mergeRevelationRhetorics(entries) {
  const merged = new Map();
  for (const entry of asEffectStackEntries(entries)) {
    const key = entry.effectId;
    if (merged.has(key)) {
      const current = merged.get(key);
      current.count = clampCoinCount(current.count + entry.count);
    } else {
      merged.set(key, { effectId: entry.effectId, count: clampCoinCount(entry.count) });
    }
  }
  return [...merged.values()];
}

function revelationEntryKey(entry) {
  return `${entry.slotKind}\u001f${entry.effectId}\u001f${entry.stateId || ""}`;
}

function normalizeRevelationEntries(entrySource, causeOptions, structureOptions, rhetoricOptions) {
  const merged = new Map();
  for (const rawEntry of entrySource || []) {
    if (!rawEntry || typeof rawEntry !== "object") continue;

    const effectId = String(rawEntry.effectId || rawEntry.id || "").trim();
    const requestedSlot = String(rawEntry.slotKind || rawEntry.slot || "").trim().toLowerCase();
    const inferredSlot = causeOptions.has(effectId)
      ? "cause"
      : structureOptions.has(effectId)
        ? "structure"
        : "";
    const slotKind = requestedSlot === "cause" || requestedSlot === "structure"
      ? requestedSlot
      : inferredSlot;
    if (!effectId || !slotKind) continue;
    if (slotKind === "cause" && !causeOptions.has(effectId)) continue;
    if (slotKind === "structure" && !structureOptions.has(effectId)) continue;

    const rawStateId = String(rawEntry.stateId || rawEntry.state || rawEntry.statusId || "").trim();
    const entry = {
      effectId,
      stateId: rawStateId && rhetoricOptions.has(rawStateId) ? rawStateId : null,
      slotKind,
      count: clampCoinCount(rawEntry.count),
    };
    const key = revelationEntryKey(entry);
    if (merged.has(key)) {
      const current = merged.get(key);
      current.count = clampCoinCount(current.count + entry.count);
    } else {
      merged.set(key, entry);
    }
  }
  return [...merged.values()];
}

export function normalizeRevelationBoardValue(field, campaignId, value, selectableEffectSource = []) {
  const causeOptions = revelationBoardOptionSet(field, campaignId, "cause", selectableEffectSource);
  const structureOptions = revelationBoardOptionSet(field, campaignId, "structure", selectableEffectSource);
  const rhetoricOptions = revelationBoardOptionSet(field, campaignId, "rhetoric", selectableEffectSource);

  const entrySource = Array.isArray(value)
    ? value
    : Array.isArray(value?.entries)
      ? value.entries
      : null;
  if (entrySource) {
    return { entries: normalizeRevelationEntries(entrySource, causeOptions, structureOptions, rhetoricOptions) };
  }

  const raw = asRevelationBoardValue(value);
  const entries = [];
  if (causeOptions.has(raw.causeId)) {
    entries.push({ effectId: raw.causeId, stateId: null, slotKind: "cause", count: 1 });
  }
  if (structureOptions.has(raw.structureId)) {
    entries.push({ effectId: raw.structureId, stateId: null, slotKind: "structure", count: 1 });
  }
  const rhetorics = mergeRevelationRhetorics(raw.rhetorics.filter((entry) => rhetoricOptions.has(entry.effectId)));
  return rhetorics.length > 0 ? { entries, rhetorics } : { entries };
}
