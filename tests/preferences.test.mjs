import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";

import { normalizeOcrEngine, normalizePreferences, ocrEngineOptions } from "../app/lib/preferences.js";
import {
  isTournamentOverlay,
  resolveOverlayAppearance,
  resolveOverlayBackgroundAlpha,
  resolveOverlayBackgroundEnabled,
  resolveOverlayScrollSpeed,
  shouldShowOverlayPartTitles,
} from "../app/lib/overlay-config.js";

test("OCR engine preference defaults to MAA-OCR", () => {
  assert.equal(normalizeOcrEngine(""), "maa-ocr");
  assert.equal(normalizeOcrEngine("unknown"), "maa-ocr");
  assert.equal(normalizeOcrEngine("profile"), "maa-ocr");
  assert.equal(normalizePreferences({}).ocrEngine, "maa-ocr");
});

test("OCR engine preference accepts GLM verification engines", () => {
  assert.equal(normalizeOcrEngine("glm-ocr"), "glm-ocr");
  assert.equal(normalizeOcrEngine("windows-glm"), "glm-ocr");
  assert.ok(ocrEngineOptions.some((option) => option.id === "glm-ocr"));
});

test("OCR engine preference exposes only MAA-OCR plus optional GLM", () => {
  assert.equal(normalizeOcrEngine("maa-ocr"), "maa-ocr");
  assert.equal(normalizeOcrEngine("maa-onnx"), "maa-ocr");
  assert.equal(normalizeOcrEngine("hybrid"), "maa-ocr");
  assert.equal(normalizeOcrEngine("paddle"), "maa-ocr");
  assert.deepEqual(ocrEngineOptions.map((option) => option.id), ["maa-ocr", "glm-ocr"]);
});

test("choice list filter preferences are normalized", () => {
  const preferences = normalizePreferences({
    operatorShowSelectedFirst: "true",
    operatorHideExcluded: false,
    operatorSelectedOnly: "1",
    operatorExcludedIds: ["texas", "", "texas", "exusiai"],
    relicShowSelectedFirst: true,
    relicHideExcluded: "false",
    relicSelectedOnly: 0,
    relicExcludedIds: ["is5_sarkaz_relic_001", null, "is5_sarkaz_relic_001"],
  });

  assert.equal(preferences.operatorShowSelectedFirst, true);
  assert.equal(preferences.operatorHideExcluded, false);
  assert.equal(preferences.operatorSelectedOnly, true);
  assert.deepEqual(preferences.operatorExcludedIds, ["texas", "exusiai"]);
  assert.equal(preferences.relicShowSelectedFirst, true);
  assert.equal(preferences.relicHideExcluded, false);
  assert.equal(preferences.relicSelectedOnly, false);
  assert.deepEqual(preferences.relicExcludedIds, ["is5_sarkaz_relic_001"]);
});

test("overlay background defaults to a fully transparent OBS canvas", () => {
  const preferences = normalizePreferences({});

  assert.equal(resolveOverlayBackgroundEnabled(preferences), false);
  assert.equal(resolveOverlayBackgroundAlpha(preferences), 0);
});

