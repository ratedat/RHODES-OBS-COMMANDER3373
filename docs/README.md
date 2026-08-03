# Project Documentation

This directory is for public, project-facing documentation only. It should explain how RHODES OBS COMMANDER3373 works, how to operate it, and how future contributors should maintain it.

Do not place Codex/Stitch working notes, prompt handoff files, generated design drafts, or temporary preview artifacts here. Keep those in `.agent-work/`, which is intentionally ignored by Git.

## User Guides

- [Startup and OBS setup](guides/startup-guide.md)
- [ADB setup and troubleshooting](guides/adb-setup.md)
- [Debugger ADB/OCR report guide](guides/debugger-adb-report-guide.md)
- [Public-debug Discord guide](guides/discord-public-debug-guide.md)
- [Tournament remote input](guides/tournament-remote-input.md)
- [Output CSS customization](guides/output-css-customization.md)
- [Sarkaz recognition test guide](guides/sarkaz-test-guide.md)
- [GLM-OCR optional verification setup](guides/glm-ocr-setup.md)
- [PaddleOCR legacy notes](guides/paddle-ocr-setup.md)

## Technical Reference

- [Architecture](reference/architecture.md)
- [Data sources](reference/data-sources.md)
- [Campaign data coverage](reference/data-summary.md)
- [Effect calculation](reference/effect-calculation.md)
- [Recognition notes](reference/recognition-notes.md)
- [MAA OCR adoption](reference/maa-ocr-adoption.md)
- [MAA OCR research](reference/maa-ocr-research.md)
- [MAAFramework family roadmap](reference/maaframework-family-roadmap.md)
- [IS#6 coin OCR corpus](reference/sui-coin-ocr-corpus.md)

## Product Design

- [Control-v2 screen design](design/control-v2-screen-design.md)
- [Suki design philosophy](design/suki-design-philosophy-ja.md)
- [Product UI information architecture](design/suki-product-ui-information-architecture.md)
- [Suki workbench design principles](design/suki-workbench-design-principles.md)
- [Preview images](previews/)

## Decisions And Legal

- [ADR-0001: Adopt MAAFramework and SukiUI](decisions/0001-adopt-maaframework-and-sukiui.md)
- [License and source availability](legal/licenses.md)

## Internal Working Notes

Codex handoffs, consultation prompts, temporary implementation plans, and generated design briefs belong under `.agent-work/handoff/` and are not committed.
