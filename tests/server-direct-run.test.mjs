import test from "node:test";
import assert from "node:assert/strict";
import { fileURLToPath, pathToFileURL } from "node:url";
import path from "node:path";
import { isDirectServerRun } from "../app/server.mjs";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

test("server direct run accepts Windows namespace paths from packaged Suki launchers", () => {
  const serverPath = path.join(ROOT, "app", "server.mjs");
  const argvPath = process.platform === "win32" ? `\\\\?\\${serverPath}` : serverPath;
  assert.equal(isDirectServerRun(argvPath, pathToFileURL(serverPath).href), true);
});
