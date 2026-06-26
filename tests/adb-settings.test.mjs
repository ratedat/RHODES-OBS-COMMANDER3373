import test from "node:test";
import assert from "node:assert/strict";

import {
  adbConnectionPresetOptions,
  buildAdbCandidatePaths,
  normalizeAdbSettings,
  parseAdbDevices,
  resolveAdbRuntimeSettings,
} from "../app/domain/adb-settings.js";
import { updateAdbSetting } from "../app/control-actions.js";

test("normalizeAdbSettings keeps GUI connection settings conservative", () => {
  const settings = normalizeAdbSettings({
    autoDetect: false,
    connectionPreset: "mumu",
    adbPath: "  M:/Program Files/Netease/MuMu Player 12/shell/adb.exe  ",
    serial: "  127.0.0.1:16384  ",
    screenshotExtension: false,
    restartServerOnFailure: false,
    unknown: "ignored",
  });

  assert.deepEqual(settings, {
    autoDetect: false,
    connectionPreset: "mumu",
    adbPath: "M:/Program Files/Netease/MuMu Player 12/shell/adb.exe",
    serial: "127.0.0.1:16384",
    emulatorPath: "",
    screenshotExtension: false,
    restartServerOnFailure: false,
    restartProcessOnFailure: true,
    closeAdbOnExit: false,
    lightweightAdb: false,
  });
});

test("resolveAdbRuntimeSettings prefers GUI settings over environment variables", () => {
  const runtime = resolveAdbRuntimeSettings({
    adbPath: "C:/custom/adb.exe",
    serial: "127.0.0.1:20000",
  }, {
    ARKNIGHTS_ADB_PATH: "C:/env/adb.exe",
    ARKNIGHTS_ADB_SERIAL: "env-serial",
  });

  assert.equal(runtime.adbPath, "C:/custom/adb.exe");
  assert.equal(runtime.serial, "127.0.0.1:20000");
});

test("resolveAdbRuntimeSettings falls back to environment and then adb", () => {
  assert.deepEqual(resolveAdbRuntimeSettings({}, { ARKNIGHTS_ADB_PATH: "C:/env/adb.exe", ARKNIGHTS_ADB_SERIAL: "env-serial" }), {
    adbPath: "C:/env/adb.exe",
    serial: "env-serial",
    autoDetect: true,
    connectionPreset: "auto",
  });
  assert.deepEqual(resolveAdbRuntimeSettings({}, {}), {
    adbPath: "adb",
    serial: "",
    autoDetect: true,
    connectionPreset: "auto",
  });
});

test("parseAdbDevices reads emulator serials and offline states", () => {
  const devices = parseAdbDevices(`List of devices attached
127.0.0.1:16384 device product:MuMu model:MuMu_Player transport_id:1
emulator-5554 offline transport_id:2

`);

  assert.deepEqual(devices, [
    { serial: "127.0.0.1:16384", state: "device", detail: "product:MuMu model:MuMu_Player transport_id:1" },
    { serial: "emulator-5554", state: "offline", detail: "transport_id:2" },
  ]);
});

test("buildAdbCandidatePaths includes MAA-style MuMu paths and de-duplicates", () => {
  const candidates = buildAdbCandidatePaths({
    env: { ARKNIGHTS_ADB_PATH: "M:/Program Files/Netease/MuMu Player 12/shell/adb.exe", ProgramFiles: "C:/Program Files" },
    driveLetters: ["M", "C"],
  });

  assert.equal(candidates[0].source, "env");
  assert.equal(candidates.some((item) => item.preset === "mumu" && item.path.includes("MuMu Player 12")), true);
  assert.equal(new Set(candidates.map((item) => item.path.toLowerCase())).size, candidates.length);
});

test("adbConnectionPresetOptions exposes auto, MuMu, and manual choices", () => {
  assert.deepEqual(adbConnectionPresetOptions.map((item) => item.id), ["auto", "mumu", "custom"]);
});


test("updateAdbSetting normalizes GUI values into state.adb", () => {
  const state = { adb: {} };
  updateAdbSetting(state, "autoDetect", "", false);
  updateAdbSetting(state, "connectionPreset", "mumu");
  updateAdbSetting(state, "serial", " 127.0.0.1:16384 ");

  assert.equal(state.adb.autoDetect, false);
  assert.equal(state.adb.connectionPreset, "mumu");
  assert.equal(state.adb.serial, "127.0.0.1:16384");
});
