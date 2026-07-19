import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

import {
  defaultCustomOverlayLayout,
  normalizeCustomOverlayLayout,
  renderCustomOverlayLayout,
} from "../app/components/overlay-custom-layout.js";
import {
  applyOverlayEditorDelta,
  isOverlayEditorMode,
} from "../app/overlay/layout-editor.js";

test("custom overlay layout keeps every known part and clamps persisted geometry", () => {
  const layout = normalizeCustomOverlayLayout([
    { id: "status", enabled: false, x: -20, y: 2000, width: 4000, height: 10, zIndex: 99 },
    { id: "unknown", enabled: true, x: 10, y: 10, width: 100, height: 100, zIndex: 1 },
  ]);

  assert.equal(layout.length, defaultCustomOverlayLayout.length);
  assert.deepEqual(layout.map((item) => item.id), defaultCustomOverlayLayout.map((item) => item.id));
  assert.deepEqual(layout.find((item) => item.id === "status"), {
    id: "status",
    enabled: false,
    x: 0,
    y: 1000,
    width: 1920,
    height: 80,
    zIndex: 6,
  });
  assert.equal(layout.some((item) => item.id === "unknown"), false);
});

test("custom overlay renderer positions enabled parts on one 1920x1080 canvas", () => {
  const output = renderCustomOverlayLayout(
    [
      { id: "status", enabled: true, x: 96, y: 54, width: 960, height: 108, zIndex: 3 },
      { id: "relics", enabled: false, x: 0, y: 0, width: 1200, height: 170, zIndex: 1 },
    ],
    {},
    {},
    (part) => `<span data-rendered-part="${part}">${part}</span>`,
  );

  assert.match(output, /class="overlay-custom-canvas"/);
  assert.match(output, /data-overlay-layout-part="status"/);
  assert.match(output, /--overlay-x:5%;/);
  assert.match(output, /--overlay-y:5%;/);
  assert.match(output, /--overlay-width:50%;/);
  assert.match(output, /--overlay-height:10%;/);
  assert.match(output, /data-rendered-part="status"/);
  assert.doesNotMatch(output, /data-rendered-part="relics"/);
});

test("custom overlay canvas fills the browser source instead of the default 86px grid row", async () => {
  const styles = await readFile(new URL("../app/styles.css", import.meta.url), "utf8");

  assert.match(
    styles,
    /\.overlay-app\.overlay-custom\s*\{[^}]*display:\s*block;/s,
  );
  assert.match(
    styles,
    /\.overlay-app\.overlay-custom-editor\s*\{[^}]*grid-template-rows:\s*1fr;/s,
  );
});

test("overlay editor mode is opt-in and never affects the OBS custom overlay URL", () => {
  assert.equal(isOverlayEditorMode(new URLSearchParams("layout=custom&edit=1")), true);
  assert.equal(isOverlayEditorMode(new URLSearchParams("layout=custom")), false);
  assert.equal(isOverlayEditorMode(new URLSearchParams("layout=default&edit=1")), false);
});

test("overlay editor converts viewport drag deltas to the 1920x1080 canvas", () => {
  const layout = normalizeCustomOverlayLayout([
    { id: "status", enabled: true, x: 40, y: 36, width: 1200, height: 120, zIndex: 1 },
  ]);

  const moved = applyOverlayEditorDelta(layout, "status", "move", 96, 54, 960, 540);
  assert.deepEqual(moved.find((item) => item.id === "status"), {
    id: "status",
    enabled: true,
    x: 232,
    y: 144,
    width: 1200,
    height: 120,
    zIndex: 1,
  });
});

test("overlay editor resize clamps minimum size and canvas boundaries", () => {
  const layout = normalizeCustomOverlayLayout([
    { id: "operators", enabled: true, x: 1460, y: 260, width: 420, height: 620, zIndex: 5 },
  ]);

  const enlarged = applyOverlayEditorDelta(layout, "operators", "resize", 500, 500, 1920, 1080);
  assert.deepEqual(enlarged.find((item) => item.id === "operators"), {
    id: "operators",
    enabled: true,
    x: 1460,
    y: 260,
    width: 460,
    height: 820,
    zIndex: 5,
  });

  const shrunk = applyOverlayEditorDelta(layout, "operators", "resize", -1000, -1000, 1920, 1080);
  assert.equal(shrunk.find((item) => item.id === "operators").width, 160);
  assert.equal(shrunk.find((item) => item.id === "operators").height, 80);
});
