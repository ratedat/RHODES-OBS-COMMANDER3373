import test from "node:test";
import assert from "node:assert/strict";
import { EventEmitter } from "node:events";
import { PassThrough } from "node:stream";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";

import { createTournamentQuickPublishManager } from "../app/domain/tournament-quick-publish.js";

class FakeTunnelProcess extends EventEmitter {
  constructor() {
    super();
    this.stdout = new PassThrough();
    this.stderr = new PassThrough();
    this.exitCode = null;
  }

  kill() {
    if (this.exitCode !== null) return true;
    this.exitCode = 0;
    queueMicrotask(() => this.emit("exit", 0));
    return true;
  }
}

function fakeRemoteHost() {
  let active = false;
  let current = {
    active: false,
    inputUrl: "",
    editorCode: "",
  };
  const calls = [];
  return {
    calls,
    status() {
      return current;
    },
    async start(options) {
      calls.push(["start", options]);
      active = true;
      current = {
        active,
        inputUrl: `${options.relayUrl}/input/session-001?code=ABC123`,
        editorCode: "ABC123",
      };
      return current;
    },
    async stop() {
      calls.push(["stop"]);
      active = false;
      current = {
        active,
        inputUrl: "",
        editorCode: "",
      };
      return current;
    },
  };
}

test("quick publish owns the relay and tunnel without exposing the admin token", async () => {
  const runtimeRoot = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-quick-publish-"));
  const executablePath = path.join(runtimeRoot, "cloudflared.exe");
  await fs.writeFile(executablePath, "test executable");

  const remoteHost = fakeRemoteHost();
  let relayClosed = false;
  const relayServer = {
    listening: true,
    close(callback) {
      this.listening = false;
      relayClosed = true;
      callback();
    },
  };
  const processHandle = new FakeTunnelProcess();
  let spawnCall = null;
  const manager = createTournamentQuickPublishManager({
    runtimeRoot,
    remoteHost,
    platform: "win32",
    fetchImpl: async () => ({
      ok: true,
      status: 200,
    }),
    relayStarter: async (options) => {
      assert.equal(options.host, "127.0.0.1");
      assert.equal(options.port, 0);
      assert.ok(options.adminToken.length >= 32);
      return { server: relayServer, host: "127.0.0.1", port: 31999 };
    },
    spawnImpl: (command, args, options) => {
      spawnCall = { command, args, options };
      queueMicrotask(() => {
        processHandle.stderr.write("INF Your quick Tunnel has been created! https://sample.trycloudflare.com");
      });
      return processHandle;
    },
  });

  try {
    const started = await manager.start({ playerLabel: "Player A" });
    assert.equal(started.active, true);
    assert.equal(started.stage, "active");
    assert.match(started.diagnostic, /利用できます/);
    assert.equal(started.publicUrl, "https://sample.trycloudflare.com");
    assert.equal(started.localRelayUrl, "http://127.0.0.1:31999");
    assert.equal("adminToken" in started, false);
    assert.equal("adminToken" in started.remote, false);
    assert.equal(spawnCall.command, executablePath);
    assert.deepEqual(spawnCall.args, [
      "tunnel",
      "--no-autoupdate",
      "--url",
      "http://127.0.0.1:31999",
    ]);
    assert.equal(spawnCall.options.windowsHide, true);

    const startCall = remoteHost.calls.find(([name]) => name === "start");
    assert.equal(startCall[1].relayUrl, "https://sample.trycloudflare.com");
    assert.equal(startCall[1].playerLabel, "Player A");
    assert.ok(startCall[1].adminToken.length >= 32);

    const stopped = await manager.stop();
    assert.equal(stopped.active, false);
    assert.equal(stopped.stage, "idle");
    assert.equal(stopped.publicUrl, "");
    assert.equal(relayClosed, true);
    assert.equal(processHandle.exitCode, 0);
  } finally {
    await manager.stop();
    await fs.rm(runtimeRoot, { recursive: true, force: true });
  }
});

