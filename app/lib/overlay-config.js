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

const overlayLayouts = new Set(["compact", "vertical", "horizontal", "full", "custom"]);
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

export function clampOverlayBackgroundOpacity(value, fallback = 100) {
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) return fallback;
  return Math.min(100, Math.max(0, Math.round(numeric)));
}

// Kept for stored legacy preferences and older callers.
export const clampOverlayBackgroundTransparency = clampOverlayBackgroundOpacity;

export function findOverlayPartPreference(preferences = {}, partId = null) {
  if (!partId || !Array.isArray(preferences.sukiOutputParts)) return null;
  return preferences.sukiOutputParts.find((part) => part?.id === partId) || null;
}

export function resolveOverlayBackgroundEnabled(preferences = {}, partId = null) {
  const part = findOverlayPartPreference(preferences, partId);
  if (part && typeof part.backgroundEnabled === "boolean") return part.backgroundEnabled;
  return preferences.sukiOutputBackgroundEnabled === true;
}

export function resolveOverlayBackgroundAlpha(preferences = {}, partId = null) {
  if (!resolveOverlayBackgroundEnabled(preferences, partId)) return 0;
  const part = findOverlayPartPreference(preferences, partId);
  const opacity = clampOverlayBackgroundOpacity(
    part?.backgroundOpacity ?? preferences.sukiOutputBackgroundOpacity,
    100,
  );
  return Math.round((opacity / 100) * 100) / 100;
}

export function shouldShowOverlayPartTitles(preferences = {}, partId = null) {
  const part = findOverlayPartPreference(preferences, partId);
  if (part && typeof part.showTitle === "boolean") return part.showTitle;
  return preferences.sukiOutputShowPartTitles !== false;
}

export function isTournamentOverlay(preferences = {}, partId = null) {
  const part = findOverlayPartPreference(preferences, partId);
  if (part && typeof part.tournamentMode === "boolean") return part.tournamentMode;
  return preferences.sukiOutputTournamentMode === true;
}

export function isOverlayScrollSpeedField(field) {
  return Object.prototype.hasOwnProperty.call(overlayScrollSpeedDefaults, field);
}
