# Tauri Migration Notes

This project is moving toward a lighter desktop shell where OBS remains the preview and broadcast surface.

## Target split

- Tauri/Rust: desktop shell, local server lifecycle, settings, ADB/OCR runtime management.
- Existing web UI: control, sidecar, and overlay views served over localhost.
- OBS: browser-source preview and production overlay rendering.
- OCR runtimes: optional downloads under the portable state directory, not part of the normal app binary.

## Current slice

The first Tauri slice keeps the existing Node local server and starts it from Rust. This preserves the existing API and OBS URLs while Electron is still available as the stable release path.

Development command:

```bash
npm run tauri:dev
```

Rust prerequisites are required before the command can run. The Node server can be pointed at another repository root with `RHODES_APP_ROOT`, another Node binary with `RHODES_NODE_BIN`, and another port with `-- --port 5174`.

## Next slices

1. Move portable storage selection into a shared contract used by both Electron and Tauri.
2. Replace Node-server spawning with a bundled sidecar so packaged Tauri builds do not require system Node.
3. Move small desktop-only actions from Electron menus to Tauri commands.
4. Keep GLM-OCR and Ollama as optional runtime downloads under `RHODES OBS COMMANDER3373 Data/state`.
