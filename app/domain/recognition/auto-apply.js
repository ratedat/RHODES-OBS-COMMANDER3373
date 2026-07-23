import { normalizeRunStatValue } from "../run-stats.js";
import { clampCoinCount, mergeCoinEntries } from "../special-values.js";

export const IS4_AUTO_APPLY_CAMPAIGN_ID = "is4_sami";
export const IS5_AUTO_APPLY_CAMPAIGN_ID = "is5_sarkaz";
export const IS6_AUTO_APPLY_CAMPAIGN_ID = "is6_sui";

const autoApplyProfiles = new Set(["runStatusFull", "relicsFull", "operatorsFull", "is4RevelationFull", "is5ThoughtFull", "is5AgeFull", "is6BaseFull", "is6ActiveCoinsFull", "is6CoinsFull"]);

function candidateFromSuggestion(suggestion = {}) {
  return suggestion.candidate && typeof suggestion.candidate === "object" ? suggestion.candidate : suggestion;
}

function suggestionKey(suggestion = {}) {
  return suggestion.recognitionKey || suggestion.id || null;
}

function numericValue(value) {
  const numeric = Number(value);
  return Number.isFinite(numeric) ? Math.trunc(numeric) : null;
}

function currentCampaignId(state) {
  return state?.run?.campaignId || IS5_AUTO_APPLY_CAMPAIGN_ID;
}

function isIs5State(state) {
  return currentCampaignId(state) === IS5_AUTO_APPLY_CAMPAIGN_ID;
}

function candidateCampaignMatchesState(state, candidate = {}) {
  return !candidate.campaignId || candidate.campaignId === currentCampaignId(state);
}

function ensureIs5Special(run) {
  return ensureCampaignSpecial(run, IS5_AUTO_APPLY_CAMPAIGN_ID);
}

function ensureCampaignSpecial(run, campaignId) {
  run.special ||= {};
  run.special[campaignId] ||= {};
  return run.special[campaignId];
}

function applyRunStatusCandidate(state, candidate) {
  const run = state.run ||= {};
  const field = candidate.field;

  if (field === "ingot") {
    const value = normalizeRunStatValue("ingot", candidate.value);
    if (value === null) return false;
    run[field] = value;
    return true;
  }

  if (field === "difficulty") {
    const value = numericValue(candidate.value);
    if (value === null || value < 1) return false;
    run.difficulty = value;
    return true;
  }

  if (field === "squadId") {
    if (!candidate.value) return false;
    const nextSquadId = String(candidate.value);
    if (run.squadId !== nextSquadId) run.squadRandomEffectOptionId = null;
    run.squadId = nextSquadId;
    run.squad = null;
    return true;
  }

  if (field === "squadRandomEffectOptionId") {
    if (!candidate.value) return false;
    run.squadRandomEffectOptionId = String(candidate.value);
    return true;
  }

  if (field === "idea") {
    const value = numericValue(candidate.value);
    if (value === null || value < 0) return false;
    ensureIs5Special(run).idea = Math.min(999, value);
    return true;
  }

  if (field === "ticket") {
    const value = numericValue(candidate.value);
    if (value === null || value < 0 || currentCampaignId(state) !== IS6_AUTO_APPLY_CAMPAIGN_ID) return false;
    ensureCampaignSpecial(run, IS6_AUTO_APPLY_CAMPAIGN_ID).ticket = Math.min(999, value);
    return true;
  }

  return false;
}

function isCampaignRelicId(relicId, campaignId) {
  return typeof relicId === "string" && Boolean(campaignId) && relicId.startsWith(campaignId + "_relic_");
}

function relicIdFromCandidate(state, candidate = {}) {
  const relicId = candidate.relicId || candidate.value;
  if (!relicId || typeof relicId !== "string") return null;
  const campaignId = currentCampaignId(state);
  if (candidate.campaignId && candidate.campaignId !== campaignId) return null;
  return isCampaignRelicId(relicId, campaignId) ? relicId : null;
}

function applyRelicCandidate(state, candidate) {
  const relicId = relicIdFromCandidate(state, candidate);
  if (!relicId) return false;
  const relics = new Set(Array.isArray(state.relics) ? state.relics : []);
  relics.add(relicId);
  state.relics = [...relics];
  return true;
}

function operatorIdFromCandidate(candidate = {}) {
  const operatorId = candidate.operatorId || candidate.value;
  return typeof operatorId === "string" && operatorId ? operatorId : null;
}

function isReserveOperatorId(operatorId) {
  return typeof operatorId === "string" && operatorId.startsWith("reserve_");
}

function thoughtIdFromCandidate(candidate = {}) {
  const thoughtId = candidate.thoughtId || candidate.value;
  if (!thoughtId || typeof thoughtId !== "string") return null;
  if (candidate.campaignId && candidate.campaignId !== IS5_AUTO_APPLY_CAMPAIGN_ID) return null;
  return thoughtId;
}

