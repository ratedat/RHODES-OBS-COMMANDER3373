import { createHash, randomBytes } from "node:crypto";
import { spawn } from "node:child_process";
import fs from "node:fs/promises";
import path from "node:path";

import { startTournamentRelayServer } from "../../services/tournament-relay/server.mjs";

export const CLOUDFLARED_VERSION = "2026.7.2";
export const CLOUDFLARED_WINDOWS_AMD64_URL =
  `https://github.com/cloudflare/cloudflared/releases/download/${CLOUDFLARED_VERSION}/cloudflared-windows-amd64.exe`;
export const CLOUDFLARED_WINDOWS_AMD64_SHA256 =
  "cdb5d4432f6ae1595654a692a51308b69d2bf7af961f5578d9391837cf072df9";

const QUICK_TUNNEL_URL_PATTERN = /https:\/\/[a-z0-9-]+\.trycloudflare\.com\b/i;
const MAX_DOWNLOAD_BYTES = 80 * 1024 * 1024;
const MAX_DIAGNOSTIC_CHARS = 2_000;

function compactDiagnostic(value) {
  return String(value || "")
    .replace(/\u001b\[[0-9;]*m/g, "")
    .replace(/\r/g, "")
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean)
    .slice(-8)
    .join(" | ")
    .slice(-MAX_DIAGNOSTIC_CHARS);
}

function closeServer(server) {
  if (!server?.listening) return Promise.resolve();
  return new Promise((resolve) => server.close(() => resolve()));
}

function waitForProcessExit(processHandle, timeoutMs = 3_000) {
  if (!processHandle || processHandle.exitCode !== null) return Promise.resolve();
  return new Promise((resolve) => {
    let settled = false;
    const finish = () => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      resolve();
    };
    const timer = setTimeout(finish, timeoutMs);
    processHandle.once("exit", finish);
  });
}

function normalizeLabel(value) {
  const text = String(value || "").trim();
  return text ? text.slice(0, 80) : "Player";
}

