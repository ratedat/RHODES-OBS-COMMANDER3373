# Archived Tauri Migration Notes

Tauri migration work is archived. RHODES OBS COMMANDER3373 now uses `apps/rhodes-suki` as the active desktop shell target:

- UI: Avalonia + SukiUI
- ADB / screenshot / recognition: MAAFramework
- OCR: MAA-OCR by default, optional GLM-OCR for local high-accuracy verification
- OBS integration: existing browser-source output remains RHODES-owned

The old Tauri package scripts and CLI dependency are intentionally removed from `package.json`. If a missing feature is found in the archived Tauri or Electron implementations, port the behavior into the Suki/Avalonia code path instead of re-enabling the old shell.
