import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";
import { getChoiceActive, markChoiceRenderDirtyAfterStateReplace, registerControlEvents } from "../app/control-events.js";
import { toggleChoiceExcluded } from "../app/control-actions.js";

test("template relics count as active choices even when not manually selected", () => {
  const state = { relics: [], operators: [] };
  const context = {
    getChoiceActive: (type, id) => type === "relic" && id === "is3_mizuki_relic_261",
    getEffectiveRelicCount: () => 2,
  };

  assert.equal(getChoiceActive("relic", "is3_mizuki_relic_261", state, context), true);
  assert.equal(getChoiceActive("operator", "exusiai", { relics: [], operators: ["exusiai"] }), true);
});

test("state replacement marks choice views dirty without reviving Control v2 screen state", () => {
  const ui = {};

  markChoiceRenderDirtyAfterStateReplace(ui);

  assert.equal(ui.forceFullChoiceRender, true);
  assert.equal("controlV2Screen" in ui, false);
  assert.equal("controlV2ChoiceTab" in ui, false);
});

test("toggleChoiceExcluded stores operator and relic display exclusion ids", () => {
  const state = { preferences: {} };

  toggleChoiceExcluded(state, "operator", "exusiai");
  toggleChoiceExcluded(state, "relic", "is5_sarkaz_relic_001");
  toggleChoiceExcluded(state, "operator", "exusiai");

  assert.deepEqual(state.preferences.operatorExcludedIds, []);
  assert.deepEqual(state.preferences.relicExcludedIds, ["is5_sarkaz_relic_001"]);
});

test("Control v2 no longer exposes the legacy scan execution path", async () => {
  const [appJs, controlEvents, apiJs] = await Promise.all([
    fs.readFile("app/app.js", "utf8"),
    fs.readFile("app/control-events.js", "utf8"),
    fs.readFile("app/lib/api.js", "utf8"),
  ]);

  assert.doesNotMatch(appJs, /getRecognitionScanActions/);
  assert.doesNotMatch(appJs, /trigger-recognition-scan/);
  assert.doesNotMatch(appJs, /cancel-recognition-scan/);
  assert.match(appJs, /MAAFramework版で実行/);
  assert.doesNotMatch(controlEvents, /recognitionScanUrl/);
  assert.doesNotMatch(controlEvents, /recognitionScanCancelUrl/);
  assert.doesNotMatch(controlEvents, /postRecognitionScan/);
  assert.doesNotMatch(apiJs, /recognitionScanUrl/);
  assert.doesNotMatch(apiJs, /recognitionScanCancelUrl/);
});


test("reset state replaces state and schedules a sidecar reload", async () => {
  const originalFetch = globalThis.fetch;
  const originalConfirm = globalThis.confirm;
  const nextState = { run: { campaignId: "is5_sarkaz" }, relics: [], operators: [] };
  let clickHandler = null;
  let replacedState = null;
  let notice = "";
  let reloads = 0;
  const ui = {};

  globalThis.fetch = async (url, options) => {
    assert.equal(url, "/api/state/reset");
    assert.equal(options.method, "POST");
    return { ok: true, json: async () => nextState };
  };
  globalThis.confirm = () => true;

  const app = {
    addEventListener(type, handler) {
      if (type === "click") clickHandler = handler;
    },
  };
  const context = {
    view: "sidecar",
    ui,
    replaceState(state) { replacedState = state; },
    reloadView() { reloads += 1; },
    setNotice(text) { notice = text; },
  };
  const button = {
    dataset: { action: "reset-state" },
    closest(selector) { return selector === "[data-action]" ? this : null; },
  };

  try {
    registerControlEvents(app, context);
    await clickHandler({ target: button });
    await new Promise((resolve) => setTimeout(resolve, 5));
  } finally {
    globalThis.fetch = originalFetch;
    globalThis.confirm = originalConfirm;
  }

  assert.equal(replacedState, nextState);
  assert.equal(ui.forceFullChoiceRender, true);
  assert.equal(notice, "状態を初期化しました。画面を再読み込みします。");
  assert.equal(reloads, 1);
});
