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
  assert.match(source, /const assetDirectories = \["bosses", "performances", "selectable-effects", "ui"\]/);
  assert.match(
    source,
    /path\.join\(repoRoot, "docs", "guides", "tournament-remote-input\.md"\)/,
  );
  assert.match(
    source,
    /path\.join\(outputDir, "docs", "guides", "tournament-remote-input\.md"\)/,
  );
  assert.match(
    source,
    /path\.join\(repoRoot, "docs", "guides", "output-css-customization\.md"\)/,
  );
  assert.match(
    source,
    /path\.join\(outputDir, "docs", "guides", "output-css-customization\.md"\)/,
  );
  assert.match(source, /path\.join\(repoRoot, "出力CSSカスタマイズガイド\.html"\)/u);
  assert.match(source, /path\.join\(outputDir, "出力CSSカスタマイズガイド\.html"\)/u);
});

test("output CSS guide documents the supported customization contract", () => {
  const guide = readFileSync(
    new URL("../docs/guides/output-css-customization.md", import.meta.url),
    "utf8",
  );

  assert.match(guide, /統合Overlay用ユーザーCSS/u);
  assert.match(guide, /個別ウィンドウ用ユーザーCSS/u);
  assert.match(guide, /--overlay-font-color/);
  assert.match(guide, /--overlay-background-rgb/);
  assert.match(guide, /--overlay-background-alpha/);
  assert.match(guide, /\.overlay-part-status/);
  assert.match(guide, /\.overlay-part-special/);
  assert.match(guide, /@import/);
  assert.match(guide, /@font-face/);
  assert.match(guide, /外部画像/u);
  assert.match(guide, /CORS/);
  assert.match(guide, /65,536文字/u);
  assert.match(guide, /rhodes-output-profile/);
});

test("interactive HTML CSS guide is self-contained and shows concrete overlay examples", () => {
  const guide = readFileSync(
    new URL("../出力CSSカスタマイズガイド.html", import.meta.url),
    "utf8",
  );

  assert.match(guide, /<html lang="ja">/u);
  assert.match(guide, /実際の表示を見ながらCSSを作る/u);
  assert.match(guide, /id="overlay-demo"/u);
  assert.match(guide, /id="generated-css"/u);
  assert.match(guide, /--overlay-font-color/);
  assert.match(guide, /\.overlay-part-operators/);
  assert.match(guide, /@font-face/);
  assert.match(guide, /background-image: url/u);
  assert.match(guide, /navigator\.clipboard\.writeText/);
  assert.doesNotMatch(guide, /<script[^>]+src=/u);
  assert.doesNotMatch(guide, /<link[^>]+rel=["']stylesheet/u);
});
