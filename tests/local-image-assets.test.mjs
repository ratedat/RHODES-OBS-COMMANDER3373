import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import test from "node:test";

const DATA_FILES = [
  "../data/campaigns.json",
  "../data/performances.json",
  "../data/selectable-effects.json",
];
const IMAGE_URL = /\.(?:gif|jpe?g|png|svg|webp)(?:[?#]|$)/i;
const IMAGE_SIGNATURES = new Map([
  [".gif", ["47494638"]],
  [".jpg", ["ffd8ff"]],
  [".jpeg", ["ffd8ff"]],
  [".png", ["89504e470d0a1a0a"]],
  [".webp", ["52494646"]],
]);

function visit(value, callback) {
  if (Array.isArray(value)) {
    value.forEach((item) => visit(item, callback));
    return;
  }
  if (!value || typeof value !== "object") return;
  callback(value);
  Object.values(value).forEach((item) => visit(item, callback));
}

test("remote image metadata has a valid local runtime asset", () => {
  for (const relativePath of DATA_FILES) {
    const dataUrl = new URL(relativePath, import.meta.url);
    const data = JSON.parse(readFileSync(dataUrl, "utf8"));

    visit(data, (value) => {
      if (typeof value.sourceUrl !== "string" || !IMAGE_URL.test(value.sourceUrl)) return;

      assert.equal(
        typeof value.localPath,
        "string",
        `${relativePath}: ${value.sourceUrl} has no localPath`,
      );
      assert.equal(
        existsSync(new URL(`../${value.localPath}`, import.meta.url)),
        true,
        `${relativePath}: ${value.localPath} does not exist`,
      );

      const extension = path.extname(value.localPath).toLowerCase();
      const signatures = IMAGE_SIGNATURES.get(extension);
      if (!signatures) return;
      const body = readFileSync(new URL(`../${value.localPath}`, import.meta.url));
      const prefix = body.subarray(0, 12).toString("hex");
      assert.equal(
        signatures.some((signature) => prefix.startsWith(signature)),
        true,
        `${relativePath}: ${value.localPath} is not a valid ${extension} image`,
      );
    });
  }
});