export function createTournamentQuickPublishManager({
  runtimeRoot,
  remoteHost,
  relayStarter = startTournamentRelayServer,
  fetchImpl = globalThis.fetch,
  spawnImpl = spawn,
  platform = process.platform,
  startupTimeoutMs = 30_000,
  publicRouteReadyTimeoutMs = 75_000,
  publicRouteProbeTimeoutMs = 5_000,
  publicRouteRetryDelayMs = 1_000,
  delayImpl = (durationMs) => new Promise((resolve) => setTimeout(resolve, durationMs)),
  downloadUrl = CLOUDFLARED_WINDOWS_AMD64_URL,
  expectedSha256 = CLOUDFLARED_WINDOWS_AMD64_SHA256,
} = {}) {
  if (!runtimeRoot) throw new Error("cloudflared runtime root is required.");
  if (!remoteHost) throw new Error("tournament remote host is required.");

  const executablePath = path.join(path.resolve(runtimeRoot), "cloudflared.exe");
  let relay = null;
  let tunnelProcess = null;
  let publicUrl = "";
  let starting = false;
  let lastError = "";
  let stage = "idle";
  let diagnostic = "";
  let lastTunnelOutput = "";
  let installPromise = null;

  async function isInstalled() {
    try {
      const stat = await fs.stat(executablePath);
      return stat.isFile() && stat.size > 0;
    } catch {
      return false;
    }
  }

  async function status() {
    return {
      installed: await isInstalled(),
      version: CLOUDFLARED_VERSION,
      runtimePath: executablePath,
      active: Boolean(tunnelProcess && publicUrl && relay?.server?.listening && remoteHost.status().active),
      starting,
      publicUrl,
      localRelayUrl: relay ? `http://127.0.0.1:${relay.port}` : "",
      lastError,
      stage,
      diagnostic,
      remote: remoteHost.status(),
    };
  }

  async function install() {
    if (await isInstalled()) {
      if (!starting) stage = "ready";
      return status();
    }
    if (installPromise) return installPromise;
    if (platform !== "win32") {
      throw new Error("簡易公開の自動導入はWindows版だけに対応しています。");
    }

    stage = "installing";
    diagnostic = "簡易公開ランタイムを検証しています。";
    installPromise = (async () => {
      const runtimeDirectory = path.dirname(executablePath);
      const temporaryPath = `${executablePath}.download`;
      await fs.mkdir(runtimeDirectory, { recursive: true });
      await fs.rm(temporaryPath, { force: true });

      const response = await fetchImpl(downloadUrl, {
        headers: { "user-agent": "RHODES-OBS-COMMANDER3373" },
        redirect: "follow",
      });
      if (!response.ok) {
        throw new Error(`cloudflaredのダウンロードに失敗しました (HTTP ${response.status})。`);
      }
      const declaredLength = Number(response.headers.get("content-length") || 0);
      if (declaredLength > MAX_DOWNLOAD_BYTES) {
        throw new Error("cloudflaredのダウンロードサイズが上限を超えています。");
      }

      const bytes = Buffer.from(await response.arrayBuffer());
      if (!bytes.length || bytes.length > MAX_DOWNLOAD_BYTES) {
        throw new Error("cloudflaredのダウンロード内容が不正です。");
      }
      const actualSha256 = createHash("sha256").update(bytes).digest("hex");
      if (actualSha256 !== expectedSha256.toLowerCase()) {
        throw new Error("cloudflaredのSHA-256検証に失敗しました。ファイルは導入していません。");
      }

      await fs.writeFile(temporaryPath, bytes, { flag: "wx" });
      await fs.rename(temporaryPath, executablePath);
      lastError = "";
      stage = "ready";
      diagnostic = "簡易公開ランタイムの準備が完了しました。";
      return status();
    })().catch(async (error) => {
      await fs.rm(`${executablePath}.download`, { force: true }).catch(() => {});
      lastError = error instanceof Error ? error.message : String(error);
      stage = "failed";
      diagnostic = lastError;
      throw error;
    }).finally(() => {
      installPromise = null;
    });

    return installPromise;
  }

  async function stopTunnelProcess() {
    const processHandle = tunnelProcess;
    tunnelProcess = null;
    publicUrl = "";
    if (!processHandle) return;
    try {
      processHandle.kill();
    } catch {
      // The process may already have exited.
    }
    await waitForProcessExit(processHandle);
    if (processHandle.exitCode === null) {
      try {
        processHandle.kill("SIGKILL");
      } catch {
        // Best effort; the owned process is already unavailable.
      }
    }
  }

  async function stop() {
    starting = false;
    await remoteHost.stop().catch(() => {});
    await stopTunnelProcess();
    const relayServer = relay?.server;
    relay = null;
    await closeServer(relayServer);
    stage = "idle";
    diagnostic = "";
    lastTunnelOutput = "";
    return status();
  }

  function waitForPublicUrl(processHandle) {
    return new Promise((resolve, reject) => {
      let output = "";
      let settled = false;
      const finish = (error, value = "") => {
        if (settled) return;
        settled = true;
        clearTimeout(timer);
        processHandle.off("exit", onExit);
        processHandle.off("error", onError);
        processHandle.stdout?.off("data", inspect);
        processHandle.stderr?.off("data", inspect);
        if (error) reject(error);
        else resolve(value);
      };
      const inspect = (chunk) => {
        output = `${output}${String(chunk || "")}`.slice(-16_000);
        lastTunnelOutput = output;
        diagnostic = compactDiagnostic(output);
        const match = output.match(QUICK_TUNNEL_URL_PATTERN);
        if (match) finish(null, match[0]);
      };
      const onExit = (code) => finish(new Error(
        `cloudflaredが公開URLを発行する前に終了しました (exit ${code ?? "unknown"})。`
        + (compactDiagnostic(output) ? ` 出力: ${compactDiagnostic(output)}` : ""),
      ));
      const onError = (error) => finish(new Error(
        `cloudflaredを起動できませんでした: ${error instanceof Error ? error.message : String(error)}`,
      ));
      const timer = setTimeout(() => finish(new Error(
        "cloudflaredの公開URL発行が30秒以内に完了しませんでした。"
        + (compactDiagnostic(output) ? ` 出力: ${compactDiagnostic(output)}` : ""),
      )), startupTimeoutMs);
      processHandle.stdout?.on("data", inspect);
      processHandle.stderr?.on("data", inspect);
      processHandle.once("exit", onExit);
      processHandle.once("error", onError);
    });
  }

  async function waitForPublicRouteReady(baseUrl, processHandle) {
    const deadline = Date.now() + publicRouteReadyTimeoutMs;
    let lastProbeResult = "未確認";

    while (Date.now() < deadline) {
      if (processHandle.exitCode !== null) {
        throw new Error(
          `cloudflaredが公開経路の準備中に終了しました (exit ${processHandle.exitCode ?? "unknown"})。`,
        );
      }

      const remainingMs = Math.max(1, deadline - Date.now());
      try {
        const response = await fetchImpl(`${baseUrl}/api/health`, {
          headers: {
            accept: "application/json",
            "user-agent": "RHODES-OBS-COMMANDER3373",
          },
          redirect: "follow",
          signal: AbortSignal.timeout(Math.min(publicRouteProbeTimeoutMs, remainingMs)),
        });
        if (response.ok) return;
        lastProbeResult = `HTTP ${response.status}`;
      } catch (error) {
        const causeCode = error?.cause?.code;
        lastProbeResult = causeCode
          ? `${causeCode}: ${error instanceof Error ? error.message : String(error)}`
          : error instanceof Error
            ? error.message
            : String(error);
      }

      const retryRemainingMs = deadline - Date.now();
      if (retryRemainingMs <= 0) break;
      await delayImpl(Math.min(publicRouteRetryDelayMs, retryRemainingMs));
    }

    throw new Error(
      `cloudflaredの公開経路が${Math.ceil(publicRouteReadyTimeoutMs / 1_000)}秒以内に利用可能になりませんでした。`
      + ` 最後の確認: ${lastProbeResult}`,
    );
  }

  async function start({ playerLabel = "Player" } = {}) {
    if (starting) throw new Error("簡易公開は開始処理中です。");
    starting = true;
    lastError = "";
    stage = "installing";
    diagnostic = "同梱ランタイムを確認しています。";
    lastTunnelOutput = "";
    try {
      await stop();
      starting = true;
      stage = "installing";
      diagnostic = "同梱ランタイムを確認しています。";
      await install();

      stage = "starting-relay";
      diagnostic = "入力用のローカル中継を起動しています。";
      const adminToken = randomBytes(32).toString("base64url");
      relay = await relayStarter({
        host: "127.0.0.1",
        port: 0,
        adminToken,
      });
      const localRelayUrl = `http://127.0.0.1:${relay.port}`;
      const isolatedHome = path.dirname(executablePath);
      stage = "starting-tunnel";
      diagnostic = "Cloudflareへ一時公開URLを要求しています。";
      tunnelProcess = spawnImpl(executablePath, [
        "tunnel",
        "--no-autoupdate",
        "--url",
        localRelayUrl,
      ], {
        cwd: isolatedHome,
        env: {
          ...process.env,
          HOME: isolatedHome,
          USERPROFILE: isolatedHome,
        },
        windowsHide: true,
        stdio: ["ignore", "pipe", "pipe"],
      });
      publicUrl = await waitForPublicUrl(tunnelProcess);
      stage = "waiting-route";
      diagnostic = `一時公開URLの利用開始を待っています: ${publicUrl}`;
      await waitForPublicRouteReady(publicUrl, tunnelProcess);
      stage = "creating-session";
      diagnostic = "入力担当者用セッションを作成しています。";
      await remoteHost.start({
        relayUrl: publicUrl,
        playerLabel: normalizeLabel(playerLabel),
        adminToken,
      });
      starting = false;
      stage = "active";
      diagnostic = "一時公開URLと入力セッションを利用できます。";
      return status();
    } catch (error) {
      lastError = error instanceof Error ? error.message : String(error);
      const failureDiagnostic = compactDiagnostic(lastTunnelOutput) || lastError;
      await stop();
      lastError = error instanceof Error ? error.message : String(error);
      stage = "failed";
      diagnostic = failureDiagnostic;
      throw error;
    } finally {
      starting = false;
    }
  }

  async function uninstall() {
    await stop();
    const resolvedRuntimeRoot = path.resolve(runtimeRoot);
    if (path.dirname(executablePath) !== resolvedRuntimeRoot) {
      throw new Error("cloudflared runtime pathの安全確認に失敗しました。");
    }
    await fs.rm(resolvedRuntimeRoot, { recursive: true, force: true });
    lastError = "";
    stage = "idle";
    diagnostic = "";
    return status();
  }

  return {
    status,
    install,
    start,
    stop,
    uninstall,
  };
}
