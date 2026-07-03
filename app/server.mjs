import http from "node:http";
import { randomUUID } from "node:crypto";
import fs from "node:fs/promises";
import { createReadStream } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { normalizeControlMode } from "./domain/ui-modes.js";
import { mergeImplementationHistory } from "./domain/operator-implementation-history.js";
import { isAppShellPath } from "./lib/view-route.js";
import { normalizePreferences } from "./lib/preferences.js";
import { normalizeRunStats } from "./domain/run-stats.js";
import { normalizeAdbSettings } from "./domain/adb-settings.js";
import { preserveLocalConfigOnReset } from "./domain/local-config.js";
import { extractRunStatusCandidates } from "./domain/recognition/run-status-extractor.js";
import { createRelicCandidateExtractor } from "./domain/recognition/relic-candidate-extractor.js";
import { createOperatorCandidateExtractor } from "./domain/recognition/operator-candidate-extractor.js";
import { createThoughtCandidateExtractor } from "./domain/recognition/thought-candidate-extractor.js";
import { createAgeCandidateExtractor } from "./domain/recognition/age-candidate-extractor.js";
import { findScanProfile, normalizeScanProfiles, profileIdFromScanBody } from "./domain/recognition/profiles.js";
import { runMaaResourceRecognition } from "./domain/recognition/maa-resource-scan-runner.js";
import { createAdbAdapter, detectAdbConnections } from "./recognition/adapters/adb-adapter.js";
import { detectWindowsHypervisor } from "./domain/system-diagnostics.js";
import { createGlmOcrRuntimeManager } from "./domain/glm-ocr-runtime.js";
import { createOllamaRuntimeManager } from "./domain/ollama-runtime.js";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");
const DATA = path.join(ROOT, "data");
const APP = path.join(ROOT, "app");
const STATE_DIR = process.env.ARKNIGHTS_STATE_DIR ? path.resolve(process.env.ARKNIGHTS_STATE_DIR) : DATA;
const CURRENT_STATE = path.join(STATE_DIR, "current-state.json");
const ADB_WORK_DIR = path.join(STATE_DIR, "adb-work");
const EXAMPLE_STATE = path.join(DATA, "overlay-state.example.json");
const PACKAGE_JSON = path.join(ROOT, "package.json");
const SCAN_PROFILES = path.join(DATA, "recognition", "scan-profiles.json");
const MAA_TASKS = path.join(DATA, "recognition", "maa-tasks.json");
const MAA_OPERATOR_OCR_MAP = path.join(DATA, "recognition", "maa-operator-name-ocr.json");
const MAA_GENERATED_PIPELINE = path.join(ROOT, "apps", "rhodes-suki", "resource", "base", "pipeline", "rhodes-generated.json");

const argvPort = (() => {
  const index = process.argv.indexOf("--port");
  if (index >= 0 && process.argv[index + 1]) return Number(process.argv[index + 1]);
  return null;
})();
const PORT = Number(process.env.PORT || argvPort || 5173);
const NO_CACHE_HEADERS = {
  "cache-control": "no-store, max-age=0, must-revalidate",
  "pragma": "no-cache",
  "expires": "0",
};

const mime = new Map([
  [".html", "text/html; charset=utf-8"],
  [".js", "text/javascript; charset=utf-8"],
  [".mjs", "text/javascript; charset=utf-8"],
  [".css", "text/css; charset=utf-8"],
  [".json", "application/json; charset=utf-8"],
  [".md", "text/markdown; charset=utf-8"],
  [".png", "image/png"],
  [".jpg", "image/jpeg"],
  [".jpeg", "image/jpeg"],
  [".webp", "image/webp"],
  [".gif", "image/gif"],
  [".svg", "image/svg+xml"],
  [".ico", "image/x-icon"],
]);

async function readJson(file) {
  return JSON.parse(await fs.readFile(file, "utf8"));
}