function ageIdFromCandidate(candidate = {}) {
  const ageId = candidate.ageId || candidate.value;
  if (!ageId || typeof ageId !== "string") return null;
  if (candidate.campaignId && candidate.campaignId !== IS5_AUTO_APPLY_CAMPAIGN_ID) return null;
  return ageId;
}

function syncRelicFullScanCandidates(state, suggestions = []) {
  const relicSuggestions = [];
  const relicIds = new Set();
  for (const suggestion of suggestions || []) {
    const candidate = candidateFromSuggestion(suggestion);
    if (!canAutoApplySuggestion(state, suggestion, candidate)) continue;
    if (suggestion.profileId !== "relicsFull" || candidate.kind !== "relic") continue;
    const relicId = relicIdFromCandidate(state, candidate);
    if (!relicId) continue;
    relicSuggestions.push(suggestion);
    relicIds.add(relicId);
  }
  if (!relicSuggestions.length) return { applied: [], keys: new Set() };

  const campaignId = currentCampaignId(state);
  const preserved = (Array.isArray(state.relics) ? state.relics : []).filter((relicId) => !isCampaignRelicId(relicId, campaignId));
  state.relics = [...preserved, ...relicIds];
  return {
    applied: relicSuggestions,
    keys: new Set(relicSuggestions.map(suggestionKey).filter(Boolean)),
  };
}

function syncOperatorFullScanCandidates(state, suggestions = []) {
  const operatorSuggestions = [];
  const operatorIds = [];
  const operatorCounts = new Map();
  const seen = new Set();
  for (const suggestion of suggestions || []) {
    const candidate = candidateFromSuggestion(suggestion);
    if (!canAutoApplySuggestion(state, suggestion, candidate)) continue;
    if (suggestion.profileId !== "operatorsFull" || candidate.kind !== "operator") continue;
    const operatorId = operatorIdFromCandidate(candidate);
    if (!operatorId) continue;
    operatorSuggestions.push(suggestion);
    if (!seen.has(operatorId)) {
      seen.add(operatorId);
      operatorIds.push(operatorId);
    }
    if (isReserveOperatorId(operatorId)) {
      const count = Math.max(1, Math.min(99, Math.trunc(Number(candidate.count) || 1)));
      operatorCounts.set(operatorId, Math.max(operatorCounts.get(operatorId) || 1, count));
    }
  }
  if (!operatorSuggestions.length) return { applied: [], keys: new Set() };

  state.operators = operatorIds;
  state.operatorCounts = Object.fromEntries([...operatorCounts].filter(([, count]) => count > 1));
  return {
    applied: operatorSuggestions,
    keys: new Set(operatorSuggestions.map(suggestionKey).filter(Boolean)),
  };
}

function syncIs5ThoughtFullScanCandidates(state, suggestions = []) {
  const thoughtSuggestions = [];
  const thoughtCounts = new Map();
  for (const suggestion of suggestions || []) {
    const candidate = candidateFromSuggestion(suggestion);
    if (!canAutoApplySuggestion(state, suggestion, candidate)) continue;
    if (suggestion.profileId !== "is5ThoughtFull" || candidate.kind !== "thought") continue;
    const thoughtId = thoughtIdFromCandidate(candidate);
    if (!thoughtId) continue;
    thoughtSuggestions.push(suggestion);
    thoughtCounts.set(thoughtId, (thoughtCounts.get(thoughtId) || 0) + 1);
  }
  if (!thoughtSuggestions.length) return { applied: [], keys: new Set() };

  const run = state.run ||= {};
  const special = ensureIs5Special(run);
  special.thought = [...thoughtCounts.entries()].map(([effectId, count]) => ({ effectId, count, stateId: null }));
  special.thoughtOverlayVisible = true;
  return {
    applied: thoughtSuggestions,
    keys: new Set(thoughtSuggestions.map(suggestionKey).filter(Boolean)),
  };
}

function syncIs5AgeFullScanCandidates(state, suggestions = []) {
  const ageSuggestions = [];
  for (const suggestion of suggestions || []) {
    const candidate = candidateFromSuggestion(suggestion);
    if (!canAutoApplySuggestion(state, suggestion, candidate)) continue;
    if (suggestion.profileId !== "is5AgeFull" || candidate.kind !== "age") continue;
    if (!ageIdFromCandidate(candidate)) continue;
    ageSuggestions.push(suggestion);
  }
  if (!ageSuggestions.length) return { applied: [], keys: new Set() };

  const best = ageSuggestions.toSorted((left, right) => Number(candidateFromSuggestion(right).confidence || 0) - Number(candidateFromSuggestion(left).confidence || 0))[0];
  const run = state.run ||= {};
  ensureIs5Special(run).age = ageIdFromCandidate(candidateFromSuggestion(best));
  return {
    applied: ageSuggestions,
    keys: new Set(ageSuggestions.map(suggestionKey).filter(Boolean)),
  };
}

