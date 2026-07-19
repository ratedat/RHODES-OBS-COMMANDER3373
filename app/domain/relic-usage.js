const USED_FLAG_RELIC_NAMES = new Set([
  "「時の果て」",
  "「門」と「救難」",
]);

export function supportsRelicUsedFlag(relic) {
  const name = typeof relic === "string" ? relic : relic?.name;
  return typeof name === "string" && USED_FLAG_RELIC_NAMES.has(name.trim());
}

export function prioritizeOwnedRelics(relics = [], usedRelicIds = []) {
  const used = new Set(Array.isArray(usedRelicIds) ? usedRelicIds : []);
  return relics
    .map((item, index) => ({
      item: {
        ...item,
        used: supportsRelicUsedFlag(item) && used.has(item.id),
      },
      index,
      priority: supportsRelicUsedFlag(item) ? 0 : 1,
    }))
    .sort((left, right) => left.priority - right.priority || left.index - right.index)
    .map(({ item }) => item);
}
