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

Build command:

```bash
npm run tauri:build
```

`tauri:build` first runs `tauri:prepare`, which creates `src-tauri/resources/` from the committed app, assets, clean data files, docs, and the current Node executable. The generated resource directory is ignored by git. It deliberately excludes local runtime state such as `data/current-state.json`, Electron cache files, ADB work folders, GLM-OCR runtime files, and Ollama runtime files.

Rust prerequisites are required before the command can run. The Node server can be pointed at another repository root with `RHODES_APP_ROOT`, another Node binary with `RHODES_NODE_BIN`, and another port with `-- --port 5174`.

The first successful Windows Tauri package with bundled Node resources was about 56 MB as an NSIS installer. This is still far smaller than the Electron portable build and keeps GLM-OCR/Ollama as optional runtime downloads.

Installer-free portable ZIP packaging is available after a successful Tauri build:

```bash
npm run tauri:package:portable
```

The portable ZIP is written under `outputs/release/`. It contains `RHODES OBS COMMANDER3373.exe`, the required `resources/` folder, license files, and a short portable README. Users can extract the ZIP and run the EXE without starting an installer. GLM-OCR and Ollama remain optional runtime downloads under the portable data folder.

Rust-side storage tests can be run with:

```bash
npm run tauri:test
```

The Tauri shell now mirrors the Electron portable storage contract in `src-tauri/src/storage.rs`: development state stays under `user-data/state`, packaged portable state goes beside the executable under `RHODES OBS COMMANDER3373 Data/state`, and `PORTABLE_EXECUTABLE_FILE` / `ARKNIGHTS_STATE_DIR` overrides are honored.

The first desktop IPC command is `rhodes_storage_target`. It returns the resolved app root, portable storage directory, state directory, and packaged/development mode from Rust. Control v2 reads it through `app/lib/tauri-bridge.js` when `window.__TAURI__` is available and shows the result on the OBS settings screen. Electron and normal browser sessions do not render this panel.

## Next slices

1. Move portable storage selection UI actions away from Electron-specific code.
2. Move desktop browse/open-dialog actions from Electron IPC to Tauri commands.
3. Move small desktop-only actions from Electron menus to Tauri commands.
4. Keep GLM-OCR and Ollama as optional runtime downloads under `RHODES OBS COMMANDER3373 Data/state`.
