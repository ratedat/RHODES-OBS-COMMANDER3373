# MAA-style Recognition Migration

## Status

Accepted as the first migration step.

## Date

2026-06-26

## Context

ADB can capture MuMu screenshots and active app resolution, but Android UIAutomator only exposes the Unity surface for Arknights. Game state therefore has to come from screenshots, OCR, and image/template recognition.

MAA already solves the same class of problem with screenshot-driven task definitions. The useful ideas for this project are:

- 1280x720 base-resolution ROI definitions scaled to the active device resolution.
- Task JSON that separates screen detection, OCR/template recognition, and post-processing.
- OCR normalization before matching, especially Japanese spacing cleanup and number-like replacements.
- Recognition results treated as suggestions until reviewed.

## Decision

Keep the Electron app, overlay, state management, and review workflow in this repository. Add a recognition seam that accepts MAA-style task definitions and can later be backed by a richer OCR/template engine or an external worker.

The current first slice is:

- `data/recognition/maa-tasks.json` defines screen/candidate tasks.
- `app/domain/recognition/maa-style-recognizer.js` implements the small recognizer interface used by `scan-runner`.
- `app/domain/recognition/text-normalize.js` contains MAA-inspired text normalization primitives.
- `app/server.mjs` loads `maa-tasks.json` for the default ADB recognition runner.

This slice now vendors selected MAA task JSON references and the MAA license under `third_party/maa`, then syncs OCR replacement rules into `data/recognition/maa-ocr-rules.json`. MAA logos, trademarks, model files, and template images are not imported.

## Interface

The recognizer still satisfies the existing `scan-runner` interface:

- `classify(frame, context)` returns whether the current frame is a known screen.
- `recognize(frame, context)` returns raw recognition candidates.
- `fingerprint(frame, context)` returns a scroll-end fingerprint.

That keeps `/api/recognition/scan`, external trigger URLs, pending suggestions, and UI review unchanged.

## Current Coverage

The first task can identify the IS#5 Sarkaz map-select header from OCR text and propose an `is5_sarkaz` run-status candidate. Real ADB frames still need an OCR/template backend before this can classify live screenshots directly.

## Next Steps

1. Add an OCR backend that converts ADB screenshot bytes into `ocrResults` for MAA-style tasks.
2. Add screen tasks for IS#5 map selection, run home, relic list, thought list, and operator list.
3. Add profile `openSteps` only after screen classification is stable.
4. Add template/icon matching for relics and operators before trusting name OCR.
5. Keep every result in `pendingSuggestions` until reviewed.

## Consequences

- The app can migrate toward MAA/MaaFramework without rewriting the UI.
- Tests can exercise recognition behavior with synthetic OCR frames before the OCR backend is ready.
- The task file becomes the main place to tune ROI and expected text.
- Until OCR/template matching is implemented, live ADB scans will still abort on unknown screens unless an adapter supplies OCR results.
