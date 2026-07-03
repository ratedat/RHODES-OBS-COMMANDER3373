import test from "node:test";
import assert from "node:assert/strict";
import { isAppShellPath, resolveAppView } from "../app/lib/view-route.js";

test("resolveAppView maps app routes to stable view ids", () => {
  assert.equal(resolveAppView("/", ""), "sidecar");
  assert.equal(resolveAppView("/control", ""), "sidecar");
  assert.equal(resolveAppView("/control-v2", ""), "sidecar");
  assert.equal(resolveAppView("/sidecar", ""), "sidecar");
  assert.equal(resolveAppView("/licenses", ""), "licenses");
  assert.equal(resolveAppView("/overlay", ""), "overlay");
  assert.equal(resolveAppView("/overlay/part/relics", ""), "overlay");
});

test("resolveAppView does not allow query params to revive retired Control shell", () => {
  assert.equal(resolveAppView("/", "?view=control"), "sidecar");
  assert.equal(resolveAppView("/", "?view=control-v2"), "sidecar");
  assert.equal(resolveAppView("/", "?view=sidecar"), "sidecar");
  assert.equal(resolveAppView("/", "?view=licenses"), "licenses");
  assert.equal(resolveAppView("/", "?view=unknown"), "sidecar");
});

test("isAppShellPath excludes retired Control shell routes", () => {
  assert.equal(isAppShellPath("/control-v2"), false);
  assert.equal(isAppShellPath("/control"), false);
  assert.equal(isAppShellPath("/sidecar"), true);
  assert.equal(isAppShellPath("/licenses"), true);
  assert.equal(isAppShellPath("/overlay/part/status"), true);
  assert.equal(isAppShellPath("/app/app.js"), false);
});
