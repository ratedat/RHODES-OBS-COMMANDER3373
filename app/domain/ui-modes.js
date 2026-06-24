export const controlModeOptions = [
  {
    id: "casual",
    label: "通常",
    shortLabel: "通常",
    subtitle: "個人配信 / 自分用",
    description: "OBSは軽量表示を優先し、入力と確認を同じ画面で扱います。",
    priorityTabs: ["run", "relics", "operators", "flags", "obs", "json"],
  },
  {
    id: "tournament",
    label: "大会",
    shortLabel: "大会",
    subtitle: "司会 / 大会配信",
    description: "第三者入力、ボスフラグ、進行確認を前に出して大会運用を見やすくします。",
    priorityTabs: ["run", "flags", "relics", "operators", "obs", "json"],
  },
  {
    id: "staff",
    label: "スタッフ",
    shortLabel: "Staff",
    subtitle: "入力 / 確認担当",
    description: "入力差分、JSON受け渡し、OBSパーツ確認を優先して編集作業に寄せます。",
    priorityTabs: ["flags", "run", "json", "obs", "relics", "operators"],
  },
];

const aliases = new Map([
  ["manual", "casual"],
  ["normal", "casual"],
  ["default", "casual"],
  ["event", "tournament"],
  ["contest", "tournament"],
  ["review", "staff"],
]);

export function normalizeControlMode(value) {
  const raw = String(value || "").trim();
  const id = aliases.get(raw) || raw || "casual";
  return controlModeOptions.some((option) => option.id === id) ? id : "casual";
}

export function getControlMode(value) {
  const id = normalizeControlMode(value);
  return controlModeOptions.find((option) => option.id === id) || controlModeOptions[0];
}

export function getModeOrderedTabs(value, tabIds) {
  const mode = getControlMode(value);
  const known = new Set(tabIds);
  const ordered = mode.priorityTabs.filter((tabId) => known.has(tabId));
  for (const tabId of tabIds) {
    if (!ordered.includes(tabId)) ordered.push(tabId);
  }
  return ordered;
}