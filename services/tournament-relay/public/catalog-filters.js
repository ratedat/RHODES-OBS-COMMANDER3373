const OPERATOR_CLASS_ORDER = ["先鋒", "前衛", "重装", "狙撃", "術師", "医療", "補助", "特殊"];
const OPERATOR_BRANCH_ORDER_BY_CLASS = [
  ["先鋒", ["先駆兵", "突撃兵", "戦術家", "旗手", "偵察兵", "策士"]],
  ["前衛", ["強襲者", "闘士", "術戦士", "教官", "領主", "剣豪", "武者", "勇士", "鎌撃士", "解放者", "重剣士", "槌撃士", "本源戦士", "傭兵"]],
  ["重装", ["重盾衛士", "庇護衛士", "破壊者", "術技衛士", "決闘者", "堅城砲手", "哨戒衛士", "本源衛士"]],
  ["狙撃", ["速射手", "精密射手", "榴弾射手", "戦術射手", "散弾射手", "破城射手", "投擲手", "狩人", "旋輪射手", "翔空射手"]],
  ["術師", ["中堅術師", "拡散術師", "操機術師", "法陣術師", "秘術師", "連鎖術師", "爆撃術師", "本源術師", "創霊術師"]],
  ["医療", ["医師", "群癒師", "療養師", "放浪医", "呪癒師", "連鎖癒師", "守望者"]],
  ["補助", ["緩速師", "呪詛師", "吟遊者", "祈祷師", "召喚師", "工匠", "祭儀師"]],
  ["特殊", ["執行者", "推撃手", "潜伏者", "鉤縄師", "鬼才", "行商人", "罠師", "傀儡師", "錬金士", "巡空者"]],
];

const CLASS_ALIASES = new Map([["術士", "術師"]]);
const CLASS_RANKS = new Map(OPERATOR_CLASS_ORDER.map((value, index) => [value, index]));
const BRANCH_RANKS = new Map(
  OPERATOR_BRANCH_ORDER_BY_CLASS.flatMap(([className, branches]) =>
    branches.map((branch, index) => [branch, {
      classRank: CLASS_RANKS.get(className),
      branchRank: index,
    }])),
);

function normalizeText(value) {
  return String(value || "")
    .replace(/[\s\u3000]+/g, "")
    .toLocaleLowerCase("ja");
}

function operatorClass(item) {
  const value = String(item?.class || item?.profession || "");
  return CLASS_ALIASES.get(value) || value;
}

function operatorBranch(item) {
  return String(item?.branch || item?.archetype || "");
}

function numberValue(value, fallback = Number.MAX_SAFE_INTEGER) {
  const numeric = Number(value);
  return Number.isFinite(numeric) ? numeric : fallback;
}

function compareText(left, right) {
  return String(left || "").localeCompare(String(right || ""), "ja");
}

function compareOperatorClass(left, right) {
  const leftRank = CLASS_RANKS.get(CLASS_ALIASES.get(left) || left) ?? Number.MAX_SAFE_INTEGER;
  const rightRank = CLASS_RANKS.get(CLASS_ALIASES.get(right) || right) ?? Number.MAX_SAFE_INTEGER;
  return (leftRank - rightRank) || compareText(left, right);
}

function compareOperatorBranch(left, right, branchClasses) {
  const leftFallback = CLASS_RANKS.get(branchClasses.get(left)) ?? Number.MAX_SAFE_INTEGER;
  const rightFallback = CLASS_RANKS.get(branchClasses.get(right)) ?? Number.MAX_SAFE_INTEGER;
  const leftRank = BRANCH_RANKS.get(left) || { classRank: leftFallback, branchRank: Number.MAX_SAFE_INTEGER };
  const rightRank = BRANCH_RANKS.get(right) || { classRank: rightFallback, branchRank: Number.MAX_SAFE_INTEGER };
  return (leftRank.classRank - rightRank.classRank)
    || (leftRank.branchRank - rightRank.branchRank)
    || compareText(left, right);
}

function uniqueOperatorClasses(items) {
  return [...new Set(items.map(operatorClass).filter(Boolean))].sort(compareOperatorClass);
}

function uniqueOperatorBranches(items) {
  const branchClasses = new Map();
  for (const item of items) {
    const branch = operatorBranch(item);
    if (branch && !branchClasses.has(branch)) branchClasses.set(branch, operatorClass(item));
  }
  return [...branchClasses.keys()].sort((left, right) => compareOperatorBranch(left, right, branchClasses));
}

function normalizedColumns(value) {
  return Math.max(1, Math.min(4, Math.round(numberValue(value, 2))));
}

function matchesSearch(item, searchText) {
  const query = normalizeText(searchText);
  if (!query) return true;
  return normalizeText([
    item?.name,
    item?.label,
    item?.class,
    item?.profession,
    item?.branch,
    item?.archetype,
    item?.category,
    item?.description,
    item?.effect,
    item?.number,
  ].filter(Boolean).join(" ")).includes(query);
}

