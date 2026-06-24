export function createLookupMaps(master, isRuleActive = () => true) {
  const maps = {
    campaign: new Map(master.campaigns.map((item) => [item.id, item])),
    squad: new Map(master.squads.map((item) => [item.id, item])),
    relic: new Map(master.relics.map((item) => [item.id, item])),
    operator: new Map(master.operators.map((item) => [item.id, item])),
    performance: new Map((master.performances || []).map((item) => [item.id, item])),
    selectableEffect: new Map((master.selectableEffects || []).map((item) => [item.id, item])),
    variantGroup: new Map((master.relicEffectVariants || []).map((item) => [item.relicId, item])),
    effectRuleByRelic: new Map(),
    effectRuleTags: master.relicEffectRules?.tagGroups || {},
  };

  for (const rule of master.relicEffectRules?.rules || []) {
    const relicId = rule?.relicId || rule?.source?.relicId;
    if (!relicId || !isRuleActive(rule)) continue;
    if (!maps.effectRuleByRelic.has(relicId)) maps.effectRuleByRelic.set(relicId, []);
    maps.effectRuleByRelic.get(relicId).push(rule);
  }

  return maps;
}