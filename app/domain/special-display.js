import * as selectableEffects from "./selectable-effects.js";
import { normalizeEffectStackEntry, normalizeRevelationBoardValue } from "./special-loadouts.js";
import { asCoinEntries, asEffectStackEntries, asSpecialArray, asSpecialObject, clampCoinCount } from "./special-values.js";

function getSelectableEffect(context, id) {
  return context.selectableEffectMap?.get(id) || null;
}

function splitEffectSentences(effect) {
  return String(effect || "")
    .split(/(?<=[。！？])/u)
    .map((sentence) => sentence.trim())
    .filter(Boolean);
}

function activeCoinEffect(effect) {
  const sentences = splitEffectSentences(effect);
  const active = sentences.filter((sentence) => /振り出され|振り銭/u.test(sentence));
  return (active.length ? active : sentences).join("");
}

function heldCoinEffect(effect) {
  const sentences = splitEffectSentences(effect);
  return sentences
    .filter((sentence) => /^銭匣内(?:にある場合|で)/u.test(sentence))
    .join("");
}

function coinOverlayPresentation(field, effect) {
  const scope = field.overlayEffectScope || (field.id === "activeCoins" ? "active" : field.id === "coins" ? "held" : "");
  if (scope === "active") {
    return {
      overlayGroupId: `${field.id}-active`,
      overlayGroupLabel: field.overlayGroupLabel || `${field.label || "有効銭"}（振出中）`,
      activationLabel: "発動中",
      effect: activeCoinEffect(effect),
    };
  }
  if (scope === "held") {
    const persistentEffect = heldCoinEffect(effect);
    return {
      overlayGroupId: `${field.id}-held`,
      overlayGroupLabel: field.overlayGroupLabel || `${field.label || "保有銭"}（銭匣内）`,
      activationLabel: persistentEffect ? "在匣" : "待機",
      effect: persistentEffect || `次回条件: ${effect || "効果情報なし"}`,
    };
  }
  return {
    overlayGroupId: field.id || "coins",
    overlayGroupLabel: field.overlayGroupLabel || field.label || "銭",
    activationLabel: "",
    effect: effect || "",
  };
}

export function getSpecialEffectName(id, selectableEffectMap) {
  const item = selectableEffectMap?.get(id);
  return item?.name || item?.title || id;
}

export function formatCoinLoadoutValue(field, value, context) {
  const entries = asCoinEntries(value).filter((entry) => getSelectableEffect(context, entry.coinId));
  if (!entries.length) return "";
  const total = entries.reduce((sum, entry) => sum + clampCoinCount(entry.count), 0);
  if (entries.length === 1) {
    const entry = entries[0];
    const coin = getSelectableEffect(context, entry.coinId);
    const status = entry.statusId ? getSelectableEffect(context, entry.statusId) : null;
    return [coin?.name, `x${entry.count}`, status?.name].filter(Boolean).join(" / ");
  }
  return `${total}枚 / ${entries.length}枠`;
}

export function formatEffectStackValue(field, value, context) {
  const entries = asEffectStackEntries(value)
    .map((entry) => normalizeEffectStackEntry(field, entry, context.campaignId, context.selectableEffectSource))
    .filter((entry) => getSelectableEffect(context, entry.effectId));
  if (!entries.length) return "";
  const total = entries.reduce((sum, entry) => sum + clampCoinCount(entry.count), 0);
  const unit = field.unitLabel || "件";
  if (entries.length === 1) {
    const entry = entries[0];
    const item = getSelectableEffect(context, entry.effectId);
    const stateLabel = selectableEffects.isEmptyStackState(field, entry.stateId, context.campaignId, context.selectableEffectSource)
      ? ""
      : selectableEffects.getStackStateLabel(field, entry.stateId, context.campaignId, context.selectableEffectSource);
    return [item?.name, `x${entry.count}`, stateLabel].filter(Boolean).join(" / ");
  }
  return `${total}${unit} / ${entries.length}枠`;
}

export function formatRevelationBoardValue(field, value, context) {
  const board = normalizeRevelationBoardValue(field, context.campaignId, value, context.selectableEffectSource);
  const cause = getSelectableEffect(context, board.causeId);
  const structure = getSelectableEffect(context, board.structureId);
  const rhetoricTotal = board.rhetorics.reduce((sum, entry) => sum + clampCoinCount(entry.count), 0);
  return [cause?.name, structure?.name, rhetoricTotal ? `修辞${rhetoricTotal}枚` : ""].filter(Boolean).join(" / ");
}