function normalizeRevelationFieldId(fieldId) {
  const value = String(fieldId || "").trim();
  return !value || value === "revelationBoard" ? "revelation" : value;
}

function mergeRevelationRhetorics(entries = []) {
  const merged = new Map();
  for (const entry of entries) {
    if (!entry?.effectId) continue;
    const count = clampCoinCount(entry.count);
    if (merged.has(entry.effectId)) {
      const current = merged.get(entry.effectId);
      current.count = clampCoinCount(current.count + count);
    } else {
      merged.set(entry.effectId, { effectId: entry.effectId, count, stateId: null });
    }
  }
  return [...merged.values()];
}

function revelationBoardDraft() {
  return { causeId: null, structureId: null, rhetorics: [] };
}

function revelationEffectIdFromCandidate(candidate = {}) {
  const effectId = candidate.effectId || candidate.value;
  return typeof effectId === "string" && effectId.trim() ? effectId.trim() : null;
}

function syncIs4RevelationFullScanCandidates(state, suggestions = []) {
  const revelationSuggestions = [];
  const boards = new Map();
  for (const suggestion of suggestions || []) {
    const candidate = candidateFromSuggestion(suggestion);
    if (!canAutoApplySuggestion(state, suggestion, candidate)) continue;
    if (suggestion.profileId !== "is4RevelationFull" || candidate.kind !== "revelation") continue;
    const effectId = revelationEffectIdFromCandidate(candidate);
    if (!effectId) continue;

    const fieldId = normalizeRevelationFieldId(candidate.fieldId);
    const slotKind = String(candidate.slotKind || "").trim().toLowerCase();
    if (!["cause", "causeid", "structure", "structureid", "rhetoric", "rhetoricid"].includes(slotKind)) continue;

    const board = boards.get(fieldId) || revelationBoardDraft();
    boards.set(fieldId, board);
    if (slotKind === "cause" || slotKind === "causeid") board.causeId = effectId;
    else if (slotKind === "structure" || slotKind === "structureid") board.structureId = effectId;
    else board.rhetorics.push({ effectId, count: candidate.count || 1, stateId: null });
    revelationSuggestions.push(suggestion);
  }
  if (!revelationSuggestions.length) return { applied: [], keys: new Set() };

  const run = state.run ||= {};
  const campaign = ensureCampaignSpecial(run, IS4_AUTO_APPLY_CAMPAIGN_ID);
  for (const [fieldId, board] of boards.entries()) {
    campaign[fieldId] = {
      causeId: board.causeId,
      structureId: board.structureId,
      rhetorics: mergeRevelationRhetorics(board.rhetorics),
    };
  }
  return {
    applied: revelationSuggestions,
    keys: new Set(revelationSuggestions.map(suggestionKey).filter(Boolean)),
  };
}

function coinEntryFromCandidate(state, candidate = {}) {
  if (!candidateCampaignMatchesState(state, candidate)) return null;
  const coinId = candidate.coinId || candidate.value;
  if (!coinId || typeof coinId !== "string") return null;
  return {
    coinId,
    count: clampCoinCount(candidate.count),
    statusId: candidate.statusId || null,
  };
}

function syncIs6CoinFullScanCandidates(state, suggestions = []) {
  const coinSuggestions = [];
  const entriesByField = new Map();
  for (const suggestion of suggestions || []) {
    const candidate = candidateFromSuggestion(suggestion);
    if (!canAutoApplySuggestion(state, suggestion, candidate)) continue;
    if (!["is6ActiveCoinsFull", "is6CoinsFull"].includes(suggestion.profileId) || candidate.kind !== "coin") continue;
    const entry = coinEntryFromCandidate(state, candidate);
    if (!entry) continue;
    const fieldId = String(candidate.fieldId || (suggestion.profileId === "is6ActiveCoinsFull" ? "activeCoins" : "coins")).trim();
    if (!["activeCoins", "coins"].includes(fieldId)) continue;
    entriesByField.set(fieldId, [...(entriesByField.get(fieldId) || []), entry]);
    coinSuggestions.push(suggestion);
  }
  if (!coinSuggestions.length) return { applied: [], keys: new Set() };

  const run = state.run ||= {};
  const campaign = ensureCampaignSpecial(run, IS6_AUTO_APPLY_CAMPAIGN_ID);
  for (const [fieldId, entries] of entriesByField.entries()) {
    campaign[fieldId] = mergeCoinEntries(entries);
  }
  return {
    applied: coinSuggestions,
    keys: new Set(coinSuggestions.map(suggestionKey).filter(Boolean)),
  };
}

