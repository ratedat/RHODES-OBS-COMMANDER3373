function clone(value) {
  return structuredClone(value);
}

export function operationKey(operation = {}) {
  if (operation.type === "campaign.set") return "campaign";
  if (operation.type === "run.set") return `run:${operation.field || ""}`;
  if (operation.type === "special.set") return `special:${operation.field || ""}`;
  if (operation.type === "operator.set") return `operator:${operation.operatorId || ""}`;
  if (operation.type === "relic.set") return `relic:${operation.relicId || ""}`;
  if (operation.type === "boss.set") return `boss:${operation.field || ""}`;
  if (operation.type === "run.clear") return "run:clear";
  return JSON.stringify(operation);
}

export function upsertDraftOperation(operations, operation) {
  const nextOperation = clone(operation);
  if (operation.type === "run.clear") return [nextOperation];

  if (operation.type === "campaign.set") {
    const retained = (operations || []).filter((item) => {
      if (item.type === "campaign.set" || item.type === "special.set" || item.type === "boss.set") return false;
      if (item.type !== "run.set") return true;
      return !["squadId", "squadRandomEffectOptionId", "difficultyTierId", "performanceId"].includes(item.field);
    });
    return [nextOperation, ...retained];
  }

  const next = [...(operations || [])];
  const key = operationKey(operation);
  const index = next.findIndex((item) => operationKey(item) === key);
  if (index >= 0) next[index] = nextOperation;
  else next.push(nextOperation);
  return next;
}

function clearRunState(state) {
  const campaignId = state.run?.campaignId || "is2_phantom";
  const next = clone(state);
  next.run = {
    ...(next.run || {}),
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
  next.relics = [];
  next.usedRelicIds = [];
  next.bossFlags = [];
  next.bossSelections = {};
  return next;
}

export function difficultyTierEntries(master, campaignId) {
  const config = master?.difficultyTiers?.[campaignId];
  if (Array.isArray(config)) return config;
  return Array.isArray(config?.tiers) ? config.tiers : [];
}

function resolveDifficultyTierId(master, run) {
  const config = master?.difficultyTiers?.[run?.campaignId];
  const tiers = difficultyTierEntries(master, run?.campaignId);
  const value = run?.difficulty === null || run?.difficulty === undefined || run?.difficulty === ""
    ? null
    : Number(run.difficulty);
  if (!Number.isFinite(value)) return null;
  const tier = tiers.find((item) =>
    value >= Number(item.minDifficulty)
      && (item.maxDifficulty === null || item.maxDifficulty === undefined || value <= Number(item.maxDifficulty)));
  return tier?.id || (!Array.isArray(config) ? config?.defaultTierId : null) || null;
}

export function applyDraftOperation(state, operation, master) {
  if (operation.type === "run.clear") return clearRunState(state);

  const next = clone(state || {});
  next.run = next.run && typeof next.run === "object" ? next.run : {};

  switch (operation.type) {
    case "campaign.set": {
      next.run.campaignId = operation.campaignId;
      next.run.special = next.run.special && typeof next.run.special === "object" ? next.run.special : {};
      next.run.special[operation.campaignId] ||= {};
      break;
    }
    case "run.set": {
      let value = operation.value;
      if (operation.field === "ingot") value = Math.max(0, Math.min(9999, Math.round(Number(value) || 0)));
      else if (operation.field === "difficulty") {
        value = value === null || value === "" ? null : Math.max(0, Math.min(99, Math.round(Number(value) || 0)));
      } else {
        value = value === null || value === undefined || value === "" ? null : value;
      }
      next.run[operation.field] = value;
      if (operation.field === "difficulty") {
        next.run.difficultyTierId = resolveDifficultyTierId(master, next.run);
      }
      if (operation.field === "squadId") {
        next.run.squad = null;
        next.run.squadRandomEffectOptionId = null;
      }
      break;
    }
    case "special.set": {
      const campaignId = next.run.campaignId;
      next.run.special = next.run.special && typeof next.run.special === "object" ? next.run.special : {};
      next.run.special[campaignId] ||= {};
      next.run.special[campaignId][operation.field] = clone(operation.value);
      break;
    }
    case "operator.set": {
      const selected = new Set(next.operators || []);
      const counts = { ...(next.operatorCounts || {}) };
      if (operation.selected) {
        selected.add(operation.operatorId);
        counts[operation.operatorId] = Math.max(1, Math.min(99, Math.trunc(Number(operation.count) || 1)));
      } else {
        selected.delete(operation.operatorId);
        delete counts[operation.operatorId];
      }
      next.operators = [...selected];
      next.operatorCounts = counts;
      break;
    }
    case "relic.set": {
      const selected = new Set(next.relics || []);
      const used = new Set(next.usedRelicIds || []);
      if (operation.selected) selected.add(operation.relicId);
      else selected.delete(operation.relicId);
      if (operation.selected && operation.used) used.add(operation.relicId);
      else used.delete(operation.relicId);
      next.relics = [...selected];
      next.usedRelicIds = [...used].filter((id) => selected.has(id));
      break;
    }
    case "boss.set": {
      const campaignId = next.run.campaignId;
      next.bossSelections = next.bossSelections && typeof next.bossSelections === "object"
        ? next.bossSelections
        : {};
      next.bossSelections[campaignId] ||= {};
      next.bossSelections[campaignId][operation.field] = clone(operation.value);
      break;
    }
    default:
      break;
  }
  return next;
}

export function buildDraftState(state, operations, master) {
  if (!operations?.length) return null;
  return operations.reduce((current, operation) => applyDraftOperation(current, operation, master), clone(state || {}));
}