async function writeJsonAtomic(file, value) {
  await fs.mkdir(path.dirname(file), { recursive: true });
  const tmp = `${file}.${process.pid}.tmp`;
  await fs.writeFile(tmp, `${JSON.stringify(value, null, 2)}\n`, "utf8");
  await fs.rename(tmp, file);
}

function initialStateFromExample(example) {
  const state = structuredClone(example);
  state.version = state.version || 1;
  state.theme = state.theme || "obs-compact";
  state.mode = normalizeControlMode("casual");
  state.updatedAt = new Date().toISOString();
  state.run = state.run || {};
  state.run.campaignId = "is5_sarkaz";
  if (!state.run.squadId && typeof state.run.squad === "string") state.run.squadId = state.run.squad;
  state.run.squad = state.run.squad ?? null;
  state.run.squadId = state.run.squadId ?? null;
  state.run.difficulty = state.run.difficulty ?? null;
  state.run.performanceId = state.run.performanceId ?? null;
  state.run.squadRandomEffectOptionId = state.run.squadRandomEffectOptionId ?? null;
  normalizeRunStats(state.run);
  state.relics = Array.isArray(state.relics) ? state.relics : [];
  state.operators = Array.isArray(state.operators) ? state.operators : [];
  state.bossFlags = Array.isArray(state.bossFlags) ? state.bossFlags : [];
  state.bossSelections = state.bossSelections && typeof state.bossSelections === "object" && !Array.isArray(state.bossSelections) ? state.bossSelections : {};
  state.pendingSuggestions = Array.isArray(state.pendingSuggestions) ? state.pendingSuggestions : [];
  state.tournament = state.tournament || { pendingState: null, lastSubmissionAt: null, submittedBy: null };
  state.adb = normalizeAdbSettings(state.adb);
  state.preferences = normalizePreferences(state.preferences);
  return state;
}

function normalizeState(state) {
  if (!state || typeof state !== "object") throw new Error("state must be an object");
  const next = structuredClone(state);
  next.version = next.version || 1;
  next.mode = normalizeControlMode(next.mode);
  next.updatedAt = new Date().toISOString();
  next.run = next.run || {};
  next.run.campaignId = next.run.campaignId || "is5_sarkaz";
  if (!next.run.squadId && typeof next.run.squad === "string") next.run.squadId = next.run.squad;
  next.run.squadId = next.run.squadId || null;
  next.run.squad = next.run.squad ?? null;
  next.run.difficulty = next.run.difficulty === "" ? null : next.run.difficulty;
  next.run.performanceId = next.run.performanceId || null;
  next.run.squadRandomEffectOptionId = next.run.squadRandomEffectOptionId || null;
  normalizeRunStats(next.run);
  next.relics = Array.isArray(next.relics) ? [...new Set(next.relics.filter(Boolean))] : [];
  next.operators = Array.isArray(next.operators) ? [...new Set(next.operators.filter(Boolean))] : [];
  next.bossFlags = Array.isArray(next.bossFlags) ? next.bossFlags.filter(Boolean) : [];
  next.bossSelections = next.bossSelections && typeof next.bossSelections === "object" && !Array.isArray(next.bossSelections) ? next.bossSelections : {};
  next.pendingSuggestions = Array.isArray(next.pendingSuggestions) ? next.pendingSuggestions : [];
  next.tournament = next.tournament || { pendingState: null, lastSubmissionAt: null, submittedBy: null };
  next.adb = normalizeAdbSettings(next.adb);
  next.preferences = normalizePreferences(next.preferences);
  return next;
}

async function ensureState() {
  try {
    return normalizeState(await readJson(CURRENT_STATE));
  } catch {
    const example = await readJson(EXAMPLE_STATE);
    const state = initialStateFromExample(example);
    await writeJsonAtomic(CURRENT_STATE, state);
    return state;
  }
}

