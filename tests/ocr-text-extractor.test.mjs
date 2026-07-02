import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

import { createDefaultOcrTextExtractor, createProfileAwareTextExtractor } from "../app/recognition/adapters/ocr-text-extractor.js";

test("default OCR selector exposes MAA-OCR for scans", () => {
  const extractor = createDefaultOcrTextExtractor({ engine: "maa-ocr" });

  assert.equal(typeof extractor.extract, "function");
});

test("default OCR selector maps auto to MAA-OCR", () => {
  const extractor = createDefaultOcrTextExtractor({ engine: "auto" });

  assert.equal(typeof extractor.extract, "function");
});

test("default OCR selector names MAA-OCR as the fallback engine", async () => {
  const source = await readFile(new URL("../app/recognition/adapters/ocr-text-extractor.js", import.meta.url), "utf8");

  assert.match(source, /RHODES_OCR_ENGINE \|\| "maa-ocr"/);
  assert.doesNotMatch(source, /RHODES_OCR_ENGINE \|\| "auto"/);
});

test("profile-aware OCR routing can force relic scans to a different extractor", async () => {
  const calls = [];
  const extractor = createProfileAwareTextExtractor({
    defaultExtractor: {
      async extract(frame) {
        calls.push("default");
        return { ...frame, text: "default" };
      },
    },
    profileExtractors: {
      relicsFull: {
        async extract(frame) {
          calls.push("relicsFull");
          return { ...frame, text: "profile-specific" };
        },
      },
    },
  });

  const relicFrame = await extractor.extract({ bytes: Buffer.from("x") }, { profile: { id: "relicsFull" } });
  const runFrame = await extractor.extract({ bytes: Buffer.from("x") }, { profile: { id: "runStatusFull" } });

  assert.equal(relicFrame.text, "profile-specific");
  assert.equal(runFrame.text, "default");
  assert.deepEqual(calls, ["relicsFull", "default"]);
});
