# Project Documentation

This directory is for public, project-facing documentation only. It should explain how RHODES OBS COMMANDER3373 works, how to operate it, and how future contributors should maintain it.

Do not place Codex/Stitch working notes, prompt handoff files, generated design drafts, or temporary preview artifacts here. Keep those in `.agent-work/`, which is intentionally ignored by Git.

## Key Documents

- `startup-guide.md` - app startup, OBS URLs, port usage, and ADB setup notes
- `adb-setup.md` - supported ADB presets, emulator setup, Hyper-V diagnostics, and troubleshooting
- `licenses.md` - license, source availability, and third-party attribution policy
- `architecture.md` - state and overlay architecture
- `data-sources.md` - source extraction notes
- `data-summary.md` - extracted campaign data coverage
- `effect-calculation.md` - relic/squad effect calculation design
- `maa-ocr-research.md` - ADB/OCR research notes that are useful to the implementation
- `recognition-notes.md` - recognition implementation notes
- `recognition-maa-migration.md` - MAA-style recognition seam and migration plan

- [PaddleOCR optional setup](paddle-ocr-setup.md)
- [ONNXRuntime / FastDeploy OCR setup](onnxruntime-fastdeploy-setup.md)

- [MAA OCR adoption](maa-ocr-adoption.md)