async function buildHealthPayload() {
  const [state, packageJson] = await Promise.all([
    ensureState(),
    readJson(PACKAGE_JSON).catch(() => ({})),
  ]);
  const run = state.run && typeof state.run === "object" ? state.run : {};
  return {
    ok: true,
    app: "RHODES OBS COMMANDER3373",
    version: packageJson.version || "unknown",
    state: {
      campaignId: run.campaignId || null,
      updatedAt: state.updatedAt || null,
      operators: Array.isArray(state.operators) ? state.operators.length : 0,
      relics: Array.isArray(state.relics) ? state.relics.length : 0,
      pendingSuggestions: Array.isArray(state.pendingSuggestions) ? state.pendingSuggestions.length : 0,
    },
    recognition: {
      active: false,
      activeProfileId: null,
      activeStage: null,
      lastScan: null,
    },
    endpoints: {
      state: "/api/state",
      master: "/api/master",
      recognitionMaaResource: "/api/recognition/maa-resource",
      hypervisor: "/api/system/hypervisor",
      glmOcr: "/api/ocr/glm/status",
      ollama: "/api/ocr/glm/ollama/status",
    },
  };
}

async function recognitionProfiles() {
  return normalizeScanProfiles(await readJson(SCAN_PROFILES).catch(() => ({ profiles: [] })));
}

async function maaGeneratedPipeline() {
  return readJson(MAA_GENERATED_PIPELINE).catch(() => ({}));
}

function httpError(status, message, details = {}) {
  return Object.assign(new Error(message), { status, details });
}

function legacyRecognitionScanError() {
  return httpError(410, "Legacy recognition scan API was retired. Use MAAFramework resource recognition instead.", {
    code: "legacy_recognition_scan_retired",
    replacement: "/api/recognition/maa-resource",
    targets: ["originiumIngots", "difficulty", "squad", "campaignSpecificValue", "operators", "relics"],
  });
}

function createRecognitionCandidateExtractors({ state, master, operatorOcrMap }) {
  const runStatusExtractor = (frame, context) => context.profile?.id === "runStatusFull"
    ? extractRunStatusCandidates(frame, {
      campaignId: state.run?.campaignId,
      squads: master.squads,
      difficultyGrades: master.difficultyGrades,
    })
    : [];
  const relicExtractor = createRelicCandidateExtractor({
    relics: master.relics,
    campaignId: state.run?.campaignId,
  });
  const operatorExtractor = createOperatorCandidateExtractor({
    operators: master.operators,
    operatorOcrMap,
  });
  const thoughtExtractor = createThoughtCandidateExtractor({
    selectableEffects: master.selectableEffects,
    campaignId: state.run?.campaignId,
  });
  const ageExtractor = createAgeCandidateExtractor({
    selectableEffects: master.selectableEffects,
    campaignId: state.run?.campaignId,
    difficulty: state.run?.difficulty,
    difficultyGrades: master.difficultyGrades,
  });
  return [runStatusExtractor, relicExtractor, operatorExtractor, thoughtExtractor, ageExtractor];
}

async function runMaaResourceRecognitionRequest(body = {}) {
  const [profiles, state, master, operatorOcrMap] = await Promise.all([
    recognitionProfiles(),
    ensureState(),
    masterData(),
    readJson(MAA_OPERATOR_OCR_MAP).catch(() => ({ rules: [], equivalenceClasses: [] })),
  ]);
  const profile = findScanProfile(profiles, profileIdFromScanBody(body));
  const pipeline = body.pipeline && typeof body.pipeline === "object" && !Array.isArray(body.pipeline)
    ? body.pipeline
    : await maaGeneratedPipeline();
  return runMaaResourceRecognition({
    profile,
    pipeline,
    taskResults: Array.isArray(body.taskResults) ? body.taskResults : [],
    candidateExtractors: createRecognitionCandidateExtractors({ state, master, operatorOcrMap }),
    recognitionContext: recognitionContextFromScanBody(body),
    source: body.source || "maa-framework",
    scanId: body.scanId || randomUUID(),
  });
}

