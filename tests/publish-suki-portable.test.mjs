import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

test("portable publisher includes local-image master data and assets", () => {
  const source = readFileSync(
    new URL("../tools/publish-suki-portable.mjs", import.meta.url),
    "utf8",
  );

  assert.match(source, /"data\/campaigns\.json"/);
  assert.match(source, /"data\/performances\.json"/);
  assert.match(source, /"data\/selectable-effects\.json"/);
  assert.match(source, /const assetDirectories = \["bosses", "performances", "selectable-effects"\]/);
});