function canAutoApplySuggestion(state, suggestion, candidate) {
  if (!autoApplyProfiles.has(suggestion.profileId)) return false;
  if (candidate.kind === "operator") return suggestion.profileId === "operatorsFull";
  if (candidate.kind === "runStatus") {
    return ["runStatusFull", "is6BaseFull"].includes(suggestion.profileId)
      && candidateCampaignMatchesState(state, candidate);
  }
  if (candidate.kind === "relic") return suggestion.profileId === "relicsFull" && candidateCampaignMatchesState(state, candidate);
  if (candidate.kind === "revelation") {
    return currentCampaignId(state) === IS4_AUTO_APPLY_CAMPAIGN_ID
      && suggestion.profileId === "is4RevelationFull"
      && (!candidate.campaignId || candidate.campaignId === IS4_AUTO_APPLY_CAMPAIGN_ID);
  }
  if (candidate.kind === "thought") {
    return isIs5State(state)
      && suggestion.profileId === "is5ThoughtFull"
      && (!candidate.campaignId || candidate.campaignId === IS5_AUTO_APPLY_CAMPAIGN_ID);
  }
  if (candidate.kind === "age") {
    return isIs5State(state)
      && suggestion.profileId === "is5AgeFull"
      && (!candidate.campaignId || candidate.campaignId === IS5_AUTO_APPLY_CAMPAIGN_ID);
  }
  if (candidate.kind === "coin") {
    return currentCampaignId(state) === IS6_AUTO_APPLY_CAMPAIGN_ID
      && ["is6ActiveCoinsFull", "is6CoinsFull"].includes(suggestion.profileId)
      && (!candidate.campaignId || candidate.campaignId === IS6_AUTO_APPLY_CAMPAIGN_ID);
  }
  return false;
}

function addSyncedSuggestions(target, synced) {
  for (const suggestion of synced.applied) target.autoApplied.push(suggestion);
  for (const key of synced.keys) target.autoAppliedKeys.add(key);
}

function hasValidAgeSuggestion(suggestions = []) {
  return (suggestions || []).some((suggestion) => {
    const candidate = candidateFromSuggestion(suggestion);
    return suggestion.profileId === "is5AgeFull" && candidate.kind === "age" && Boolean(ageIdFromCandidate(candidate));
  });
}

export function applyRecognitionSuggestionsToState(state, suggestions = []) {
  const next = structuredClone(state || {});
  const remainingSuggestions = [];
  const autoApplied = [];
  const autoAppliedKeys = new Set();
  const syncedTarget = { autoApplied, autoAppliedKeys };

  addSyncedSuggestions(syncedTarget, syncOperatorFullScanCandidates(next, suggestions));
  addSyncedSuggestions(syncedTarget, syncRelicFullScanCandidates(next, suggestions));
  addSyncedSuggestions(syncedTarget, syncIs4RevelationFullScanCandidates(next, suggestions));
  addSyncedSuggestions(syncedTarget, syncIs5ThoughtFullScanCandidates(next, suggestions));
  addSyncedSuggestions(syncedTarget, syncIs5AgeFullScanCandidates(next, suggestions));
  addSyncedSuggestions(syncedTarget, syncIs6CoinFullScanCandidates(next, suggestions));

  for (const suggestion of suggestions || []) {
    const existingKey = suggestionKey(suggestion);
    if (existingKey && autoAppliedKeys.has(existingKey)) continue;
    const candidate = candidateFromSuggestion(suggestion);
    let applied = false;
    if (canAutoApplySuggestion(next, suggestion, candidate)) {
      if (candidate.kind === "runStatus") applied = applyRunStatusCandidate(next, candidate);
      else if (candidate.kind === "relic") applied = applyRelicCandidate(next, candidate);
    }

    if (applied) {
      autoApplied.push(suggestion);
      const key = suggestionKey(suggestion);
      if (key) autoAppliedKeys.add(key);
    } else {
      remainingSuggestions.push(suggestion);
    }
  }

  if (autoAppliedKeys.size && Array.isArray(next.pendingSuggestions)) {
    next.pendingSuggestions = next.pendingSuggestions.filter((suggestion) => !autoAppliedKeys.has(suggestionKey(suggestion)));
  }

  return { state: next, autoApplied, remainingSuggestions };
}


export function applyRecognitionScanCompletionToState(state, { profileId = null, suggestions = [] } = {}) {
  const applied = applyRecognitionSuggestionsToState(state, suggestions);
  const next = applied.state;
  if (profileId === "is5AgeFull" && isIs5State(next) && !hasValidAgeSuggestion(suggestions)) {
    const run = next.run ||= {};
    ensureIs5Special(run).age = null;
  }
  return { ...applied, state: next };
}
