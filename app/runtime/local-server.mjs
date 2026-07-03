import http from "node:http";

export const DEFAULT_PORT = 5173;

export function readArg(args, name, fallback = null) {
  const index = args.indexOf(name);
  if (index < 0) return fallback;
  return args[index + 1] || fallback;
}

export function normalizePort(value) {
  const port = Number(value);
  if (!Number.isInteger(port) || port <= 0 || port > 65535) return DEFAULT_PORT;
  return port;
}

export function normalizeView(value) {
  if (value === "overlay") return "overlay";
  if (value === "sidecar") return "sidecar";
  if (value === "licenses") return "licenses";
  return "control-v2";
}

export function appUrl(port, view = "control-v2") {
  return `http://127.0.0.1:${port}/${normalizeView(view)}`;
}

export function overlayUrl(port, query = "") {
  return `http://127.0.0.1:${port}/overlay${query}`;
}

export function overlayPartUrl(port, part) {
  return `http://127.0.0.1:${port}/overlay/part/${part}`;
}

export function waitForReady(url, attempts = 60) {
  return new Promise((resolve, reject) => {
    let remaining = attempts;
    const retry = () => {
      remaining -= 1;
      if (remaining <= 0) return reject(new Error(`Timed out waiting for ${url}`));
      setTimeout(probe, 250);
    };
    const probe = () => {
      const req = http.get(url, (res) => {
        res.resume();
        if (res.statusCode && res.statusCode >= 200 && res.statusCode < 400) return resolve();
        retry();
      });
      req.setTimeout(500, () => {
        req.destroy();
        retry();
      });
      req.on("error", retry);
    };
    probe();
  });
}

export async function isLocalServerReady(url, attempts = 2) {
  try {
    await waitForReady(url, attempts);
    return true;
  } catch {
    return false;
  }
}
