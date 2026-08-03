import { getBossManualSections, normalizeBossSelections } from "./boss-flags.js";
import { applyDifficultyTier } from "./difficulty.js";
import { normalizeOperatorCounts } from "./operator-counts.js";
import { normalizeOperatorPromotionLevels } from "./operator-promotions.js";
import {
  normalizeCoinLoadoutEntries,
  normalizeEffectStackEntries,
  normalizeRevelationBoardValue,
} from "./special-loadouts.js";
import { getSelectableEffectsForField } from "./selectable-effects.js";
import { asSpecialArray, asSpecialObject, clampSpecialNumber } from "./special-values.js";

const RUN_FIELDS = new Set([
  "ingot",
  "difficulty",
  "squadId",
  "difficultyTierId",
  "performanceId",
  "squadRandomEffectOptionId",
]);
const MAX_TEXT_LENGTH = 160;

function operationError(message) {
  return Object.assign(new Error(message), { code: "invalid_tournament_operation" });
}

function boundedText(value, max = MAX_TEXT_LENGTH) {
  const text = value === null || value === undefined ? "" : String(value).trim();
  return text.slice(0, max);
}

function campaignById(master, campaignId) {
  const campaign = (master?.campaigns || []).find((item) => item.id === campaignId);
  if (!campaign) throw operationError(`存在しない統合戦略です: ${campaignId || "(未指定)"}`);
  return campaign;
}

function currentCampaign(state, master) {
  return campaignById(master, state?.run?.campaignId);
}

function difficultyTierEntries(master, campaignId) {
  const config = master?.difficultyTiers?.[campaignId];
  if (Array.isArray(config)) return config;
  return Array.isArray(config?.tiers) ? config.tiers : [];
}

function specialField(campaign, fieldId) {
  const field = (campaign?.specialFields || []).find((item) => item.id === fieldId);
  if (!field) throw operationError(`許可されていない特殊値です: ${fieldId || "(未指定)"}`);
  return field;
}

function validEffectIds(master, campaignId, field) {
  return new Set(getSelectableEffectsForField(master?.selectableEffects || [], field, campaignId).map((item) => item.id));
}

function normalizeEffectSelect(master, campaignId, field, value) {
  const valid = validEffectIds(master, campaignId, field);
  const id = boundedText(value);
  return valid.has(id) ? id : null;
}

function normalizeEffectMultiSelect(master, campaignId, field, value) {
  const valid = validEffectIds(master, campaignId, field);
  return [...new Set(asSpecialArray(value).map(boundedText).filter((id) => valid.has(id)))];
}

function normalizeRankedMultiSelect(master, campaignId, field, value) {
  const valid = validEffectIds(master, campaignId, field);
  const raw = asSpecialObject(value);
  return Object.fromEntries(
    Object.entries(raw)
      .map(([key, id]) => [boundedText(key, 80), boundedText(id)])
      .filter(([key, id]) => key && valid.has(id)),
  );
}

function operatorIdSet(master) {
  return new Set((master?.operators || []).map((item) => item.id));
}

function supportsEliteTwo(master, operatorId) {
  const operator = (master?.operators || []).find((item) => item.id === operatorId);
  return Number(operator?.rarity) >= 4;
}

function normalizeOperatorTargets(master, value) {
  const valid = operatorIdSet(master);
  const raw = Array.isArray(value?.operatorTargets) ? value.operatorTargets : [];
  const targets = [];
  const seen = new Set();
  for (const item of raw) {
    const operatorId = boundedText(item?.operatorId || item?.id, 100);
    if (!valid.has(operatorId)) continue;
    const instance = Math.max(1, Math.min(99, Math.trunc(Number(item?.instance) || 1)));
    const key = `${operatorId}\u001f${instance}`;
    if (seen.has(key)) continue;
    seen.add(key);
    targets.push({ operatorId, instance });
  }
  const ids = [
    ...new Set([
      ...asSpecialArray(value?.operatorIds).map((id) => boundedText(id, 100)).filter((id) => valid.has(id)),
      ...targets.map((item) => item.operatorId),
    ]),
  ];
  return { operatorIds: ids, operatorTargets: targets };
}

