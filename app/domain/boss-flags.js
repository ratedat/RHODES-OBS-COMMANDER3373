export function bossImages(entry) {
  if (Array.isArray(entry?.images)) return entry.images.filter(Boolean);
  return entry?.image ? [entry.image] : [];
}

export function bossSectionAllowsMultiple(section) {
  return section?.mode === "multi" || section?.multiple === true;
}

function bossSectionIsVisible(section, relicIds) {
  if (relicIds === null || relicIds === undefined) return true;
  const owned = new Set(relicIds || []);
  if (section?.visibleWhenRelicId && !owned.has(section.visibleWhenRelicId)) return false;
  const required = Array.isArray(section?.visibleWhenAllRelicIds) ? section.visibleWhenAllRelicIds.filter(Boolean) : [];
  return required.every((id) => owned.has(id));
}

export function getBossManualSections(config, relicIds = null) {
  if (!config) return [];
  const sections = Array.isArray(config.manualSections) ? [...config.manualSections] : [];
  if (config.floor3 && !sections.some((section) => section.field === config.floor3.field)) sections.unshift(config.floor3);
  return sections.filter((section) => section?.field && bossSectionIsVisible(section, relicIds));
}

export function bossSelectionValues(section, selection = {}) {
  const current = selection?.[section.field];
  if (bossSectionAllowsMultiple(section)) return Array.isArray(current) ? current.filter(Boolean) : (current ? [current] : []);
  return current ? [current] : [];
}

export function normalizeBossSelections(campaigns, bossSelections = {}) {
  const normalized = bossSelections || {};
  for (const campaign of campaigns || []) {
    normalized[campaign.id] ||= {};
    for (const section of getBossManualSections(campaign.bossFlags)) {
      const validIds = new Set((section.options || []).map((item) => item.id));
      if (bossSectionAllowsMultiple(section)) {
        const current = normalized[campaign.id][section.field];
        const values = Array.isArray(current) ? current : (current ? [current] : []);
        normalized[campaign.id][section.field] = values.filter((id) => validIds.has(id));
      } else {
        const current = normalized[campaign.id][section.field] || null;
        const id = Array.isArray(current) ? current[0] : current;
        normalized[campaign.id][section.field] = validIds.has(id) ? id : null;
      }
    }
  }
  return normalized;
}

export function getSelectedManualBosses(sections, selection = {}) {
  return (sections || [])
    .flatMap((section) => bossSelectionValues(section, selection).map((id) => {
      const item = (section.options || []).find((option) => option.id === id);
      return item ? { ...item, type: "manualBoss", label: item.label || section.label || "手動", source: "manual", sectionId: section.id || section.field } : null;
    }))
    .filter(Boolean);
}

export function getSelectedFloor3Boss(manualBosses) {
  return (manualBosses || []).find((item) => Number(item.floor) === 3 || item.sectionId === "floor3BossId") || null;
}

function bossRouteTriggerIds(route) {
  if (Array.isArray(route?.triggerRelicIds)) return route.triggerRelicIds.filter(Boolean);
  return route?.triggerRelicId ? [route.triggerRelicId] : [];
}

function bossRouteTriggerRelics(route, owned = null, relicMap = new Map()) {
  return bossRouteTriggerIds(route)
    .filter((id) => !owned || owned.has(id))
    .map((id) => relicMap.get(id))
    .filter(Boolean);
}

function bossRouteDisabledIds(route) {
  if (Array.isArray(route?.disabledByRelicIds)) return route.disabledByRelicIds.filter(Boolean);
  return route?.disabledByRelicId ? [route.disabledByRelicId] : [];
}

function isBossRouteActive(route, owned) {
  if (bossRouteDisabledIds(route).some((id) => owned.has(id))) return false;
  const ids = bossRouteTriggerIds(route);
  if (!ids.length) return false;
  if (route.triggerMode === "all") return ids.every((id) => owned.has(id));
  return ids.some((id) => owned.has(id));
}

function selectedBossReplacementVariant(rule, entry) {
  const variants = Array.isArray(rule?.variants) ? rule.variants : [];
  return variants.find((variant) => {
    const selectedIds = Array.isArray(variant?.selectedIds) ? variant.selectedIds : [];
    return !selectedIds.length || selectedIds.includes(entry.id);
  }) || rule;
}

