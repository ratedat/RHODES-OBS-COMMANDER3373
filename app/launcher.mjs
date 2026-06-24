import { spawn } from "node:child_process";
import http from "node:http";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");
const DEFAULT_PORT = 5173;

function readArg(name, fallback = null) {
  const index = process.argv.indexOf(name);
  if (index < 0) return fallback;
  return process.argv[index + 1] || fallback;
}

function hasFlag(name) {
  return process.argv.includes(name);
}

function normalizePort(value) {
  const port = Number(value);
  if (!Number.isInteger(port) || port <= 0 || port > 65535) return DEFAULT_PORT;
  return port;
}

function normalizeView(value) {
  if (value === "overlay") return "overlay";
  return "control";
}

function waitForReady(url, attempts = 60) {
  return new Promise((resolve, reject) => {
    let remaining = attempts;
    const probe = () => {
      const req = http.get(url, (res) => {
        res.resume();
        if (res.statusCode && res.statusCode < 500) return resolve();
        retry();
      });
      req.setTimeout(500, () => {
        req.destroy();
        retry();
      });
      req.on("error", retry);
    };
    const retry = () => {
      remaining -= 1;
      if (remaining <= 0) return reject(new Error(`Timed out waiting for ${url}`));
      setTimeout(probe, 250);
    };
    probe();
  });
}

function openExternal(url) {
  const platform = process.platform;
  const command = platform === "win32" ? "cmd.exe" : platform === "darwin" ? "open" : "xdg-open";
  const args = platform === "win32" ? ["/c", "start", "", url] : [url];
  const opener = spawn(command, args, { detached: true, stdio: "ignore" });
  opener.unref();
}

const port = normalizePort(readArg("--port", process.env.PORT || DEFAULT_PORT));
const view = normalizeView(readArg("--view", "control"));
const noOpen = hasFlag("--no-open") || process.env.ARKNIGHTS_APP_NO_OPEN === "1";
const exitAfterReady = hasFlag("--exit-after-ready");
const targetUrl = `http://127.0.0.1:${port}/${view}`;

const server = spawn(process.execPath, [path.join(__dirname, "server.mjs"), "--port", String(port)], {
  cwd: ROOT,
  env: { ...process.env, PORT: String(port) },
  stdio: ["ignore", "pipe", "pipe"],
});

server.stdout.on("data", (chunk) => process.stdout.write(chunk));
server.stderr.on("data", (chunk) => process.stderr.write(chunk));

let shuttingDown = false;
function shutdown(code = 0) {
  if (shuttingDown) return;
  shuttingDown = true;
  if (!server.killed) server.kill();
  setTimeout(() => process.exit(code), 150).unref();
}

process.on("SIGINT", () => shutdown(0));
process.on("SIGTERM", () => shutdown(0));
server.on("exit", (code) => {
  if (!shuttingDown) process.exit(code ?? 1);
});

try {
  await waitForReady(targetUrl);
  console.log(`App: ${targetUrl}`);
  if (!noOpen) openExternal(targetUrl);
  if (exitAfterReady) shutdown(0);
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  shutdown(1);
}