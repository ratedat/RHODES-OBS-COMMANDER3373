import assert from "node:assert/strict";
import { readdir, readFile, stat } from "node:fs/promises";
import test from "node:test";

test("package exposes Suki/Avalonia as the only active desktop app", async () => {
  const pkg = JSON.parse(await readFile(new URL("../package.json", import.meta.url), "utf8"));

  assert.equal(pkg.main, "app/server.mjs");
  assert.equal(pkg.scripts["suki:build"], "dotnet build apps/rhodes-suki/RhodesSuki.csproj");
  assert.equal(pkg.scripts["suki:test"], "dotnet run --project tests/rhodes-suki/RhodesSuki.ServiceTests.csproj");
  assert.equal(pkg.scripts["suki:run"], "dotnet run --project apps/rhodes-suki/RhodesSuki.csproj");
  assert.equal(pkg.scripts["verify:desktop"], "npm run suki:check && npm run suki:build");
  assert.equal(Object.values(pkg.scripts).some((command) => command.includes("launcher.mjs")), false);

  const scriptNames = Object.keys(pkg.scripts);
  assert.equal(scriptNames.some((name) => name.startsWith("tauri:")), false);
  assert.equal(scriptNames.some((name) => [
    "app",
    "app:5174",
    "app:debug",
    "app:smoke",
    "app:web",
    "app:web:5174",
    "dist:win",
    "dist:debugger",
    "pack:win",
    "pack:debugger",
  ].includes(name)), false);
  assert.equal("build" in pkg, false);

  const devDependencies = pkg.devDependencies ?? {};
  assert.equal("@tauri-apps/cli" in devDependencies, false);
  assert.equal("electron" in devDependencies, false);
  assert.equal("electron-builder" in devDependencies, false);

  const lock = JSON.parse(await readFile(new URL("../package-lock.json", import.meta.url), "utf8"));
  const lockedPackages = new Set(Object.keys(lock.packages ?? {}).map((name) => name.replace(/^node_modules\//, "")));
  assert.equal(lockedPackages.has("@tauri-apps/cli"), false);
  assert.equal(lockedPackages.has("electron"), false);
  assert.equal(lockedPackages.has("electron-builder"), false);
  assert.equal([...lockedPackages].some((name) => name.startsWith("@tauri-apps/")), false);
});

test("retired Electron and Tauri project scaffolds are absent", async () => {
  await assert.rejects(stat(new URL("../src-tauri", import.meta.url)), { code: "ENOENT" });
  await assert.rejects(stat(new URL("../electron", import.meta.url)), { code: "ENOENT" });

  const entries = await readdir(new URL("../", import.meta.url), { withFileTypes: true });
  const activeTopLevel = entries
    .map((entry) => entry.name)
    .filter((name) => !["node_modules", "dist", "dist-debugger", "outputs"].includes(name));
  assert.equal(activeTopLevel.some((name) => /(?:electron|tauri)/iu.test(name)), false);
});

test("local web server does not advertise the retired Control shell as the default", async () => {
  const [server, localServer] = await Promise.all([
    readFile(new URL("../app/server.mjs", import.meta.url), "utf8"),
    readFile(new URL("../app/runtime/local-server.mjs", import.meta.url), "utf8"),
  ]);

  assert.doesNotMatch(server, /Control: http:\/\/\$\{host\}:\$\{actualPort\}\/control-v2/);
  assert.match(server, /Sidecar: http:\/\/\$\{host\}:\$\{actualPort\}\/sidecar/);
  assert.doesNotMatch(localServer, /view = "control-v2"/);
  assert.match(localServer, /view = "sidecar"/);
});

test("non-Control web pages do not link users back into the retired Control shell", async () => {
  const app = await readFile(new URL("../app/app.js", import.meta.url), "utf8");
  const licensesPage = app.slice(app.indexOf("function renderLicensesPage()"), app.indexOf("function renderInteractive()"));
  const sidecarPage = app.slice(app.indexOf("function renderSidecar()"), app.indexOf("function renderOverlayContext()"));

  assert.doesNotMatch(licensesPage, /href="\/control-v2/);
  assert.doesNotMatch(sidecarPage, /href="\/control-v2/);
  assert.doesNotMatch(sidecarPage, />Control</);
});

test("runtime view guards no longer treat retired Control as an active interactive shell", async () => {
  const [app, controlEvents] = await Promise.all([
    readFile(new URL("../app/app.js", import.meta.url), "utf8"),
    readFile(new URL("../app/control-events.js", import.meta.url), "utf8"),
  ]);

  assert.doesNotMatch(app, /view === "control-v2"/);
  assert.doesNotMatch(app, /return renderControlV2\(\)/);
  assert.doesNotMatch(controlEvents, /context\.view === "control-v2"/);
});

test("retired Control screen metadata module has been removed", async () => {
  await assert.rejects(
    readFile(new URL("../app/domain/control-v2-screens.js", import.meta.url), "utf8"),
    { code: "ENOENT" },
  );
});

test("runtime event layer no longer handles retired Control screen switching actions", async () => {
  const controlEvents = await readFile(new URL("../app/control-events.js", import.meta.url), "utf8");

  assert.doesNotMatch(controlEvents, /action === "control-v2-screen"/);
  assert.doesNotMatch(controlEvents, /action === "control-v2-choice-tab"/);
});