function normalizeOperatorMultiSelect(master, value) {
  return normalizeOperatorTargets(master, asSpecialObject(value));
}

function normalizeOperatorEffectAssignment(master, campaignId, field, value) {
  const raw = asSpecialObject(value);
  const validEffects = validEffectIds(master, campaignId, field);
  const targets = normalizeOperatorTargets(master, raw);
  return {
    effectId: validEffects.has(raw.effectId) ? raw.effectId : null,
    ...targets,
  };
}

function normalizeTextMultiSelect(field, value) {
  const configured = new Set(
    (field?.options || [])
      .map((item) => boundedText(typeof item === "string" ? item : item?.id || item?.value || item?.label))
      .filter(Boolean),
  );
  return [...new Set(asSpecialArray(value).map((item) => boundedText(item)).filter((item) => item && (!configured.size || configured.has(item))))];
}

function normalizeSpecialValue(master, campaignId, field, value) {
  switch (field.type) {
    case "number":
      return clampSpecialNumber(value, field.min, field.max);
    case "effectSelect":
      return normalizeEffectSelect(master, campaignId, field, value);
    case "effectMultiSelect":
      return normalizeEffectMultiSelect(master, campaignId, field, value);
    case "effectRankedMultiSelect":
      return normalizeRankedMultiSelect(master, campaignId, field, value);
    case "effectStackLoadout":
      return normalizeEffectStackEntries(field, campaignId, value, master?.selectableEffects || []);
    case "coinLoadout":
      return normalizeCoinLoadoutEntries(field, campaignId, value, master?.selectableEffects || []);
    case "revelationBoardLoadout":
      return normalizeRevelationBoardValue(field, campaignId, value, master?.selectableEffects || []);
    case "operatorMultiSelect":
      return normalizeOperatorMultiSelect(master, value);
    case "operatorEffectAssignment":
      return normalizeOperatorEffectAssignment(master, campaignId, field, value);
    case "textMultiSelect":
      return normalizeTextMultiSelect(field, value);
    case "boolean":
    case "overlayToggle":
      return Boolean(value);
    default:
      throw operationError(`遠隔入力に未対応の特殊値形式です: ${field.type || "(未指定)"}`);
  }
}

function normalizeRunValue(state, master, field, value) {
  const campaignId = state.run.campaignId;
  if (field === "ingot") return Math.max(0, Math.min(9999, Math.round(Number(value) || 0)));
  if (field === "difficulty") {
    if (value === null || value === "") return null;
    return Math.max(0, Math.min(99, Math.round(Number(value) || 0)));
  }
  if (field === "squadId") {
    const id = boundedText(value, 120);
    if (!id) return null;
    const valid = (master?.squads || []).some((item) => item.id === id && (!item.campaignId || item.campaignId === campaignId));
    if (!valid) throw operationError(`存在しない分隊です: ${id}`);
    return id;
  }
  if (field === "difficultyTierId") {
    const id = boundedText(value, 120);
    if (!id) return null;
    const entries = difficultyTierEntries(master, campaignId);
    if (!entries.some((item) => item.id === id)) throw operationError(`存在しない等級Tierです: ${id}`);
    return id;
  }
  if (field === "performanceId") {
    const id = boundedText(value, 120);
    if (!id) return null;
    if (!(master?.performances || []).some((item) => item.id === id && (!item.campaignId || item.campaignId === campaignId))) {
      throw operationError(`存在しない演目です: ${id}`);
    }
    return id;
  }
  if (field === "squadRandomEffectOptionId") {
    const id = boundedText(value, 120);
    if (!id) return null;
    const squadId = state.run.squadId || state.run.squad;
    const squad = (master?.squads || []).find((item) =>
      item.id === squadId && (!item.campaignId || item.campaignId === campaignId));
    const valid = (squad?.randomEffectOptions || []).some((item) => item.id === id);
    if (!valid) throw operationError(`現在の分隊に存在しない追加効果です: ${id}`);
    return id;
  }
  throw operationError(`許可されていないラン項目です: ${field}`);
}