function sanitizeFilePart(value, fallback = "scan") {
  const cleaned = String(value || fallback).replace(/[^a-zA-Z0-9._-]/g, "_").slice(0, 80);
  return cleaned || fallback;
}

function recognitionContextFromScanBody(body = {}) {
  const context = {};
  for (const key of [
    "operatorClass",
    "operatorClasses",
    "allowedOperatorClass",
    "allowedOperatorClasses",
    "allowedClasses",
    "expectedOperatorClass",
    "expectedOperatorClasses",
  ]) {
    if (Object.hasOwn(body, key)) context[key] = body[key];
  }
  if (body.recognitionContext && typeof body.recognitionContext === "object" && !Array.isArray(body.recognitionContext)) {
    for (const key of [
      "operatorClass",
      "operatorClasses",
      "allowedOperatorClass",
      "allowedOperatorClasses",
      "allowedClasses",
      "expectedOperatorClass",
      "expectedOperatorClasses",
    ]) {
      if (Object.hasOwn(body.recognitionContext, key)) context[key] = body.recognitionContext[key];
    }
  }
  return context;
}

function adbSettingsFromRequest(body, state) {
  return normalizeAdbSettings(body?.settings || state?.adb || {});
}

function timestampForFile(value = new Date()) {
  return new Date(value).toISOString().replace(/[:.]/g, "-");
}

export async function saveAdbScreenshotFrame(frame, { stateDir = STATE_DIR, now = new Date() } = {}) {
  if (!frame?.bytes) throw new Error("screenshot frame has no bytes");
  const dir = path.join(stateDir, "adb-screenshots");
  await fs.mkdir(dir, { recursive: true });
  const file = path.join(dir, `adb-test-${timestampForFile(now)}.png`);
  await fs.writeFile(file, frame.bytes);
  return {
    bytes: frame.bytes.length || 0,
    capturedAt: frame.capturedAt || new Date(now).toISOString(),
    path: file,
  };
}

export async function saveRecognitionAdbCaptureFrame(frame, { baseDir, scanId, profile, source = "adb", stage = "capture", iteration = null, passIndex = null, scanStartedAt = null, now = new Date() } = {}) {
  if (!baseDir) return null;
  if (source !== "adb") return null;
  if (!frame?.bytes) throw new Error("screenshot frame has no bytes");
  const timestamp = timestampForFile(scanStartedAt || frame.capturedAt || now);
  const profileId = sanitizeFilePart(profile?.id || frame.profileId, "profile");
  const safeScanId = sanitizeFilePart(scanId, "scan");
  const safeStage = sanitizeFilePart(stage, "capture");
  const scanDir = path.join(baseDir, `${timestamp}-${profileId}-${safeScanId}`);
  await fs.mkdir(scanDir, { recursive: true });
  const parts = [safeStage];
  if (Number.isFinite(Number(passIndex))) parts.push(`p${Number(passIndex)}`);
  if (Number.isFinite(Number(iteration))) parts.push(`i${Number(iteration)}`);
  const file = path.join(scanDir, `${parts.join("-")}.png`);
  await fs.writeFile(file, frame.bytes);
  return {
    bytes: frame.bytes.length || 0,
    capturedAt: frame.capturedAt || new Date(now).toISOString(),
    path: file,
  };
}

async function defaultAdbTester({ settings = {}, capture = false } = {}) {
  const normalized = normalizeAdbSettings(settings);
  const adapter = createAdbAdapter({ settings: normalized, workDir: ADB_WORK_DIR });
  const resolution = await adapter.getActualResolution();
  let screenshot = null;
  if (capture) {
    const frame = await adapter.capture({ profileId: "adbTest", stage: "screenshot-test" });
    screenshot = await saveAdbScreenshotFrame(frame);
  }
  return { ok: true, settings: normalized, resolution, screenshot };
}

