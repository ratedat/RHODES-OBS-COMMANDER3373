export const overlaySizeAliases = {
  s: "small",
  small: "small",
  m: "medium",
  medium: "medium",
  l: "large",
  large: "large",
};

export const overlayScrollSpeedDefaults = {
  compactRelicScrollSpeed: 9,
  verticalRelicScrollSpeed: 11,
  verticalOperatorScrollSpeed: 13,
  horizontalRelicScrollSpeed: 14,
  horizontalOperatorScrollSpeed: 16,
};

export const overlayScrollSpeedLabels = {
  compactRelicScrollSpeed: "コンパクト 秘宝",
  verticalRelicScrollSpeed: "縦長 秘宝",
  verticalOperatorScrollSpeed: "縦長 オペレーター",
  horizontalRelicScrollSpeed: "横長 秘宝",
  horizontalOperatorScrollSpeed: "横長 オペレーター",
};

const overlayLayouts = new Set(["compact", "vertical", "horizontal", "full"]);
const overlayParts = new Set(["status", "relics", "operators", "effects", "bosses", "special"]);

export function resolveOverlayLayout(value) {
  return overlayLayouts.has(value) ? value : "compact";
}

export function resolveOverlayPart(value) {
  return overlayParts.has(value) ? value : null;
}

export function resolveOverlaySize(value) {
  return overlaySizeAliases[value] || "medium";
}

export function clampOverlayScrollSpeed(value, fallback = 12) {
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) return fallback;
  return Math.min(30, Math.max(0, Math.round(numeric)));
}

export function isOverlayScrollSpeedField(field) {
  return Object.prototype.hasOwnProperty.call(overlayScrollSpeedDefaults, field);
}