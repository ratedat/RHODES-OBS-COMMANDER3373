import test from "node:test";
import assert from "node:assert/strict";
import { preserveLocalConfigOnReset } from "../app/domain/local-config.js";

test("preserveLocalConfigOnReset keeps adb and UI/OBS preferences", () => {
  const resetState = {
    run: { campaignId: "is5_sarkaz", difficulty: null },
    adb: { adbPath: "", serial: "" },
    preferences: { operatorSort: "rarity_desc", compactRelicScrollSpeed: 9 },
    relics: [],
  };
  const previousState = {
    run: { campaignId: "is3_mizuki", difficulty: 18 },
    adb: { adbPath: "M:/Program Files/Netease/MuMu Player 12/shell/adb.exe", serial: "127.0.0.1:16384", autoDetect: false },
    preferences: { operatorSort: "implementation_desc", operatorGridColumns: 4, compactRelicScrollSpeed: 21, horizontalOperatorScrollSpeed: 6, ocrEngine: "windows-glm" },
    relics: ["is3_001"],
  };

  const next = preserveLocalConfigOnReset(resetState, previousState);

  assert.equal(next.run.campaignId, "is5_sarkaz");
  assert.deepEqual(next.relics, []);
  assert.equal(next.adb.adbPath, "M:/Program Files/Netease/MuMu Player 12/shell/adb.exe");
  assert.equal(next.adb.serial, "127.0.0.1:16384");
  assert.equal(next.adb.autoDetect, false);
  assert.equal(next.preferences.operatorSort, "implementation_desc");
  assert.equal(next.preferences.operatorGridColumns, 4);
  assert.equal(next.preferences.compactRelicScrollSpeed, 21);
  assert.equal(next.preferences.horizontalOperatorScrollSpeed, 6);
  assert.equal(next.preferences.ocrEngine, "glm-ocr");
});
