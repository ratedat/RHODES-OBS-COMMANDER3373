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
    /path\.join\(repoRoot, "docs", "guides", "external-relay-server-setup\.md"\)/,
  );
  assert.match(
    source,
    /path\.join\(outputDir, "docs", "guides", "external-relay-server-setup\.md"\)/,
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

test("publish entry points enforce the publication boundary", () => {
  const portable = readFileSync(
    new URL("../tools/publish-suki-portable.mjs", import.meta.url),
    "utf8",
  );
  const publicDebug = readFileSync(
    new URL("../tools/package-suki-public-debug.mjs", import.meta.url),
    "utf8",
  );
  const apple = readFileSync(
    new URL("../tools/publish-apple-design-prototype.mjs", import.meta.url),
    "utf8",
  );

  for (const source of [portable, publicDebug, apple]) {
    assert.match(source, /createPublicationGuard/);
    assert.match(source, /boundary\.assertDestination\(/);
    assert.match(source, /boundary\.checkGitWorktree\(\)/);
    assert.match(source, /boundary\.checkTree\(/);
  }
  for (const source of [portable, publicDebug]) {
    assert.match(source, /boundary\.copyTree\(/);
    assert.doesNotMatch(source, /fs\.cp\(/);
  }

  const excludedPortableEntries = publicDebug.match(
    /const excludedPortableEntries = new Set\(\[[\s\S]*?\]\);/,
  )?.[0] ?? "";
  assert.match(excludedPortableEntries, /nodejs-runtime/);
  assert.match(excludedPortableEntries, /cloudflared-runtime/);
  assert.match(publicDebug, /await fs\.rm\(runtimeRoot, \{ recursive: true, force: true \}\)/);
  assert.ok(publicDebug.indexOf("boundary.checkTree(packageRoot") < publicDebug.lastIndexOf('run("tar.exe"'));
  assert.ok(apple.indexOf("boundary.checkTree(output") < apple.indexOf("existsSync(executable)"));
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
  assert.match(guide, /--overlay-canvas-background-rgb/);
  assert.match(guide, /--overlay-canvas-background-alpha/);
  assert.match(guide, /透明キャンバス.*半透明枠/u);
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
  assert.match(guide, /id="live-css-editor"/u);
  assert.match(guide, /--overlay-font-color/);
  assert.match(guide, /--overlay-canvas-background-rgb/);
  assert.match(guide, /透明キャンバス.*半透明枠/u);
  assert.match(guide, /id="item-background-alpha"/u);
  assert.match(guide, /id="item-border-color"/u);
  assert.match(guide, /\.overlay-part-operators/);
  assert.match(guide, /@font-face/);
  assert.match(guide, /background-image: url/u);
  assert.match(guide, /navigator\.clipboard\.writeText/);
  assert.doesNotMatch(guide, /<script[^>]+src=/u);
  assert.doesNotMatch(guide, /<link[^>]+rel=["']stylesheet/u);
});

test("interactive HTML CSS guide provides a safe live editor for copying into 3373", () => {
  const guide = readFileSync(
    new URL("../出力CSSカスタマイズガイド.html", import.meta.url),
    "utf8",
  );

  assert.match(guide, /<textarea id="live-css-editor"(?![^>]*readonly)[^>]*>/u);
  assert.match(guide, /id="live-css-character-count"/u);
  assert.match(guide, /id="live-css-validation"[^>]+aria-live="polite"/u);
  assert.match(guide, /id="live-preview-css"/u);
  assert.match(guide, /livePreviewStyle\.textContent = draft/u);
  assert.match(guide, /addEventListener\("input", scheduleLivePreview\)/u);
  assert.match(guide, /CSSを3373用にコピー/u);
  assert.match(guide, /javascript:/u);
  assert.match(guide, /65,536/u);
  assert.match(guide, /不透明度は0から1/u);
});

test("overlay CSS exposes stable tokens for nested cards and text", () => {
  const styles = readFileSync(new URL("../app/styles.css", import.meta.url), "utf8");
  const app = readFileSync(new URL("../app/app.js", import.meta.url), "utf8");
  const guide = readFileSync(
    new URL("../docs/guides/output-css-customization.md", import.meta.url),
    "utf8",
  );

  for (const token of [
    "--overlay-item-background-rgb",
    "--overlay-item-background-alpha",
    "--overlay-item-border-color",
    "--overlay-strong-font-color",
    "--overlay-muted-font-color",
    "--overlay-shadow",
  ]) {
    assert.match(app, new RegExp(token));
    assert.match(styles, new RegExp(`var\\(${token}`));
    assert.match(guide, new RegExp(token));
  }

  assert.match(
    styles,
    /\.overlay-part-relic[\s\S]+background: rgb\(var\(--overlay-item-background-rgb/u,
  );
  assert.match(
    styles,
    /\.overlay-part-operator[\s\S]+border: 1px solid var\(--overlay-item-border-color/u,
  );
});