function summarizeAdbSettingsForLog(settings = {}) {
  return {
    autoDetect: Boolean(settings.autoDetect),
    connectionPreset: settings.connectionPreset || "auto",
    adbPath: settings.adbPath || "",
    serial: settings.serial || "",
    restartServerOnFailure: Boolean(settings.restartServerOnFailure),
    restartProcessOnFailure: Boolean(settings.restartProcessOnFailure),
    retryCount: Number.isFinite(Number(settings.retryCount)) ? Number(settings.retryCount) : null,
  };
}

function summarizeAdbDetectionForLog(result = {}) {
  return {
    selectedAdbPath: result.selectedAdbPath || "",
    runtime: result.runtime ? summarizeAdbSettingsForLog(result.runtime) : null,
    connect: result.connect
      ? {
          address: result.connect.address || "",
          recovered: Boolean(result.connect.recovered),
          error: result.connect.error || null,
        }
      : null,
    adbCandidates: Array.isArray(result.adbCandidates)
      ? result.adbCandidates.map((item) => ({
          path: item.path || "",
          source: item.source || "",
          preset: item.preset || "",
          exists: Boolean(item.exists),
          available: Boolean(item.available),
          error: item.error || null,
        }))
      : [],
    devices: Array.isArray(result.devices)
      ? result.devices.map((item) => ({
          serial: item.serial || "",
          state: item.state || "",
          detail: item.detail || "",
        }))
      : [],
  };
}

function summarizeAdbTestForLog(result = {}) {
  return {
    ok: Boolean(result.ok),
    settings: result.settings ? summarizeAdbSettingsForLog(result.settings) : null,
    runtime: result.runtime ? summarizeAdbSettingsForLog(result.runtime) : null,
    resolution: result.resolution || null,
    screenshot: result.screenshot
      ? {
          bytes: Number(result.screenshot.bytes || 0),
          capturedAt: result.screenshot.capturedAt || "",
          path: result.screenshot.path || "",
        }
      : null,
  };
}

function summarizeErrorForLog(error) {
  return {
    name: error?.name || "Error",
    message: error instanceof Error ? error.message : String(error),
    status: Number(error?.status) || null,
    code: error?.details?.code || error?.code || null,
    details: error?.details || null,
  };
}

function logAdbDiagnostic(event, payload = {}, level = "log") {
  const line = JSON.stringify({ event, at: new Date().toISOString(), ...payload });
  if (level === "error") console.error(`[adb-diagnostic] ${line}`);
  else console.log(`[adb-diagnostic] ${line}`);
}


async function masterData() {
  const [campaigns, squadsRaw, relicsRaw, operatorsRaw, performancesRaw, selectableEffectsRaw, tiersRaw, gradesRaw, variantsRaw, effectRulesRaw, startTemplatesRaw, operatorHistoryRaw] = await Promise.all([
    readJson(path.join(DATA, "campaigns.json")),
    readJson(path.join(DATA, "squads.json")),
    readJson(path.join(DATA, "relics.json")),
    readJson(path.join(DATA, "operators.json")),
    readJson(path.join(DATA, "performances.json")).catch(() => ({ performances: [] })),
    readJson(path.join(DATA, "selectable-effects.json")).catch(() => ({ selectableEffects: [] })),
    readJson(path.join(DATA, "difficulty-tiers.json")),
    readJson(path.join(DATA, "difficulty-grades.json")).catch(() => ({ campaignDifficultyGrades: {} })),
    readJson(path.join(DATA, "relic-effect-variants.json")).catch(() => ({ variants: [] })),
    readJson(path.join(DATA, "relic-effect-rules.json")).catch(() => ({ rules: [], tagGroups: {} })),
    readJson(path.join(DATA, "start-templates.json")).catch(() => ({ templates: [] })),
    readJson(path.join(DATA, "operator-implementation-history.json")).catch(() => ({ history: [] })),
  ]);
  return {
    campaigns,
    squads: squadsRaw.squads || [],
    relics: relicsRaw.relics || [],
    operators: mergeImplementationHistory(operatorsRaw.operators || [], operatorHistoryRaw.history || []).operators,
    performances: performancesRaw.performances || [],
    selectableEffects: selectableEffectsRaw.selectableEffects || [],
    difficultyTiers: tiersRaw.campaignDifficultyTiers || {},
    difficultyGrades: gradesRaw.campaignDifficultyGrades || {},
    relicEffectVariants: variantsRaw.variantGroups || variantsRaw.variants || variantsRaw.relicEffectVariants || [],
    relicEffectRules: {
      version: effectRulesRaw.version || 1,
      tagGroups: effectRulesRaw.tagGroups || {},
      rules: effectRulesRaw.rules || [],
    },
    startTemplates: startTemplatesRaw.templates || [],
  };
}

