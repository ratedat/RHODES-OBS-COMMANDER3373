import assert from "node:assert/strict";
import { access, readFile } from "node:fs/promises";
import test from "node:test";

test("package exposes Tauri development and build scripts", async () => {
  const pkg = JSON.parse(await readFile(new URL("../package.json", import.meta.url), "utf8"));
  assert.equal(pkg.scripts["tauri:prepare"], "node tools/prepare-tauri-resources.mjs");
  assert.equal(pkg.scripts["tauri:dev"], "tauri dev");
  assert.equal(pkg.scripts["tauri:build"], "npm run tauri:prepare && tauri build");
  assert.match(pkg.devDependencies["@tauri-apps/cli"], /^\^2\./);
});

test("Tauri config keeps the existing localhost control surface", async () => {
  const config = JSON.parse(await readFile(new URL("../src-tauri/tauri.conf.json", import.meta.url), "utf8"));
  assert.equal(config.identifier, "com.ratedat.rhodes.obs.commander3373");
  assert.equal(config.build.devUrl, "http://127.0.0.1:5173/control-v2");
  assert.equal(config.build.frontendDist, "../app");
  assert.deepEqual(config.app.windows, []);
  assert.deepEqual(config.bundle.resources, ["resources/rhodes-app", "resources/bin"]);
});

test("Tauri Rust shell starts the existing local server before opening the main window", async () => {
  const source = await readFile(new URL("../src-tauri/src/main.rs", import.meta.url), "utf8");
  assert.match(source, /start_node_server/);
  assert.match(source, /app[\\"]\)\.join\("server\.mjs"\)/);
  assert.match(source, /wait_for_server\(port, Duration::from_secs\(12\)\)/);
  assert.match(source, /WebviewWindowBuilder::new\(app, "main", WebviewUrl::External\(url\)\)/);
  assert.match(source, /ARKNIGHTS_STATE_DIR/);
  assert.match(source, /resource_dir\(\)/);
  assert.match(source, /node-\{triple\}/);
});

test("Tauri default capability is restricted to the main window core permissions", async () => {
  const capability = JSON.parse(await readFile(new URL("../src-tauri/capabilities/default.json", import.meta.url), "utf8"));
  assert.deepEqual(capability.windows, ["main"]);
  assert.deepEqual(capability.permissions, ["core:default"]);
});

test("Tauri Windows resource icon is present for cargo checks and bundles", async () => {
  await access(new URL("../src-tauri/icons/icon.ico", import.meta.url));
});

test("Tauri resource preparation copies runtime assets without user state", async () => {
  const source = await readFile(new URL("../tools/prepare-tauri-resources.mjs", import.meta.url), "utf8");
  assert.match(source, /process\.execPath/);
  assert.match(source, /node-\$\{triple\}/);
  assert.match(source, /overlay-state\.example\.json/);
  assert.doesNotMatch(source, /current-state\.json/);
  assert.doesNotMatch(source, /dataFiles = \[[\s\S]*electron/);
});
