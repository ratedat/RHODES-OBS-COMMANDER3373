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

export const defaultOverlayAppearance = Object.freeze({
  fontColor: "#F2EFE6",
  backgroundColor: "#080B0C",
  borderColor: "#2B3638",
  accentColor: "#55D6BE",
  fontSizePercent: 100,
  customCss: "",
});

const cssHexColorPattern = /^#[0-9a-f]{6}(?:[0-9a-f]{2})?$/i;
// Overlay CSS may load remote fonts and images because it only affects the
// broadcast rendering surface. Script-like URL schemes remain invalid.
const blockedCustomCssPattern = /javascript\s*:/i;

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

export function normalizeOverlayAppearance(value = {}, fallback = defaultOverlayAppearance) {
  const source = value && typeof value === "object" && !Array.isArray(value) ? value : {};
  const base = fallback && typeof fallback === "object" ? fallback : defaultOverlayAppearance;
  const color = (field) => {
    const candidate = String(source[field] ?? base[field] ?? defaultOverlayAppearance[field]);
    return cssHexColorPattern.test(candidate) ? candidate.toUpperCase() : defaultOverlayAppearance[field];
  };
  const rawFontSize = Number(source.fontSizePercent ?? base.fontSizePercent);
  const fontSizePercent = Number.isFinite(rawFontSize)
    ? Math.min(200, Math.max(60, Math.round(rawFontSize)))
    : defaultOverlayAppearance.fontSizePercent;
  const customCss = String(source.customCss ?? base.customCss ?? "").slice(0, 65_536);

  return {
    fontColor: color("fontColor"),
    backgroundColor: color("backgroundColor"),
    borderColor: color("borderColor"),
    accentColor: color("accentColor"),
    fontSizePercent,
    customCss: blockedCustomCssPattern.test(customCss) ? "" : customCss,
  };
}

export function resolveOverlayAppearance(preferences = {}, partId = null) {
  const integrated = normalizeOverlayAppearance(preferences.sukiOutputIntegratedAppearance);
  if (!partId) return integrated;
  return normalizeOverlayAppearance(preferences.sukiOutputIndividualAppearance, integrated);
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
  if (partId && typeof preferences.sukiOutputIndividualBackgroundEnabled === "boolean") {
    return preferences.sukiOutputIndividualBackgroundEnabled;
  }
  return preferences.sukiOutputBackgroundEnabled === true;
}

export function resolveOverlayBackgroundAlpha(preferences = {}, partId = null) {
  if (!resolveOverlayBackgroundEnabled(preferences, partId)) return 0;
  const part = findOverlayPartPreference(preferences, partId);
  const opacity = clampOverlayBackgroundOpacity(
    part?.backgroundOpacity
      ?? (partId ? preferences.sukiOutputIndividualBackgroundOpacity : null)
      ?? preferences.sukiOutputBackgroundOpacity,
    100,
  );
  return Math.round((opacity / 100) * 100) / 100;
}

export function shouldShowOverlayPartTitles(preferences = {}, partId = null) {
  const part = findOverlayPartPreference(preferences, partId);
  if (part && typeof part.showTitle === "boolean") return part.showTitle;
  if (partId && typeof preferences.sukiOutputIndividualShowPartTitles === "boolean") {
    return preferences.sukiOutputIndividualShowPartTitles;
  }
  return preferences.sukiOutputShowPartTitles !== false;
}

export function isTournamentOverlay(preferences = {}, partId = null) {
  const part = findOverlayPartPreference(preferences, partId);
  if (part && typeof part.tournamentMode === "boolean") return part.tournamentMode;
  if (partId && typeof preferences.sukiOutputIndividualTournamentMode === "boolean") {
    return preferences.sukiOutputIndividualTournamentMode;
  }
  return preferences.sukiOutputTournamentMode === true;
}

export function resolveOverlayScrollSpeed(preferences = {}, field, partId = null) {
  const fallback = overlayScrollSpeedDefaults[field] ?? 12;
  if (partId && preferences.sukiOutputIndividualScrollSpeed != null) {
    return clampOverlayScrollSpeed(preferences.sukiOutputIndividualScrollSpeed, fallback);
  }
  return clampOverlayScrollSpeed(preferences[field], fallback);
}

export function isOverlayScrollSpeedField(field) {
  return Object.prototype.hasOwnProperty.call(overlayScrollSpeedDefaults, field);
}