function sendJson(res, status, value) {
  const body = JSON.stringify(value, null, 2);
  res.writeHead(status, {
    "content-type": "application/json; charset=utf-8",
    ...NO_CACHE_HEADERS,
  });
  res.end(body);
}

function sendText(res, status, text) {
  res.writeHead(status, { "content-type": "text/plain; charset=utf-8", ...NO_CACHE_HEADERS });
  res.end(text);
}

async function readBody(req) {
  const chunks = [];
  for await (const chunk of req) chunks.push(chunk);
  return Buffer.concat(chunks).toString("utf8");
}

function safeStaticPath(urlPath) {
  const decoded = decodeURIComponent(urlPath.split("?")[0]);
  const normalized = path.normalize(decoded).replace(/^([/\\])+/, "");
  const file = path.resolve(ROOT, normalized);
  if (!file.startsWith(ROOT)) return null;
  return file;
}

async function serveFile(res, file) {
  try {
    const stat = await fs.stat(file);
    if (stat.isDirectory()) return false;
    const ext = path.extname(file).toLowerCase();
    const base = path.basename(file);
    const noCache = [".html", ".js", ".mjs", ".css", ".json", ".md"].includes(ext) || base === "LICENSE";
    res.writeHead(200, {
      "content-type": base === "LICENSE" ? "text/plain; charset=utf-8" : (mime.get(ext) || "application/octet-stream"),
      ...(noCache ? NO_CACHE_HEADERS : { "cache-control": "public, max-age=600" }),
      ...(ext === ".html" ? { "clear-site-data": '"cache"' } : {}),
    });
    createReadStream(file).pipe(res);
    return true;
  } catch {
    return false;
  }
}

function legacyControlRedirectLocation(url) {
  const tabToScreen = new Map([
    ["run", "common"],
    ["relics", "relics"],
    ["operators", "operators"],
    ["flags", "sidecar"],
    ["obs", "obs"],
  ]);
  const screen = tabToScreen.get(url.searchParams.get("tab")) || url.searchParams.get("screen");
  const params = new URLSearchParams();
  if (screen) params.set("screen", screen);
  const query = params.toString();
  return `/control-v2${query ? `?${query}` : ""}`;
}