function applyManualBossReplacements(entries, cfg, owned, relicMap) {
  const rules = Array.isArray(cfg?.manualReplacementRoutes) ? cfg.manualReplacementRoutes : [];
  if (!rules.length) return entries;
  return entries.map((entry) => {
    const rule = rules.find((candidate) => {
      if (!isBossRouteActive(candidate, owned)) return false;
      const sectionIds = Array.isArray(candidate.sectionIds) ? candidate.sectionIds : (candidate.sectionId ? [candidate.sectionId] : []);
      const selectedIds = Array.isArray(candidate.selectedIds) ? candidate.selectedIds : [];
      if (sectionIds.length && !sectionIds.includes(entry.sectionId)) return false;
      return !selectedIds.length || selectedIds.includes(entry.id);
    });
    if (!rule) return entry;
    const variant = selectedBossReplacementVariant(rule, entry);
    const triggerRelics = bossRouteTriggerRelics(rule, owned, relicMap);
    return {
      ...entry,
      ...variant,
      id: variant.id || (entry.id + "__" + rule.id),
      source: "relic",
      type: "manualBossReplacement",
      originalBoss: entry,
      triggerRelics,
      triggerRelic: triggerRelics[0] || null,
      requiredNote: variant.requiredNote || rule.requiredNote || entry.requiredNote || "",
    };
  });
}

function uniqueById(items) {
  const seen = new Set();
  return items.filter((item) => {
    const key = item?.id || item?.name;
    if (!key || seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

function combineBossRouteGroups(routes) {
  const grouped = new Map();
  const output = [];
  for (const route of routes) {
    const groupId = route.combineGroupId || route.combineGroup;
    if (!groupId) {
      output.push(route);
      continue;
    }
    if (!grouped.has(groupId)) {
      const bucket = [];
      grouped.set(groupId, bucket);
      output.push(bucket);
    }
    grouped.get(groupId).push(route);
  }
  return output.flatMap((item) => {
    if (!Array.isArray(item)) return [item];
    if (item.length <= 1) return item;
    const [first] = item;
    const names = item
      .map((route) => route.bossName || route.title || route.stageName)
      .filter(Boolean);
    const notes = [...new Set(item.map((route) => route.requiredNote || route.note).filter(Boolean))];
    const triggerRelics = uniqueById(item.flatMap((route) => route.triggerRelics || (route.triggerRelic ? [route.triggerRelic] : [])));
    return [{
      ...first,
      id: item.map((route) => route.id).join("__"),
      bossName: names.join("+"),
      title: names.join("+"),
      requiredNote: notes.join(" / "),
      triggerRelics,
      triggerRelic: triggerRelics[0] || null,
      combinedRoutes: item,
    }];
  });
}

function bossSortValue(entry) {
  const sortOrder = Number(entry.sortOrder);
  if (Number.isFinite(sortOrder)) return sortOrder;
  const floor = Number(entry.floor);
  if (Number.isFinite(floor)) return floor;
  return 99;
}

export function bossFloorLabel(entry) {
  if (entry.floorLabel) return entry.floorLabel;
  if (entry.floor) return `${entry.floor}層`;
  return entry.label || "手動";
}

export function buildBossFlagEntries({ config, relicIds = [], manualBosses = [], manualFlags = [], relicMap = new Map() } = {}) {
  if (!config) return manualFlags.map((flag, index) => ({ id: `manual_${index}`, type: "manual", label: "手動", title: flag, stageName: "" }));
  const owned = new Set(relicIds || []);
  const entries = applyManualBossReplacements([...manualBosses], config, owned, relicMap);
  const activeRoutes = combineBossRouteGroups((config.relicRoutes || [])
    .filter((route) => isBossRouteActive(route, owned))
    .map((route) => {
      const triggerRelics = bossRouteTriggerRelics(route, owned, relicMap);
      return { ...route, type: "relicBoss", source: "relic", triggerRelics, triggerRelic: triggerRelics[0] || null };
    }));
  const activeRouteIds = new Set(activeRoutes.map((route) => route.id));
  for (let index = entries.length - 1; index >= 0; index -= 1) {
    if (activeRouteIds.has(entries[index]?.id)) entries.splice(index, 1);
  }
  for (const boss of config.defaultBosses || []) {
    const replaced = activeRoutes.some((route) => Number(route.replacesDefaultFloor) === Number(boss.floor));
    if (!replaced) entries.push({ ...boss, type: "defaultBoss", source: "default" });
  }
  entries.push(...activeRoutes);
  for (const [index, flag] of (manualFlags || []).entries()) {
    entries.push({ id: `manual_${index}`, type: "manual", label: "手動メモ", title: flag, stageName: "" });
  }
  return entries.sort((a, b) => (bossSortValue(a) - bossSortValue(b)) || String(a.id).localeCompare(String(b.id), "ja"));
}
