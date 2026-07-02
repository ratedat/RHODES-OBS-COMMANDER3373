import { clampOverlayScrollSpeed, overlayScrollSpeedDefaults } from "./overlay-config.js";
import { normalizeChoiceFilterIds } from "../domain/choice-filters.js";

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
  for (const [key, fallback] of Object.entries(overlayScrollSpeedDefaults)) {
    preferences[key] = clampOverlayScrollSpeed(preferences[key], fallback);
  }
  return preferences;
}