export function createAppServer({
  adbDetector = detectAdbConnections,
  adbTester = defaultAdbTester,
  adbPathPicker = null,
  hypervisorDetector = detectWindowsHypervisor,
  glmOcrRuntimeManager = createGlmOcrRuntimeManager({ stateDir: STATE_DIR }),
  ollamaRuntimeManager = createOllamaRuntimeManager({ stateDir: STATE_DIR }),
} = {}) {
  return http.createServer(async (req, res) => {
  try {
    const url = new URL(req.url || "/", `http://${req.headers.host || "localhost"}`);

    if (req.method === "GET" && url.pathname === "/api/master") {
      return sendJson(res, 200, await masterData());
    }

    if (req.method === "GET" && url.pathname === "/api/health") {
      return sendJson(res, 200, await buildHealthPayload());
    }

    if (req.method === "GET" && url.pathname === "/api/state") {
      return sendJson(res, 200, await ensureState());
    }

    if (req.method === "PUT" && url.pathname === "/api/state") {
      const state = normalizeState(JSON.parse(await readBody(req)));
      await writeJsonAtomic(CURRENT_STATE, state);
      return sendJson(res, 200, state);
    }

    if (req.method === "POST" && url.pathname === "/api/state/reset") {
      const previousState = await ensureState().catch(() => null);
      const state = normalizeState(preserveLocalConfigOnReset(initialStateFromExample(await readJson(EXAMPLE_STATE)), previousState));
      await writeJsonAtomic(CURRENT_STATE, state);
      return sendJson(res, 200, state);
    }



    if (req.method === "POST" && url.pathname === "/api/adb/select-path") {
      if (typeof adbPathPicker !== "function") throw httpError(501, "ADB file picker is available only in the desktop app.", { code: "adb_picker_unavailable" });
      const result = await adbPathPicker();
      return sendJson(res, 200, { canceled: Boolean(result?.canceled), path: result?.path || result?.filePath || "" });
    }

    if (req.method === "POST" && url.pathname === "/api/adb/detect") {
      const bodyText = await readBody(req);
      const body = bodyText ? JSON.parse(bodyText) : {};
      const state = await ensureState();
      const settings = adbSettingsFromRequest(body, state);
      const requestId = randomUUID();
      logAdbDiagnostic("adb_detect_start", { requestId, settings: summarizeAdbSettingsForLog(settings) });
      try {
        const result = await adbDetector({ settings });
        logAdbDiagnostic("adb_detect_success", { requestId, ...summarizeAdbDetectionForLog(result) });
        return sendJson(res, 200, result);
      } catch (error) {
        logAdbDiagnostic("adb_detect_error", { requestId, settings: summarizeAdbSettingsForLog(settings), error: summarizeErrorForLog(error) }, "error");
        throw error;
      }
    }

    if (req.method === "POST" && url.pathname === "/api/adb/test") {
      const bodyText = await readBody(req);
      const body = bodyText ? JSON.parse(bodyText) : {};
      const state = await ensureState();
      const settings = adbSettingsFromRequest(body, state);
      const capture = Boolean(body.capture);
      const requestId = randomUUID();
      logAdbDiagnostic("adb_test_start", { requestId, capture, settings: summarizeAdbSettingsForLog(settings) });
      try {
        const result = await adbTester({ settings, capture });
        logAdbDiagnostic("adb_test_success", { requestId, capture, ...summarizeAdbTestForLog(result) });
        return sendJson(res, 200, result);
      } catch (error) {
        logAdbDiagnostic("adb_test_error", { requestId, capture, settings: summarizeAdbSettingsForLog(settings), error: summarizeErrorForLog(error) }, "error");
        throw error;
      }
    }

    if (req.method === "GET" && url.pathname === "/api/ocr/glm/status") {
      return sendJson(res, 200, await glmOcrRuntimeManager.status());
    }

    if (req.method === "POST" && url.pathname === "/api/ocr/glm/install") {
      return sendJson(res, 202, await glmOcrRuntimeManager.install());
    }

    if (req.method === "POST" && url.pathname === "/api/ocr/glm/uninstall") {
      return sendJson(res, 200, await glmOcrRuntimeManager.uninstall());
    }

    if (req.method === "GET" && url.pathname === "/api/ocr/glm/ollama/status") {
      return sendJson(res, 200, await ollamaRuntimeManager.status());
    }

    if (req.method === "POST" && url.pathname === "/api/ocr/glm/ollama/install") {
      return sendJson(res, 202, await ollamaRuntimeManager.install());
    }

    if (req.method === "POST" && url.pathname === "/api/ocr/glm/ollama/start") {
      return sendJson(res, 200, await ollamaRuntimeManager.start());
    }

    if (req.method === "POST" && url.pathname === "/api/ocr/glm/ollama/uninstall") {
      return sendJson(res, 200, await ollamaRuntimeManager.uninstall());
    }

    if (req.method === "GET" && url.pathname === "/api/system/hypervisor") {
      return sendJson(res, 200, await hypervisorDetector());
    }

    if (req.method === "GET" && url.pathname === "/api/recognition/scan/status") {
      throw legacyRecognitionScanError();
    }

    if (req.method === "POST" && url.pathname === "/api/recognition/scan") {
      throw legacyRecognitionScanError();
    }

    if (req.method === "POST" && url.pathname === "/api/recognition/maa-resource") {
      const bodyText = await readBody(req);
      const body = bodyText ? JSON.parse(bodyText) : {};
      const result = await runMaaResourceRecognitionRequest(body);
      return sendJson(res, 200, { result });
    }

    if (req.method === "POST" && url.pathname === "/api/recognition/scan/cancel") {
      throw legacyRecognitionScanError();
    }

    if (req.method === "GET" && url.pathname.startsWith("/trigger/scan/")) {
      throw legacyRecognitionScanError();
    }

    if (req.method !== "GET" && req.method !== "HEAD") {
      return sendText(res, 405, "Method not allowed");
    }

    if (url.pathname === "/") {
      res.writeHead(302, { location: "/sidecar" });
      return res.end();
    }

    if (url.pathname === "/control") {
      res.writeHead(302, { location: legacyControlRedirectLocation(url) });
      return res.end();
    }

    if (isAppShellPath(url.pathname)) {
      return serveFile(res, path.join(APP, "index.html"));
    }

    const file = safeStaticPath(url.pathname);
    if (file && (await serveFile(res, file))) return;
    sendText(res, 404, "Not found");
  } catch (error) {
    const status = Number(error?.status) || 500;
    sendJson(res, status, {
      error: error instanceof Error ? error.message : String(error),
      ...(error?.details ? { details: error.details } : {}),
    });
  }
  });
}

