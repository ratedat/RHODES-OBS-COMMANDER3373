export const adbConnectionPresetOptions = Object.freeze([
  { id: "auto", label: "自動" },
  { id: "mumu", label: "MuMu Player" },
  { id: "custom", label: "手動" },
]);

export const defaultAdbSettings = Object.freeze({
  autoDetect: true,
  connectionPreset: "auto",
  adbPath: "",
  serial: "",
  emulatorPath: "",
  screenshotExtension: true,
  restartServerOnFailure: true,
  restartProcessOnFailure: true,
  closeAdbOnExit: false,
  lightweightAdb: false,
});

const validPresets = new Set(adbConnectionPresetOptions.map((item) => item.id));

function asBool(value, fallback) {
  if (value === true || value === "true" || value === "1" || value === 1) return true;
  if (value === false || value === "false" || value === "0" || value === 0) return false;
  return fallback;
}

function cleanText(value) {
  return typeof value === "string" ? value.trim() : "";
}

export function normalizeAdbSettings(input = {}) {
  const source = input && typeof input === "object" ? input : {};
  const connectionPreset = validPresets.has(source.connectionPreset) ? source.connectionPreset : defaultAdbSettings.connectionPreset;
  return {
    autoDetect: asBool(source.autoDetect, defaultAdbSettings.autoDetect),
    connectionPreset,
    adbPath: cleanText(source.adbPath),
    serial: cleanText(source.serial),
    emulatorPath: cleanText(source.emulatorPath),
    screenshotExtension: asBool(source.screenshotExtension, defaultAdbSettings.screenshotExtension),
    restartServerOnFailure: asBool(source.restartServerOnFailure, defaultAdbSettings.restartServerOnFailure),
    restartProcessOnFailure: asBool(source.restartProcessOnFailure, defaultAdbSettings.restartProcessOnFailure),
    closeAdbOnExit: asBool(source.closeAdbOnExit, defaultAdbSettings.closeAdbOnExit),
    lightweightAdb: asBool(source.lightweightAdb, defaultAdbSettings.lightweightAdb),
  };
}

export function resolveAdbRuntimeSettings(input = {}, env = process.env) {
  const settings = normalizeAdbSettings(input);
  return {
    adbPath: settings.adbPath || cleanText(env.ARKNIGHTS_ADB_PATH) || "adb",
    serial: settings.serial || cleanText(env.ARKNIGHTS_ADB_SERIAL),
    autoDetect: settings.autoDetect,
    connectionPreset: settings.connectionPreset,
  };
}

export function parseAdbDevices(output = "") {
  return String(output)
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line && !/^List of devices attached/i.test(line) && !/^\* daemon/i.test(line))
    .map((line) => {
      const [serial = "", state = "", ...rest] = line.split(/\s+/);
      return { serial, state, detail: rest.join(" ") };
    })
    .filter((item) => item.serial && item.state);
}

function pushCandidate(candidates, seen, path, source, preset = "custom") {
  const normalized = cleanText(path);
  if (!normalized) return;
  const key = normalized.toLowerCase().replace(/\\/g, "/");
  if (seen.has(key)) return;
  seen.add(key);
  candidates.push({ path: normalized, source, preset });
}

function programFilesRoots(env = {}) {
  return [env.ProgramFiles, env["ProgramFiles(x86)"], env.LOCALAPPDATA].filter(Boolean);
}

export function buildAdbCandidatePaths({ env = process.env, driveLetters = [] } = {}) {
  const candidates = [];
  const seen = new Set();
  pushCandidate(candidates, seen, env.ARKNIGHTS_ADB_PATH, "env", "custom");
  pushCandidate(candidates, seen, "adb", "path", "custom");

  const roots = [
    ...programFilesRoots(env),
    ...driveLetters.flatMap((drive) => [`${drive}:\\Program Files`, `${drive}:\\Program Files (x86)`]),
  ];
  for (const root of roots) {
    pushCandidate(candidates, seen, `${root}\\Netease\\MuMu Player 12\\shell\\adb.exe`, "known-path", "mumu");
    pushCandidate(candidates, seen, `${root}\\Netease\\MuMu PlayerGlobal-12.0\\shell\\adb.exe`, "known-path", "mumu");
    pushCandidate(candidates, seen, `${root}\\MuMu Player 12\\shell\\adb.exe`, "known-path", "mumu");
    pushCandidate(candidates, seen, `${root}\\BlueStacks_nxt\\HD-Adb.exe`, "known-path", "bluestacks");
    pushCandidate(candidates, seen, `${root}\\LDPlayer\\LDPlayer9\\adb.exe`, "known-path", "ldplayer");
  }
  return candidates;
}