function selectedFirst(items, selected, enabled) {
  if (!enabled) return items;
  return items
    .map((item, index) => ({ item, index, selected: selected.has(item.id) ? 0 : 1 }))
    .sort((left, right) => (left.selected - right.selected) || (left.index - right.index))
    .map(({ item }) => item);
}

function operatorSort(items, mode) {
  return [...items].sort((left, right) => {
    if (mode === "class") {
      const classComparison = compareOperatorClass(operatorClass(left), operatorClass(right));
      const branchClasses = new Map([
        [operatorBranch(left), operatorClass(left)],
        [operatorBranch(right), operatorClass(right)],
      ]);
      return classComparison
        || compareOperatorBranch(operatorBranch(left), operatorBranch(right), branchClasses)
        || (numberValue(right.rarity, 0) - numberValue(left.rarity, 0))
        || (numberValue(left.displayOrder) - numberValue(right.displayOrder))
        || compareText(left.name, right.name);
    }
    if (mode === "name") return compareText(left.name, right.name);
    return (numberValue(right.rarity, 0) - numberValue(left.rarity, 0))
      || (numberValue(left.displayOrder) - numberValue(right.displayOrder))
      || compareText(left.name, right.name);
  });
}

function relicSort(items, mode) {
  return [...items].sort((left, right) => {
    if (mode === "name") return compareText(left.name, right.name);
    if (mode === "number") {
      return (numberValue(left.number) - numberValue(right.number))
        || compareText(left.name, right.name);
    }
    return compareText(left.category, right.category)
      || (numberValue(left.number) - numberValue(right.number))
      || compareText(left.name, right.name);
  });
}

export function buildOperatorCatalogView(items = [], selectedIds = [], filters = {}) {
  const selected = new Set(selectedIds || []);
  const normalized = {
    search: String(filters.search || ""),
    className: String(filters.className || "all"),
    branch: String(filters.branch || "all"),
    rarity: String(filters.rarity || "all"),
    sort: ["rarity", "class", "name"].includes(filters.sort) ? filters.sort : "rarity",
    selectedFirst: Boolean(filters.selectedFirst),
    selectedOnly: Boolean(filters.selectedOnly),
    columns: normalizedColumns(filters.columns),
  };
  const available = (items || []).filter((item) => item?.id && !item.hiddenByDefault);
  const rarityOptions = [...new Set(available.map((item) => numberValue(item.rarity, 0)).filter(Boolean))]
    .sort((left, right) => right - left);
  if (normalized.rarity !== "all" && !rarityOptions.map(String).includes(normalized.rarity)) {
    normalized.rarity = "all";
  }

  const rarityBase = available.filter((item) =>
    normalized.rarity === "all" || String(item.rarity) === normalized.rarity);
  const classOptions = uniqueOperatorClasses(rarityBase);
  if (normalized.className !== "all" && !classOptions.includes(normalized.className)) {
    normalized.className = "all";
    normalized.branch = "all";
  }

  const classBase = rarityBase.filter((item) =>
    normalized.className === "all" || operatorClass(item) === normalized.className);
  const branchOptions = uniqueOperatorBranches(classBase);
  if (normalized.branch !== "all" && !branchOptions.includes(normalized.branch)) {
    normalized.branch = "all";
  }

  const filtered = classBase.filter((item) =>
    (normalized.branch === "all" || operatorBranch(item) === normalized.branch)
    && matchesSearch(item, normalized.search)
    && (!normalized.selectedOnly || selected.has(item.id)));
  const sorted = selectedFirst(operatorSort(filtered, normalized.sort), selected, normalized.selectedFirst);
  return {
    filters: normalized,
    items: sorted,
    options: { rarity: rarityOptions, classes: classOptions, branches: branchOptions },
    total: available.length,
    selectedCount: available.filter((item) => selected.has(item.id)).length,
  };
}

export function buildRelicCatalogView(items = [], selectedIds = [], campaignId = "", filters = {}) {
  const selected = new Set(selectedIds || []);
  const normalized = {
    search: String(filters.search || ""),
    category: String(filters.category || "all"),
    sort: ["category", "number", "name"].includes(filters.sort) ? filters.sort : "category",
    selectedFirst: Boolean(filters.selectedFirst),
    selectedOnly: Boolean(filters.selectedOnly),
    columns: normalizedColumns(filters.columns),
  };
  const available = (items || []).filter((item) =>
    item?.id
    && !item.hiddenByDefault
    && (!item.campaignId || item.campaignId === campaignId));
  const categoryOptions = [...new Set(available.map((item) => item.category).filter(Boolean))].sort(compareText);
  if (normalized.category !== "all" && !categoryOptions.includes(normalized.category)) {
    normalized.category = "all";
  }

  const filtered = available.filter((item) =>
    (normalized.category === "all" || item.category === normalized.category)
    && matchesSearch(item, normalized.search)
    && (!normalized.selectedOnly || selected.has(item.id)));
  const sorted = selectedFirst(relicSort(filtered, normalized.sort), selected, normalized.selectedFirst);
  return {
    filters: normalized,
    items: sorted,
    options: { categories: categoryOptions },
    total: available.length,
    selectedCount: available.filter((item) => selected.has(item.id)).length,
  };
}