function operationSummary(operation, state, master) {
  if (operation.type === "campaign.set") return `統合戦略を${currentCampaign(state, master).shortTitle || currentCampaign(state, master).title}に変更`;
  if (operation.type === "run.set") return `${operation.field}を更新`;
  if (operation.type === "special.set") return `特殊値 ${operation.field} を更新`;
  if (operation.type === "operator.set") return `オペレーター ${operation.operatorId} を更新`;
  if (operation.type === "relic.set") return `秘宝 ${operation.relicId} を更新`;
  if (operation.type === "boss.set") return `ボス ${operation.field} を更新`;
  if (operation.type === "run.clear") return "ラン状態をクリア";
  return operation.type;
}

function clearEditableRunState(state) {
  const campaignId = state.run?.campaignId || "is2_phantom";
  const next = structuredClone(state);
  next.run = {
    ...next.run,
    campaignId,
    ingot: 0,
    difficulty: null,
    squad: null,
    squadId: null,
    difficultyTierId: null,
    performanceId: null,
    squadRandomEffectOptionId: null,
    special: { ...(next.run?.special || {}), [campaignId]: {} },
  };
  next.operators = [];
  next.operatorCounts = {};
  next.operatorPromotionLevels = {};
  next.relics = [];
  next.usedRelicIds = [];
  next.bossFlags = [];
  next.bossSelections = {};
  return next;
}

