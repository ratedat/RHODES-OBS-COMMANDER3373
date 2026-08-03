import {
  clampOverlayBackgroundOpacity,
  clampOverlayScrollSpeed,
  normalizeOverlayAppearance,
  overlayScrollSpeedDefaults,
} from "./overlay-config.js";
import { normalizeChoiceFilterIds } from "../domain/choice-filters.js";
import { normalizeCustomOverlayLayout } from "./overlay-layout-state.js";

export const gridColumnOptions = [1, 2, 3, 4, 5, 6];

export const ocrEngineOptions = Object.freeze([
  { id: "maa-ocr", label: "MAA-OCR" },
  { id: "glm-ocr", label: "GLM-OCR 任意検証" },
]);

const validOcrEngines = new Set(ocrEngineOptions.map((item) => item.id));
const ocrEngineAliases = new Map([
  ["auto", "maa-ocr"],
  ["profile", "maa-ocr"],
  ["maa", "maa-ocr"],
  ["maa-onnx", "maa-ocr"],
  ["onnx", "maa-ocr"],
  ["glm", "glm-ocr"],
  ["hybrid", "maa-ocr"],
  ["maa-hybrid", "maa-ocr"],
  ["onnx-hybrid", "maa-ocr"],
  ["paddle", "maa-ocr"],
  ["windows", "maa-ocr"],
  ["windows-paddle", "maa-ocr"],
  ["paddle-windows", "maa-ocr"],
  ["windows-glm", "glm-ocr"],
  ["glm-windows", "glm-ocr"],
  ["glm-hybrid", "glm-ocr"],
  ["hybrid-glm", "glm-ocr"],
]);
const booleanPreferenceFields = [
  "operatorShowSelectedFirst",
  "operatorHideExcluded",
  "operatorSelectedOnly",
  "relicShowSelectedFirst",
  "relicHideExcluded",
  "relicSelectedOnly",
];

export function clampGridColumns(value) {
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) return 2;
  return Math.min(6, Math.max(1, Math.trunc(numeric)));
}

export function normalizeOcrEngine(value) {
  const normalized = String(value || "maa-ocr").toLowerCase();
  if (ocrEngineAliases.has(normalized)) return ocrEngineAliases.get(normalized);
  return validOcrEngines.has(normalized) ? normalized : "maa-ocr";
}

function normalizeBoolean(value) {
  return value === true || value === "true" || value === 1 || value === "1";
}

function normalizeOutputParts(value, defaults) {
  if (!Array.isArray(value)) return [];
  return value
    .filter((part) => part && typeof part === "object" && String(part.id || "").trim())
    .map((part) => ({
      ...part,
      id: String(part.id).trim(),
      enabled: normalizeBoolean(part.enabled),
      scrollEnabled: normalizeBoolean(part.scrollEnabled),
      hideExcluded: normalizeBoolean(part.hideExcluded),
      width: Math.max(1, Math.round(Number(part.width) || 1)),
      height: Math.max(1, Math.round(Number(part.height) || 1)),
      tournamentMode: part.tournamentMode == null
        ? defaults.tournamentMode
        : normalizeBoolean(part.tournamentMode),
      backgroundEnabled: part.backgroundEnabled == null
        ? defaults.backgroundEnabled
        : normalizeBoolean(part.backgroundEnabled),
      backgroundOpacity: clampOverlayBackgroundOpacity(
        part.backgroundOpacity,
        defaults.backgroundOpacity,
      ),
      showTitle: part.showTitle == null
        ? defaults.showTitle
        : normalizeBoolean(part.showTitle),
    }));
}

