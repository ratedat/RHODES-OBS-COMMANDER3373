import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

test("package exposes Suki/Avalonia as the only active desktop shell", async () => {
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
