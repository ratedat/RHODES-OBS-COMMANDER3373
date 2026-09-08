const SUPPORTED_SCHEMA_VERSION = 1;
const SUPPORTED_NORMALIZATION = "nfkc-compact-quotes-lower";

export function normalizeNameCorrectionText(value) {
  return String(value ?? "")
    .normalize("NFKC")
    .replace(/\s+/gu, "")
    .replace(/[「」『』【】\[\]()]/gu, "")
    .toLowerCase();
}

function isObject(value) {
  return value != null && typeof value === "object" && !Array.isArray(value);
}

function belongsToScope(item, kind, campaignId) {
  if (kind === "operator") return true;
  return !campaignId || item?.campaignId === campaignId;
}

function ruleBelongsToScope(rule, kind, campaignId) {
  if (rule?.kind !== kind) return false;
  if (kind === "operator") return !rule.campaignId;
  return Boolean(campaignId) && rule.campaignId === campaignId;
}

function uniqueFormalTargets(catalog, kind, campaignId) {
  const buckets = new Map();
  for (const item of catalog) {
    if (!item?.id || !item?.name || !belongsToScope(item, kind, campaignId)) continue;
    const normalized = normalizeNameCorrectionText(item.name);
    if (!normalized) continue;
    const bucket = buckets.get(normalized) || [];
    bucket.push(item);
    buckets.set(normalized, bucket);
  }
  return new Map(
    [...buckets]
      .filter(([, items]) => new Set(items.map((item) => item.id)).size === 1)
      .map(([normalized, items]) => [normalized, items[0]]),
  );
}

export function createNameCorrectionResolver({
  nameCorrections = {},
  kind,
  campaignId = "",
  catalog = [],
} = {}) {
  const items = Array.isArray(catalog) ? catalog : [];
  const scopedItems = items.filter((item) => item?.id && belongsToScope(item, kind, campaignId));
  const idBuckets = new Map();
  for (const item of scopedItems) {
    const bucket = idBuckets.get(item.id) || [];
    bucket.push(item);
    idBuckets.set(item.id, bucket);
  }
  const byId = new Map(
    [...idBuckets].filter(([, matches]) => matches.length === 1).map(([id, matches]) => [id, matches[0]]),
  );
  const formalTargets = uniqueFormalTargets(items, kind, campaignId);
  const formalNames = new Set(
    scopedItems.map((item) => normalizeNameCorrectionText(item.name)).filter(Boolean),
  );
  const aliasBuckets = new Map();
  const supported = isObject(nameCorrections)
    && nameCorrections.schemaVersion === SUPPORTED_SCHEMA_VERSION
    && nameCorrections.normalization === SUPPORTED_NORMALIZATION
    && Array.isArray(nameCorrections.rules);

  for (const rule of supported ? nameCorrections.rules : []) {
    if (!ruleBelongsToScope(rule, kind, campaignId)) continue;
    const target = byId.get(rule.targetId);
    if (!target || target.name !== rule.canonicalName || !Array.isArray(rule.aliases)) continue;
    const canonical = normalizeNameCorrectionText(rule.canonicalName);
    for (const alias of rule.aliases) {
      const normalized = normalizeNameCorrectionText(alias);
      if (!normalized || normalized === canonical || formalNames.has(normalized)) continue;
      const bucket = aliasBuckets.get(normalized) || [];
      bucket.push({ targetId: target.id, target, ruleId: rule.id || null });
      aliasBuckets.set(normalized, bucket);
    }
  }

  const aliases = new Map();
  for (const [normalized, matches] of aliasBuckets) {
    const targetIds = new Set(matches.map((match) => match.targetId));
    if (targetIds.size !== 1) continue;
    aliases.set(normalized, matches[0]);
  }

  return {
    resolve(value) {
      const normalizedText = normalizeNameCorrectionText(value);
      if (!normalizedText) return null;
      const formal = formalTargets.get(normalizedText);
      if (formal) {
        return {
          targetId: formal.id,
          target: formal,
          matchType: "formal",
          ruleId: null,
          normalizedText,
        };
      }
      const alias = aliases.get(normalizedText);
      if (!alias) return null;
      return {
        ...alias,
        matchType: "alias",
        normalizedText,
      };
    },
    containsAliasFragment(value) {
      const normalizedText = normalizeNameCorrectionText(value);
      if (!normalizedText || aliases.has(normalizedText)) return false;
      return [...aliases.keys()].some((alias) => normalizedText.includes(alias));
    },
  };
}
