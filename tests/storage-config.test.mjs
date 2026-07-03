import test from "node:test";
import assert from "node:assert/strict";
import path from "node:path";
import {
  documentsStorageDir,
  parseStoragePointer,
  portableStorageDir,
  serializeStoragePointer,
  storageTarget,
  targetFromStoredSelection,
} from "../app/runtime/storage-config.mjs";

test("portable storage sits beside the executable in packaged builds", () => {
  const dir = portableStorageDir({ isPackaged: true, execPath: "D:/Apps/RHODES/RhodesSuki.exe", appRoot: "O:/dev" });
  assert.equal(dir, path.join("D:/Apps/RHODES", "RHODES OBS COMMANDER3373 Data"));
});

test("portable storage follows the original portable executable path", () => {
  const dir = portableStorageDir({
    isPackaged: true,
    execPath: "C:/Users/owner/AppData/Local/Temp/.mount/RhodesSuki.exe",
    appRoot: "O:/dev",
    env: { PORTABLE_EXECUTABLE_FILE: "E:/Tools/RhodesSuki.exe" },
  });
  assert.equal(dir, path.join("E:/Tools", "RHODES OBS COMMANDER3373 Data"));
});

test("portable storage stays beside the Suki portable executable", () => {
  const dir = portableStorageDir({
    isPackaged: true,
    execPath: "O:/Arknights_Rogue_OBSTool/outputs/suki-portable/RhodesSuki.exe",
    appRoot: "O:/Arknights_Rogue_OBSTool",
  });
  assert.equal(dir, path.join("O:/Arknights_Rogue_OBSTool/outputs/suki-portable", "RHODES OBS COMMANDER3373 Data"));
});

test("development portable storage stays inside the project root", () => {
  const dir = portableStorageDir({ isPackaged: false, execPath: "C:/Apps/RHODES.exe", appRoot: "O:/Arknights_Rogue_OBSTool" });
  assert.equal(dir, path.join("O:/Arknights_Rogue_OBSTool", "user-data"));
});

test("documents storage uses a visible user Documents folder", () => {
  assert.equal(documentsStorageDir({ documentsPath: "C:/Users/owner/Documents" }), path.join("C:/Users/owner/Documents", "RHODES OBS COMMANDER3373"));
});

test("storage target derives settings, state, and desktop profile paths", () => {
  const target = storageTarget({ mode: "portable", storageDir: "D:/Tool/RHODES OBS COMMANDER3373 Data" });
  assert.equal(target.settingsFile, path.join("D:/Tool/RHODES OBS COMMANDER3373 Data", "desktop-settings.json"));
  assert.equal(target.stateDir, path.join("D:/Tool/RHODES OBS COMMANDER3373 Data", "state"));
  assert.equal(target.userDataDir, path.join("D:/Tool/RHODES OBS COMMANDER3373 Data", "profile"));
});

test("storage target exposes bundled portable runtime state beside the original executable", () => {
  const target = storageTarget({
    mode: "portable",
    appRoot: "O:/dev",
    execPath: "C:/Users/owner/AppData/Local/Temp/.mount/RhodesSuki.exe",
    isPackaged: true,
    env: { PORTABLE_EXECUTABLE_FILE: "E:/Tools/RhodesSuki.exe" },
  });
  assert.equal(target.stateDir, path.join("E:/Tools", "RHODES OBS COMMANDER3373 Data", "state"));
});

test("storage pointer round-trips the selected location", () => {
  const text = serializeStoragePointer({ mode: "documents", storageDir: "C:/Users/owner/Documents/RHODES OBS COMMANDER3373" });
  assert.deepEqual(parseStoragePointer(text), { mode: "documents", storageDir: "C:/Users/owner/Documents/RHODES OBS COMMANDER3373" });
});

test("targetFromStoredSelection resolves saved documents mode", () => {
  const target = targetFromStoredSelection({ mode: "documents", storageDir: "" }, { documentsPath: "C:/Users/owner/Documents" });
  assert.equal(target.storageDir, path.join("C:/Users/owner/Documents", "RHODES OBS COMMANDER3373"));
});

test("targetFromStoredSelection refreshes saved portable paths for the current executable", () => {
  const target = targetFromStoredSelection(
    { mode: "portable", storageDir: "O:/Arknights_Rogue_OBSTool/outputs/suki-portable/RHODES OBS COMMANDER3373 Data" },
    {
      appRoot: "O:/Arknights_Rogue_OBSTool",
      execPath: "E:/Tools/RhodesSuki.exe",
      isPackaged: true,
    },
  );
  assert.equal(target.storageDir, path.join("E:/Tools", "RHODES OBS COMMANDER3373 Data"));
});
