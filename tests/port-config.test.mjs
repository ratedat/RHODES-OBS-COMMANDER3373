import test from "node:test";
import assert from "node:assert/strict";
import {
  explicitPortValue,
  parseDesktopSettings,
  resolveStartupPort,
  serializeDesktopSettings,
  shouldPromptForPort,
} from "../app/runtime/port-config.mjs";

test("explicitPortValue prefers --port over env PORT", () => {
  assert.equal(explicitPortValue(["rhodes", "--port", "5188"], { PORT: "5174" }), "5188");
});

test("explicitPortValue falls back to env PORT", () => {
  assert.equal(explicitPortValue(["rhodes"], { PORT: "5174" }), "5174");
});

test("shouldPromptForPort prompts only when no explicit port exists", () => {
  assert.equal(shouldPromptForPort(["rhodes"], {}, { smokeTest: false }), true);
  assert.equal(shouldPromptForPort(["rhodes", "--port", "5174"], {}, { smokeTest: false }), false);
  assert.equal(shouldPromptForPort(["rhodes"], { PORT: "5175" }, { smokeTest: false }), false);
  assert.equal(shouldPromptForPort(["rhodes"], {}, { smokeTest: true }), false);
});

test("resolveStartupPort uses explicit, saved, then default values", () => {
  assert.equal(resolveStartupPort({ args: ["rhodes", "--port", "5188"], savedPort: 5174 }), 5188);
  assert.equal(resolveStartupPort({ args: ["rhodes"], env: {}, savedPort: 5174 }), 5174);
  assert.equal(resolveStartupPort({ args: ["rhodes"], env: {}, savedPort: null, defaultPort: 5190 }), 5190);
});

test("desktop settings parsing tolerates malformed input", () => {
  assert.deepEqual(parseDesktopSettings('{"port":5174}'), { port: 5174, storageMode: null, storageDir: "" });
  assert.deepEqual(parseDesktopSettings('{"port":5174,"storageMode":"documents","storageDir":"C:/Docs/RHODES"}'), { port: 5174, storageMode: "documents", storageDir: "C:/Docs/RHODES" });
  assert.deepEqual(parseDesktopSettings("{broken"), { port: null, storageMode: null, storageDir: "" });
});

test("serializeDesktopSettings normalizes invalid ports and preserves storage choice", () => {
  assert.equal(JSON.parse(serializeDesktopSettings({ port: 5188 })).port, 5188);
  assert.equal(JSON.parse(serializeDesktopSettings({ port: "bad" })).port, 5173);
  assert.deepEqual(JSON.parse(serializeDesktopSettings({ port: 5188, storageMode: "documents", storageDir: "C:/Docs/RHODES" })), {
    port: 5188,
    storageMode: "documents",
    storageDir: "C:/Docs/RHODES",
  });
});