test("quick publish waits through transient public route failures before registering the host", async () => {
  const runtimeRoot = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-quick-ready-"));
  await fs.writeFile(path.join(runtimeRoot, "cloudflared.exe"), "test executable");

  const remoteHost = fakeRemoteHost();
  const processHandle = new FakeTunnelProcess();
  const probeResults = [
    Object.assign(new Error("fetch failed"), { cause: { code: "ENOTFOUND" } }),
    { ok: false, status: 530 },
    { ok: true, status: 200 },
  ];
  let probeCount = 0;
  const manager = createTournamentQuickPublishManager({
    runtimeRoot,
    remoteHost,
    platform: "win32",
    publicRouteReadyTimeoutMs: 5_000,
    publicRouteRetryDelayMs: 1,
    delayImpl: async () => {},
    fetchImpl: async (url) => {
      assert.equal(url, "https://warming-up.trycloudflare.com/api/health");
      const result = probeResults[probeCount++];
      if (result instanceof Error) throw result;
      return result;
    },
    relayStarter: async () => ({
      server: {
        listening: true,
        close(callback) {
          this.listening = false;
          callback();
        },
      },
      host: "127.0.0.1",
      port: 32000,
    }),
    spawnImpl: () => {
      queueMicrotask(() => {
        processHandle.stderr.write(
          "INF Your quick Tunnel has been created! https://warming-up.trycloudflare.com",
        );
      });
      return processHandle;
    },
  });

  try {
    const started = await manager.start({ playerLabel: "Player B" });
    assert.equal(started.active, true);
    assert.equal(started.stage, "active");
    assert.equal(probeCount, 3);
    const startCall = remoteHost.calls.find(([name]) => name === "start");
    assert.equal(startCall[1].relayUrl, "https://warming-up.trycloudflare.com");
  } finally {
    await manager.stop();
    await fs.rm(runtimeRoot, { recursive: true, force: true });
  }
});

test("quick publish preserves the failed stage and cloudflared output for support", async () => {
  const runtimeRoot = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-quick-failure-"));
  await fs.writeFile(path.join(runtimeRoot, "cloudflared.exe"), "test executable");

  const remoteHost = fakeRemoteHost();
  const processHandle = new FakeTunnelProcess();
  const manager = createTournamentQuickPublishManager({
    runtimeRoot,
    remoteHost,
    platform: "win32",
    relayStarter: async () => ({
      server: {
        listening: true,
        close(callback) {
          this.listening = false;
          callback();
        },
      },
      host: "127.0.0.1",
      port: 32001,
    }),
    spawnImpl: () => {
      queueMicrotask(() => {
        processHandle.stderr.write("ERR unable to reach Cloudflare edge");
        processHandle.exitCode = 1;
        processHandle.emit("exit", 1);
      });
      return processHandle;
    },
  });

  try {
    await assert.rejects(
      manager.start({ playerLabel: "Player C" }),
      /unable to reach Cloudflare edge/,
    );
    const failed = await manager.status();
    assert.equal(failed.active, false);
    assert.equal(failed.stage, "failed");
    assert.match(failed.lastError, /公開URLを発行する前に終了/);
    assert.match(failed.diagnostic, /unable to reach Cloudflare edge/);
  } finally {
    await manager.stop();
    await fs.rm(runtimeRoot, { recursive: true, force: true });
  }
});

test("quick publish rejects a cloudflared download with an unexpected checksum", async () => {
  const runtimeRoot = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-quick-install-"));
  const remoteHost = fakeRemoteHost();
  const manager = createTournamentQuickPublishManager({
    runtimeRoot,
    remoteHost,
    platform: "win32",
    expectedSha256: "0".repeat(64),
    fetchImpl: async () => ({
      ok: true,
      status: 200,
      headers: {
        get(name) {
          return name.toLowerCase() === "content-length" ? "5" : null;
        },
      },
      async arrayBuffer() {
        return Buffer.from("wrong");
      },
    }),
  });

  try {
    await assert.rejects(manager.install(), /SHA-256/);
    await assert.rejects(fs.access(path.join(runtimeRoot, "cloudflared.exe")));
  } finally {
    await fs.rm(runtimeRoot, { recursive: true, force: true });
  }
});