test("overlay background off removes the dark root canvas itself", async () => {
  const css = await fs.readFile("app/styles.css", "utf8");
  const rootTokens = css.match(/^:root\s*\{([\s\S]*?)^\}/m)?.[1] ?? "";

  assert.doesNotMatch(rootTokens, /color-scheme\s*:\s*dark/);
  assert.match(css, /:root\.overlay-background-disabled,[\s\S]*?\.overlay-background-disabled body,[\s\S]*?\.overlay-background-disabled #app\s*\{[\s\S]*?background:\s*transparent\s*!important/);
});

test("overlay background off removes integrated and nested neutral surfaces", async () => {
  const css = await fs.readFile("app/styles.css", "utf8");
  const transparentSurfaceRule = css.match(
    /\.overlay-background-disabled \.compact-overlay-shell,[\s\S]*?\.overlay-background-disabled \.stream-counts span\s*\{([\s\S]*?)\}/,
  );

  assert.ok(transparentSurfaceRule, "transparent overlay surface rule is missing");
  assert.match(transparentSurfaceRule[0], /\.overlay-background-disabled \.overlay-part-status-cell/);
  assert.match(transparentSurfaceRule[0], /\.overlay-background-disabled \.overlay-part-relic/);
  assert.match(transparentSurfaceRule[0], /\.overlay-background-disabled \.overlay-part-operator/);
  assert.match(transparentSurfaceRule[0], /\.overlay-background-disabled \.effect-row/);
  assert.match(transparentSurfaceRule[0], /\.overlay-background-disabled \.boss-card/);
  assert.match(transparentSurfaceRule[0], /\.overlay-background-disabled \.special-overlay-chip/);
  assert.match(transparentSurfaceRule[1], /background:\s*transparent\s*!important/);
  assert.match(transparentSurfaceRule[1], /border-color:\s*transparent\s*!important/);
  assert.match(transparentSurfaceRule[1], /box-shadow:\s*none\s*!important/);
});

test("overlay background opacity is clamped and applies only when background is enabled", () => {
  assert.equal(resolveOverlayBackgroundAlpha(normalizePreferences({
    sukiOutputBackgroundEnabled: true,
    sukiOutputBackgroundOpacity: 35,
  })), 0.35);
  assert.equal(resolveOverlayBackgroundAlpha(normalizePreferences({
    sukiOutputBackgroundEnabled: true,
    sukiOutputBackgroundOpacity: 140,
  })), 1);
  assert.equal(resolveOverlayBackgroundAlpha(normalizePreferences({
    sukiOutputBackgroundEnabled: false,
    sukiOutputBackgroundOpacity: 100,
  })), 0);
});

test("legacy transparent-background settings migrate without changing their visible result", () => {
  assert.equal(resolveOverlayBackgroundAlpha(normalizePreferences({
    sukiOutputTransparentBackground: false,
    sukiOutputBackgroundTransparency: 100,
  })), 1);
  assert.equal(resolveOverlayBackgroundAlpha(normalizePreferences({
    sukiOutputTransparentBackground: true,
    sukiOutputBackgroundTransparency: 35,
  })), 0.65);
});

test("individual overlay settings override the integrated defaults", () => {
  const preferences = normalizePreferences({
    sukiOutputTournamentMode: false,
    sukiOutputBackgroundEnabled: true,
    sukiOutputBackgroundOpacity: 80,
    sukiOutputShowPartTitles: true,
    sukiOutputParts: [{
      id: "operators",
      enabled: true,
      scrollEnabled: false,
      hideExcluded: true,
      width: 420,
      height: 620,
      tournamentMode: true,
      backgroundEnabled: false,
      backgroundOpacity: 25,
      showTitle: false,
    }],
  });

  assert.equal(resolveOverlayBackgroundEnabled(preferences, "operators"), false);
  assert.equal(resolveOverlayBackgroundAlpha(preferences, "operators"), 0);
  assert.equal(shouldShowOverlayPartTitles(preferences, "operators"), false);
  assert.equal(isTournamentOverlay(preferences, "operators"), true);
  assert.equal(resolveOverlayBackgroundAlpha(preferences, "relics"), 0.8);
  assert.equal(shouldShowOverlayPartTitles(preferences, "relics"), true);
  assert.equal(isTournamentOverlay(preferences, "relics"), false);
});

test("individual overlay titles default to visible and can be hidden", () => {
  assert.equal(shouldShowOverlayPartTitles(normalizePreferences({})), true);
  assert.equal(shouldShowOverlayPartTitles(normalizePreferences({
    sukiOutputShowPartTitles: false,
  })), false);
});

test("legacy output settings migrate to schema 2 without coupling integrated and individual appearance", () => {
  const preferences = normalizePreferences({
    sukiOutputTournamentMode: false,
    sukiOutputBackgroundEnabled: true,
    sukiOutputBackgroundOpacity: 72,
    sukiOutputIntegratedAppearance: {
      fontColor: "#112233",
      backgroundColor: "#223344",
      borderColor: "#334455",
      accentColor: "#445566",
      fontSizePercent: 125,
      customCss: ".integrated { color: red; }",
    },
    sukiOutputParts: [{
      id: "operators",
      tournamentMode: true,
      backgroundEnabled: false,
      backgroundOpacity: 20,
      showTitle: false,
    }],
  });

  assert.equal(preferences.sukiOutputSchemaVersion, 2);
  assert.equal(preferences.sukiOutputIndividualTournamentMode, false);
  assert.equal(preferences.sukiOutputIndividualBackgroundEnabled, true);
  assert.equal(preferences.sukiOutputIndividualBackgroundOpacity, 72);
  assert.equal(preferences.sukiOutputIndividualShowPartTitles, true);
  assert.equal(preferences.sukiOutputParts[0].tournamentMode, true);
  assert.equal(preferences.sukiOutputParts[0].backgroundEnabled, false);
  assert.equal(preferences.sukiOutputParts[0].backgroundOpacity, 20);
  assert.equal(preferences.sukiOutputParts[0].showTitle, false);
  assert.deepEqual(preferences.sukiOutputIndividualAppearance, preferences.sukiOutputIntegratedAppearance);

  preferences.sukiOutputIndividualAppearance.fontColor = "#AABBCC";
  assert.equal(preferences.sukiOutputIntegratedAppearance.fontColor, "#112233");
});

test("integrated and individual overlays resolve independent appearance and scroll speed", () => {
  const preferences = normalizePreferences({
    horizontalOperatorScrollSpeed: 7,
    sukiOutputIndividualScrollSpeed: 19,
    sukiOutputIntegratedAppearance: {
      fontColor: "#123456",
      backgroundColor: "#234567",
      borderColor: "#345678",
      accentColor: "#456789",
      fontSizePercent: 115,
      customCss: ".integrated-only {}",
    },
    sukiOutputIndividualAppearance: {
      fontColor: "#ABCDEF",
      backgroundColor: "#BCDEF0",
      borderColor: "#CDEF01",
      accentColor: "#DEF012",
      fontSizePercent: 85,
      customCss: ".individual-only {}",
    },
  });

  assert.equal(resolveOverlayAppearance(preferences).fontColor, "#123456");
  assert.equal(resolveOverlayAppearance(preferences, "operators").fontColor, "#ABCDEF");
  assert.equal(resolveOverlayAppearance(preferences).customCss, ".integrated-only {}");
  assert.equal(resolveOverlayAppearance(preferences, "operators").customCss, ".individual-only {}");
  assert.equal(resolveOverlayScrollSpeed(preferences, "horizontalOperatorScrollSpeed"), 7);
  assert.equal(resolveOverlayScrollSpeed(preferences, "horizontalOperatorScrollSpeed", "operators"), 19);
});