export function normalizePreferences(value) {
  const preferences = value && typeof value === "object" && !Array.isArray(value) ? value : {};
  preferences.showUnreleasedOperators = normalizeBoolean(preferences.showUnreleasedOperators);
  preferences.ocrEngine = normalizeOcrEngine(preferences.ocrEngine);
  preferences.operatorSort ||= "rarity_desc";
  preferences.operatorGridColumns = clampGridColumns(preferences.operatorGridColumns ?? 2);
  preferences.relicGridColumns = clampGridColumns(preferences.relicGridColumns ?? 2);
  for (const field of booleanPreferenceFields) preferences[field] = normalizeBoolean(preferences[field]);
  preferences.operatorExcludedIds = normalizeChoiceFilterIds(preferences.operatorExcludedIds);
  preferences.relicExcludedIds = normalizeChoiceFilterIds(preferences.relicExcludedIds);
  preferences.sukiOverlayLayout = normalizeCustomOverlayLayout(preferences.sukiOverlayLayout);
  const hasBackgroundEnabled = Object.prototype.hasOwnProperty.call(
    preferences,
    "sukiOutputBackgroundEnabled",
  );
  if (!hasBackgroundEnabled
      && Object.prototype.hasOwnProperty.call(preferences, "sukiOutputTransparentBackground")) {
    const legacyTransparent = normalizeBoolean(preferences.sukiOutputTransparentBackground);
    const legacyTransparency = clampOverlayBackgroundOpacity(
      preferences.sukiOutputBackgroundTransparency,
      100,
    );
    preferences.sukiOutputBackgroundEnabled = !legacyTransparent || legacyTransparency < 100;
    preferences.sukiOutputBackgroundOpacity = legacyTransparent
      ? 100 - legacyTransparency
      : 100;
  } else {
    preferences.sukiOutputBackgroundEnabled = hasBackgroundEnabled
      ? normalizeBoolean(preferences.sukiOutputBackgroundEnabled)
      : false;
    preferences.sukiOutputBackgroundOpacity = clampOverlayBackgroundOpacity(
      preferences.sukiOutputBackgroundOpacity,
      100,
    );
  }
  preferences.sukiOutputTournamentMode = normalizeBoolean(preferences.sukiOutputTournamentMode);
  preferences.sukiOutputShowPartTitles = preferences.sukiOutputShowPartTitles == null
    ? true
    : normalizeBoolean(preferences.sukiOutputShowPartTitles);
  const integratedAppearance = normalizeOverlayAppearance(preferences.sukiOutputIntegratedAppearance);
  const individualAppearance = normalizeOverlayAppearance(
    preferences.sukiOutputIndividualAppearance,
    integratedAppearance,
  );
  preferences.sukiOutputIntegratedAppearance = integratedAppearance;
  preferences.sukiOutputIndividualAppearance = individualAppearance;
  preferences.sukiOutputIndividualTournamentMode = preferences.sukiOutputIndividualTournamentMode == null
    ? preferences.sukiOutputTournamentMode
    : normalizeBoolean(preferences.sukiOutputIndividualTournamentMode);
  preferences.sukiOutputIndividualBackgroundEnabled = preferences.sukiOutputIndividualBackgroundEnabled == null
    ? preferences.sukiOutputBackgroundEnabled
    : normalizeBoolean(preferences.sukiOutputIndividualBackgroundEnabled);
  preferences.sukiOutputIndividualBackgroundOpacity = clampOverlayBackgroundOpacity(
    preferences.sukiOutputIndividualBackgroundOpacity,
    preferences.sukiOutputBackgroundOpacity,
  );
  preferences.sukiOutputIndividualShowPartTitles = preferences.sukiOutputIndividualShowPartTitles == null
    ? preferences.sukiOutputShowPartTitles
    : normalizeBoolean(preferences.sukiOutputIndividualShowPartTitles);
  preferences.sukiOutputIndividualScrollSpeed = clampOverlayScrollSpeed(
    preferences.sukiOutputIndividualScrollSpeed,
    13,
  );
  preferences.sukiOutputParts = normalizeOutputParts(preferences.sukiOutputParts, {
    tournamentMode: preferences.sukiOutputIndividualTournamentMode,
    backgroundEnabled: preferences.sukiOutputIndividualBackgroundEnabled,
    backgroundOpacity: preferences.sukiOutputIndividualBackgroundOpacity,
    showTitle: preferences.sukiOutputIndividualShowPartTitles,
  });
  for (const [key, fallback] of Object.entries(overlayScrollSpeedDefaults)) {
    preferences[key] = clampOverlayScrollSpeed(preferences[key], fallback);
  }
  preferences.sukiOutputSchemaVersion = 2;
  return preferences;
}
