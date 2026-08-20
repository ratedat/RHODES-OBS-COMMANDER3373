export const TOURNAMENT_OPERATOR_RARITIES = Object.freeze([6, 5, 4, 3, 2, 1]);

const MAX_SCORE = 999_999;
const MAX_WITHDRAWALS = 9_999;
const MAX_MEMO_LENGTH = 160;

function normalizeOptionalInteger(value, minimum, maximum) {
  if (value === "" || value === null || value === undefined) return null;
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) return null;
  return Math.min(maximum, Math.max(minimum, Math.trunc(numeric)));
}

export function normalizeTournamentInfo(value) {
  const source = value && typeof value === "object" && !Array.isArray(value) ? value : {};
  return {
    score: normalizeOptionalInteger(source.score, -MAX_SCORE, MAX_SCORE),
    withdrawals: normalizeOptionalInteger(source.withdrawals, 0, MAX_WITHDRAWALS),
    memo: String(source.memo ?? "").trim().slice(0, MAX_MEMO_LENGTH),
  };
}

export function normalizeTournamentOperatorRarities(value) {
  if (!Array.isArray(value)) return [...TOURNAMENT_OPERATOR_RARITIES];
  const selected = new Set(value.map(Number).filter((rarity) => TOURNAMENT_OPERATOR_RARITIES.includes(rarity)));
  return TOURNAMENT_OPERATOR_RARITIES.filter((rarity) => selected.has(rarity));
}

export function filterTournamentOperators(operators, rarities) {
  const visible = new Set(normalizeTournamentOperatorRarities(rarities));
  return (Array.isArray(operators) ? operators : []).filter((item) => visible.has(Number(item?.rarity)));
}

export function normalizeTournamentPresentation(value) {
  const source = value && typeof value === "object" && !Array.isArray(value) ? value : {};
  return {
    relicIconOnly: source.relicIconOnly === true,
    operatorIconOnly: source.operatorIconOnly === true,
    operatorRarities: normalizeTournamentOperatorRarities(source.operatorRarities),
  };
}

export function tournamentPresentationClassNames(value) {
  const presentation = normalizeTournamentPresentation(value);
  return [
    presentation.relicIconOnly ? "overlay-relic-icon-only" : "",
    presentation.operatorIconOnly ? "overlay-operator-icon-only" : "",
  ].filter(Boolean).join(" ");
}

export function prepareTournamentOverlayArgs(args = {}) {
  const presentation = normalizeTournamentPresentation(args.presentation);
  const allOperators = Array.isArray(args.operators) ? args.operators : [];
  return {
    ...args,
    presentation,
    allOperators,
    operators: filterTournamentOperators(allOperators, presentation.operatorRarities),
  };
}
