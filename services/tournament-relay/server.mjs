import http from "node:http";
import fs from "node:fs/promises";
import { createReadStream } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createTournamentRelaySessionStore } from "./session-store.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PUBLIC_DIR = path.join(__dirname, "public");
const DEFAULT_MAX_BODY_BYTES = 8 * 1024 * 1024;
const NO_CACHE_HEADERS = {
  "cache-control": "no-store, max-age=0, must-revalidate",
  pragma: "no-cache",
  expires: "0",
};
const MIME = new Map([
  [".html", "text/html; charset=utf-8"],
  [".js", "text/javascript; charset=utf-8"],
  [".css", "text/css; charset=utf-8"],
  [".svg", "image/svg+xml"],
  [".png", "image/png"],
]);

function relayHttpError(message, status = 400, code = "relay_error") {
  return Object.assign(new Error(message), { status, code });
}

async function readJsonBody(req, maxBodyBytes) {
  const chunks = [];
  let size = 0;
  for await (const chunk of req) {
    size += chunk.length;
    if (size > maxBodyBytes) throw relayHttpError("リクエストが大きすぎます。", 413, "payload_too_large");
    chunks.push(chunk);
  }
  if (!chunks.length) return {};
  try {
    return JSON.parse(Buffer.concat(chunks).toString("utf8"));
  } catch {
    throw relayHttpError("JSON形式が不正です。", 400, "invalid_json");
  }
}

function sendJson(res, status, value) {
  res.writeHead(status, {
    "content-type": "application/json; charset=utf-8",
    ...NO_CACHE_HEADERS,
  });
  res.end(JSON.stringify(value));
}

function sendText(res, status, value) {
  res.writeHead(status, { "content-type": "text/plain; charset=utf-8", ...NO_CACHE_HEADERS });
  res.end(value);
}

function bearerToken(req) {
  const value = String(req.headers.authorization || "");
  return value.startsWith("Bearer ") ? value.slice(7).trim() : "";
}

function editorCode(req, url) {
  return String(req.headers["x-editor-code"] || url.searchParams.get("code") || "").trim().toUpperCase();
}

function safePublicFile(relativePath) {
  const file = path.resolve(PUBLIC_DIR, relativePath.replace(/^[/\\]+/, ""));
  const relative = path.relative(PUBLIC_DIR, file);
  return relative && !relative.startsWith("..") && !path.isAbsolute(relative) ? file : null;
}

async function servePublicFile(res, relativePath) {
  const file = safePublicFile(relativePath);
  if (!file) return false;
  try {
    const stat = await fs.stat(file);
    if (!stat.isFile()) return false;
    res.writeHead(200, {
      "content-type": MIME.get(path.extname(file).toLowerCase()) || "application/octet-stream",
      ...NO_CACHE_HEADERS,
    });
    createReadStream(file).pipe(res);
    return true;
  } catch {
    return false;
  }
}

function sessionRoute(pathname) {
  const match = pathname.match(/^\/api\/sessions\/([^/]+)(?:\/(.*))?$/);
  if (!match) return null;
  return {
    sessionId: decodeURIComponent(match[1]),
    action: match[2] || "",
  };
}

