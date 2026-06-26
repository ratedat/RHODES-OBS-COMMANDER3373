export function normalizeMaaEquivalenceClasses(configOrClasses = {}) {
  const rawClasses = Array.isArray(configOrClasses)
    ? configOrClasses
    : Array.isArray(configOrClasses.equivalence_classes)
      ? configOrClasses.equivalence_classes
      : [];
  return rawClasses
    .filter((group) => Array.isArray(group) && group.length >= 2)
    .map((group) => group.map((item) => String(item)).filter(Boolean))
    .filter((group) => group.length >= 2);
}

export function applyMaaEquivalenceClasses(value, configOrClasses = {}) {
  let text = String(value ?? "");
  for (const group of normalizeMaaEquivalenceClasses(configOrClasses)) {
    const replacement = group[0];
    for (const variant of group.slice(1)) {
      text = text.split(variant).join(replacement);
    }
  }
  return text;
}

export function createMaaOcrNormalizer(configOrClasses = {}) {
  const equivalenceClasses = normalizeMaaEquivalenceClasses(configOrClasses);
  return (value) => applyMaaEquivalenceClasses(value, equivalenceClasses);
}
