import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import test from "node:test";

test("Sui coin OCR corpus renders Noto Sans JP line and sheet fixtures", async () => {
  const output = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-sui-coin-ocr-"));
  try {
    const run = spawnSync(
      "python",
      [
        "tools/generate-sui-coin-ocr-corpus.py",
        "--output",
        output,
        "--sizes",
        "16",
        "--variants",
        "active,dim",
        "--limit",
        "2",
      ],
      { cwd: process.cwd(), encoding: "utf8" },
    );
    assert.equal(run.status, 0, run.stderr || run.stdout);

    const manifest = JSON.parse(await fs.readFile(path.join(output, "manifest.json"), "utf8"));
    assert.equal(manifest.schemaVersion, 1);
    assert.equal(manifest.campaignId, "is6_sui");
    assert.equal(manifest.font.familyAssumption, "Noto Sans JP Regular");
    assert.equal(manifest.coinCount, 2);
    assert.equal(manifest.sampleCount, 4);
    assert.deepEqual(manifest.fontSizes, [16]);
    assert.deepEqual(manifest.variants, ["active", "dim"]);
    assert.match(manifest.samples[0].displayText, /^[衡厲花]-/u);
    assert.deepEqual(manifest.samples[0].lineRoi, [0, 0, 220, 32]);
    assert.deepEqual(manifest.samples[0].sheetRoi, [170, 116, 220, 32]);

    const line = await fs.readFile(path.join(output, manifest.samples[0].linePath));
    const sheet = await fs.readFile(path.join(output, manifest.samples[0].sheetPath));
    assert.deepEqual([...line.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10]);
    assert.deepEqual([...sheet.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10]);
  } finally {
    await fs.rm(output, { recursive: true, force: true });
  }
});