export function createTournamentRelayServer({
  store = createTournamentRelaySessionStore({
    publicBaseUrl: process.env.TOURNAMENT_RELAY_PUBLIC_URL || "",
  }),
  adminToken = process.env.TOURNAMENT_RELAY_ADMIN_TOKEN || "",
  maxBodyBytes = DEFAULT_MAX_BODY_BYTES,
} = {}) {
  return http.createServer(async (req, res) => {
    try {
      const url = new URL(req.url || "/", `http://${req.headers.host || "localhost"}`);

      if (req.method === "GET" && url.pathname === "/api/health") {
        return sendJson(res, 200, { ok: true, service: "rhodes-tournament-relay" });
      }

      if (req.method === "POST" && url.pathname === "/api/sessions") {
        if (adminToken && String(req.headers["x-admin-token"] || "") !== adminToken) {
          throw relayHttpError("中継サーバーの管理認証に失敗しました。", 401, "admin_auth_failed");
        }
        const created = store.createSession(await readJsonBody(req, maxBodyBytes));
        return sendJson(res, 201, created);
      }

      const route = sessionRoute(url.pathname);
      if (route) {
        const { sessionId, action } = route;
        const hostToken = bearerToken(req);

        if (req.method === "PUT" && action === "snapshot") {
          const body = await readJsonBody(req, maxBodyBytes);
          return sendJson(res, 200, store.setSnapshot(sessionId, hostToken, body.snapshot));
        }

        if (req.method === "GET" && action === "bootstrap") {
          return sendJson(res, 200, store.getEditorBootstrap(sessionId, editorCode(req, url)));
        }

        if (req.method === "GET" && action === "operations") {
          return sendJson(res, 200, {
            operations: store.listOperations(sessionId, hostToken, {
              after: url.searchParams.get("after") || 0,
            }),
          });
        }

        if (req.method === "POST" && action === "operations") {
          const body = await readJsonBody(req, maxBodyBytes);
          return sendJson(res, 202, store.enqueueOperation(sessionId, editorCode(req, url), body.operation));
        }

        const resolveMatch = action.match(/^operations\/([^/]+)\/result$/);
        if (req.method === "POST" && resolveMatch) {
          return sendJson(
            res,
            200,
            store.resolveOperation(
              sessionId,
              hostToken,
              decodeURIComponent(resolveMatch[1]),
              await readJsonBody(req, maxBodyBytes),
            ),
          );
        }

        if (req.method === "DELETE" && !action) {
          store.closeSession(sessionId, hostToken);
          res.writeHead(204, NO_CACHE_HEADERS);
          return res.end();
        }
      }

      if (req.method === "GET" && /^\/input\/[^/]+$/.test(url.pathname)) {
        if (await servePublicFile(res, "index.html")) return;
      }
      if (req.method === "GET" && url.pathname.startsWith("/assets/")) {
        if (await servePublicFile(res, url.pathname.slice("/assets/".length))) return;
      }
      if (req.method === "GET" && url.pathname === "/") {
        return sendText(res, 200, "RHODES OBS COMMANDER3373 Tournament Relay");
      }

      sendText(res, 404, "Not found");
    } catch (error) {
      sendJson(res, Number(error?.status) || 500, {
        error: error instanceof Error ? error.message : String(error),
        code: error?.code || "relay_error",
      });
    }
  });
}

export function startTournamentRelayServer({
  host = process.env.TOURNAMENT_RELAY_HOST || "127.0.0.1",
  port = Number(process.env.TOURNAMENT_RELAY_PORT || process.env.PORT || 5180),
  ...options
} = {}) {
  const normalizedHost = String(host || "").trim().toLowerCase().replace(/^\[|\]$/g, "");
  const loopback = normalizedHost === "localhost"
    || normalizedHost === "::1"
    || /^127(?:\.\d{1,3}){3}$/.test(normalizedHost);
  const adminToken = String(options.adminToken ?? process.env.TOURNAMENT_RELAY_ADMIN_TOKEN ?? "");
  if (!loopback && !adminToken) {
    return Promise.reject(new Error(
      "外部公開する中継サーバーにはTOURNAMENT_RELAY_ADMIN_TOKENが必要です。",
    ));
  }
  const server = createTournamentRelayServer({ ...options, adminToken });
  return new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(port, host, () => {
      server.off("error", reject);
      const address = server.address();
      const actualPort = typeof address === "object" && address ? address.port : port;
      console.log(`RHODES Tournament Relay: http://${host}:${actualPort}`);
      resolve({ server, host, port: actualPort });
    });
  });
}

if (path.resolve(process.argv[1] || "") === fileURLToPath(import.meta.url)) {
  startTournamentRelayServer().catch((error) => {
    console.error(error instanceof Error ? error.message : String(error));
    process.exit(1);
  });
}