function countOperatorAssignments(value) {
  const source = asSpecialObject(value);
  const targetKeys = new Set(
    asSpecialArray(source.operatorTargets)
      .map((rawTarget) => {
        const target = asSpecialObject(rawTarget);
        const operatorId = String(target.operatorId ?? "").trim();
        const parsedInstance = Number(target.instance);
        const instance = Number.isFinite(parsedInstance) && parsedInstance > 0
          ? Math.floor(parsedInstance)
          : 1;
        return operatorId ? `${operatorId}#${instance}` : "";
      })
      .filter(Boolean),
  );
  if (targetKeys.size > 0) return targetKeys.size;

  const operatorIds = asSpecialArray(source.operatorIds).length > 0
    ? asSpecialArray(source.operatorIds)
    : asSpecialArray(value);
  return new Set(operatorIds.map((id) => String(id ?? "").trim()).filter(Boolean)).size;
}

function formatOperatorEffectAssignmentValue(value, context) {
  const source = asSpecialObject(value);
  const effect = source.effectId ? getSelectableEffect(context, source.effectId) : null;
  const effectLabel = effect?.name || effect?.title || (source.effectId ? "反応設定あり" : "");
  const targetCount = countOperatorAssignments(value);
  if (!effectLabel && targetCount === 0) return "";
  return [effectLabel, targetCount > 0 ? `対象${targetCount}名` : "対象なし"].filter(Boolean).join(" / ");
}

function formatOperatorMultiSelectValue(value) {
  const targetCount = countOperatorAssignments(value);
  return targetCount > 0 ? `対象${targetCount}名` : "";
}

export function formatSpecialValue(field, value, context) {
  if (field.type === "effectSelect") return value ? getSpecialEffectName(value, context.selectableEffectMap) : "";
  if (field.type === "effectMultiSelect") {
    const names = asSpecialArray(value).map((id) => getSpecialEffectName(id, context.selectableEffectMap)).filter(Boolean);
    if (names.length <= 1) return names[0] || "";
    return `${names.length}件`;
  }
  if (field.type === "effectRankedMultiSelect") {
    const names = Object.values(asSpecialObject(value)).map((id) => getSpecialEffectName(id, context.selectableEffectMap)).filter(Boolean);
    if (names.length <= 1) return names[0] || "";
    return `${names.length}件`;
  }
  if (field.type === "textMultiSelect") {
    const values = [...new Set(asSpecialArray(value).map((item) => String(item ?? "").trim()).filter(Boolean))];
    if (values.length <= 1) return values[0] || "";
    return `${values.length}件`;
  }
  if (field.type === "effectStackLoadout") return formatEffectStackValue(field, value, context);
  if (field.type === "revelationBoardLoadout") return formatRevelationBoardValue(field, value, context);
  if (field.type === "coinLoadout") return formatCoinLoadoutValue(field, value, context);
  if (field.type === "operatorEffectAssignment") return formatOperatorEffectAssignmentValue(value, context);
  if (field.type === "operatorMultiSelect") return formatOperatorMultiSelectValue(value);
  if (field.type === "number") return value === null || value === undefined || value === "" ? "" : String(value);
  return value ?? "";
}

export function getSpecialOverlayToggleKey(field) {
  return field.overlayToggleKey || `${field.id}OverlayVisible`;
}

export function isSpecialFieldVisibleOnOverlay(field, special) {
  if (!field.overlayToggle) return true;
  return Boolean(special[getSpecialOverlayToggleKey(field)]);
}

export function getSpecialTags(specialFields, special, context, options = {}) {
  return specialFields
    .filter((field) => !options.overlay || isSpecialFieldVisibleOnOverlay(field, special))
    .map((field) => ({ label: field.label, value: formatSpecialValue(field, special[field.id], context) }))
    .filter((item) => item.value !== null && item.value !== undefined && item.value !== "");
}