export function startServer({ port = PORT, host = "127.0.0.1", adbDetector, adbTester, adbPathPicker, hypervisorDetector, glmOcrRuntimeManager, ollamaRuntimeManager } = {}) {
  const server = createAppServer({ adbDetector, adbTester, adbPathPicker, hypervisorDetector, glmOcrRuntimeManager, ollamaRuntimeManager });
  return new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(port, host, () => {
      server.off("error", reject);
      const address = server.address();
      const actualPort = typeof address === "object" && address ? address.port : port;
      console.log(`RHODES OBS COMMANDER3373`);
      console.log(`Sidecar: http://${host}:${actualPort}/sidecar`);
      console.log(`Overlay: http://${host}:${actualPort}/overlay`);
      resolve({ server, port: actualPort, host });
    });
  });
}

function normalizeWindowsNamespacePath(value) {
  if (typeof value !== "string") return "";
  if (value.startsWith("\\\\?\\UNC\\")) return `\\\\${value.slice(8)}`;
  if (value.startsWith("\\\\?\\")) return value.slice(4);
  return value;
}

export function isDirectServerRun(argvEntry = process.argv[1], moduleUrl = import.meta.url) {
  if (!argvEntry) return false;
  const argvPath = path.normalize(normalizeWindowsNamespacePath(path.resolve(argvEntry)));
  const modulePath = path.normalize(normalizeWindowsNamespacePath(fileURLToPath(moduleUrl)));
  return argvPath === modulePath;
}

if (isDirectServerRun()) {
  startServer().catch((error) => {
    console.error(error instanceof Error ? error.message : String(error));
    process.exit(1);
  });
}