export function applyTournamentRemoteOperation(state, master, operation) {
  if (!state || typeof state !== "object") throw operationError("現在stateがありません。");
  if (!operation || typeof operation !== "object" || Array.isArray(operation)) throw operationError("操作形式が不正です。");
  if (operation.type === "batch") {
    const operations = Array.isArray(operation.operations) ? operation.operations : [];
    if (!operations.length || operations.length > 200) {
      throw operationError("一括操作は1件以上200件以下で指定してください。");
    }
    let workingState = state;
    for (const childOperation of operations) {
      if (childOperation?.type === "batch") throw operationError("一括操作の入れ子は許可されていません。");
      workingState = applyTournamentRemoteOperation(workingState, master, childOperation).state;
    }
    return {
      state: workingState,
      summary: `${operations.length}件の入力を一括反映`,
    };
  }
  const next = structuredClone(state);
  next.run = next.run && typeof next.run === "object" ? next.run : {};

  switch (operation.type) {
    case "campaign.set": {
      const campaign = campaignById(master, boundedText(operation.campaignId, 100));
      next.run.campaignId = campaign.id;
      next.run.special = next.run.special && typeof next.run.special === "object" ? next.run.special : {};
      next.run.special[campaign.id] ||= {};
      break;
    }
    case "run.set": {
      if (!RUN_FIELDS.has(operation.field)) throw operationError(`許可されていないラン項目です: ${operation.field}`);
      const value = normalizeRunValue(next, master, operation.field, operation.value);
      next.run[operation.field] = value;
      if (operation.field === "difficulty") applyDifficultyTier(master, next.run);
      if (operation.field === "squadId") {
        next.run.squad = null;
        next.run.squadRandomEffectOptionId = null;
      }
      break;
    }
    case "special.set": {
      const campaign = currentCampaign(next, master);
      const field = specialField(campaign, boundedText(operation.field, 100));
      next.run.special = next.run.special && typeof next.run.special === "object" ? next.run.special : {};
      next.run.special[campaign.id] ||= {};
      next.run.special[campaign.id][field.id] = normalizeSpecialValue(master, campaign.id, field, operation.value);
      break;
    }
    case "operator.set": {
      const id = boundedText(operation.operatorId, 100);
      if (!operatorIdSet(master).has(id)) throw operationError(`存在しないオペレーターです: ${id}`);
      const selected = Boolean(operation.selected);
      const set = new Set(Array.isArray(next.operators) ? next.operators : []);
      if (selected) set.add(id);
      else set.delete(id);
      next.operators = [...set];
      const counts = { ...(next.operatorCounts || {}) };
      if (selected) counts[id] = Math.max(1, Math.min(99, Math.trunc(Number(operation.count) || 1)));
      else delete counts[id];
      next.operatorCounts = normalizeOperatorCounts(counts, next.operators);
      const promotions = normalizeOperatorPromotionLevels(next.operatorPromotionLevels, next.operators);
      if (!selected || Number(operation.promotionLevel) < 2) {
        if (!selected || operation.promotionLevel !== undefined) delete promotions[id];
      } else if (supportsEliteTwo(master, id)) {
        promotions[id] = 2;
      }
      next.operatorPromotionLevels = promotions;
      break;
    }
    case "relic.set": {
      const id = boundedText(operation.relicId, 120);
      if (!(master?.relics || []).some((item) => item.id === id)) throw operationError(`存在しない秘宝です: ${id}`);
      const selected = Boolean(operation.selected);
      const relics = new Set(Array.isArray(next.relics) ? next.relics : []);
      const used = new Set(Array.isArray(next.usedRelicIds) ? next.usedRelicIds : []);
      if (selected) relics.add(id);
      else relics.delete(id);
      if (selected && operation.used) used.add(id);
      else used.delete(id);
      next.relics = [...relics];
      next.usedRelicIds = [...used].filter((item) => relics.has(item));
      break;
    }
    case "boss.set": {
      const campaign = currentCampaign(next, master);
      const fieldId = boundedText(operation.field, 100);
      const section = getBossManualSections(campaign.bossFlags, next.relics).find((item) => item.field === fieldId);
      if (!section) throw operationError(`許可されていないボス選択です: ${fieldId}`);
      next.bossSelections = next.bossSelections && typeof next.bossSelections === "object" ? next.bossSelections : {};
      next.bossSelections[campaign.id] ||= {};
      next.bossSelections[campaign.id][fieldId] = operation.value;
      normalizeBossSelections(master.campaigns || [], next.bossSelections);
      break;
    }
    case "run.clear":
      return {
        state: clearEditableRunState(next),
        summary: "ラン状態をクリア",
      };
    default:
      throw operationError(`許可されていない遠隔操作です: ${operation.type || "(未指定)"}`);
  }

  return {
    state: next,
    summary: operationSummary(operation, next, master),
  };
}

export function buildTournamentRemoteSnapshot(state, master) {
  const run = state?.run && typeof state.run === "object" ? state.run : {};
  return {
    revision: Number(state?.version || 1),
    updatedAt: state?.updatedAt || new Date().toISOString(),
    state: {
      run: structuredClone(run),
      operators: [...(state?.operators || [])],
      operatorCounts: structuredClone(state?.operatorCounts || {}),
      operatorPromotionLevels: structuredClone(state?.operatorPromotionLevels || {}),
      relics: [...(state?.relics || [])],
      usedRelicIds: [...(state?.usedRelicIds || [])],
      bossFlags: [...(state?.bossFlags || [])],
      bossSelections: structuredClone(state?.bossSelections || {}),
    },
    master: {
      campaigns: structuredClone(master?.campaigns || []),
      squads: structuredClone(master?.squads || []),
      relics: structuredClone(master?.relics || []),
      operators: structuredClone(master?.operators || []),
      performances: structuredClone(master?.performances || []),
      selectableEffects: structuredClone(master?.selectableEffects || []),
      difficultyTiers: structuredClone(master?.difficultyTiers || {}),
    },
  };
}