export function getSelectedSpecialEffectsForField(field, special, context) {
  const effects = [];
  if (field.type === "effectSelect") {
    const item = getSelectableEffect(context, special[field.id]);
    if (item) effects.push(item);
  } else if (field.type === "effectMultiSelect") {
    for (const id of asSpecialArray(special[field.id])) {
      const item = getSelectableEffect(context, id);
      if (item) effects.push(item);
    }
  } else if (field.type === "effectRankedMultiSelect") {
    for (const id of Object.values(asSpecialObject(special[field.id]))) {
      const item = getSelectableEffect(context, id);
      if (item) effects.push(item);
    }
  } else if (field.type === "textMultiSelect") {
    const values = [...new Set(asSpecialArray(special[field.id]).map((item) => String(item ?? "").trim()).filter(Boolean))];
    for (const [index, value] of values.entries()) {
      effects.push({
        id: `${context.campaignId}:${field.id}:${index}`,
        campaignId: context.campaignId,
        slot: field.id,
        slotLabel: field.label,
        groupLabel: "手動入力",
        category: field.label,
        name: value,
        effect: "",
      });
    }
  } else if (field.type === "effectStackLoadout") {
    for (const rawEntry of asEffectStackEntries(special[field.id])) {
      const entry = normalizeEffectStackEntry(field, rawEntry, context.campaignId, context.selectableEffectSource);
      const item = getSelectableEffect(context, entry.effectId);
      if (!item) continue;
      const hasState = !selectableEffects.isEmptyStackState(field, entry.stateId, context.campaignId, context.selectableEffectSource);
      const stateLabel = hasState ? selectableEffects.getStackStateLabel(field, entry.stateId, context.campaignId, context.selectableEffectSource) : "";
      const stateEffect = hasState ? selectableEffects.getStackStateEffect(field, entry.stateId, context.campaignId, context.selectableEffectSource) : "";
      const titleParts = [`x${entry.count}`, stateLabel].filter(Boolean);
      const effectParts = [item.effect, stateEffect ? `${field.stateLabel || "状態"} ${stateLabel}: ${stateEffect}` : ""].filter(Boolean);
      effects.push({
        ...item,
        slotLabel: field.label || item.slotLabel,
        name: `${item.name} ${titleParts.join(" / ")}`,
        effect: effectParts.join(" / "),
      });
    }
  } else if (field.type === "revelationBoardLoadout") {
    const board = normalizeRevelationBoardValue(field, context.campaignId, special[field.id], context.selectableEffectSource);
    const cause = getSelectableEffect(context, board.causeId);
    if (cause) effects.push({ ...cause, slotLabel: `${field.label || cause.slotLabel} 本因` });
    const structure = getSelectableEffect(context, board.structureId);
    if (structure) effects.push({ ...structure, slotLabel: `${field.label || structure.slotLabel} 構成` });
    for (const entry of board.rhetorics) {
      const item = getSelectableEffect(context, entry.effectId);
      if (!item) continue;
      effects.push({
        ...item,
        slotLabel: `${field.label || item.slotLabel} 修辞`,
        name: `${item.name} x${entry.count}`,
      });
    }
  } else if (field.type === "coinLoadout") {
    for (const entry of asCoinEntries(special[field.id])) {
      const coin = getSelectableEffect(context, entry.coinId);
      if (!coin) continue;
      const status = entry.statusId ? getSelectableEffect(context, entry.statusId) : null;
      const presentation = coinOverlayPresentation(field, coin.effect);
      const titleParts = [`x${entry.count}`, status?.name].filter(Boolean);
      const effectParts = [presentation.effect, status?.effect ? `${status.name}: ${status.effect}` : ""].filter(Boolean);
      effects.push({
        ...coin,
        slotLabel: field.label || coin.slotLabel,
        name: `${coin.name} ${titleParts.join(" / ")}`,
        quantity: clampCoinCount(entry.count),
        ...presentation,
        effect: effectParts.join(" / "),
      });
    }
  }
  return effects;
}

export function getSelectedSpecialEffects(specialFields, special, context, options = {}) {
  const effects = [];
  for (const field of specialFields || []) {
    if (options.overlay && !isSpecialFieldVisibleOnOverlay(field, special)) continue;
    effects.push(...getSelectedSpecialEffectsForField(field, special, context));
  }
  return effects;
}

export function getOverlaySpecialEffects(specialFields, special, context) {
  const effects = [];
  for (const field of specialFields || []) {
    if (!field.overlayToggle || !isSpecialFieldVisibleOnOverlay(field, special)) continue;
    effects.push(...getSelectedSpecialEffectsForField(field, special, context));
  }
  return effects;
}
