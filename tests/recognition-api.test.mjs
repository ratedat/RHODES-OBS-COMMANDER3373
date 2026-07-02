import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";

import { saveAdbScreenshotFrame, saveRecognitionAdbCaptureFrame, startServer } from "../app/server.mjs";

async function closeServer(server) {
  await new Promise((resolve, reject) => server.close((error) => error ? reject(error) : resolve()));
}

async function captureConsoleDuring(callback) {
  const entries = [];
  const originalLog = console.log;
  const originalError = console.error;
  console.log = (...args) => entries.push({ level: "log", message: args.map(String).join(" ") });
  console.error = (...args) => entries.push({ level: "error", message: args.map(String).join(" ") });
  try {
    const value = await callback(entries);
    return { entries, value };
  } finally {
    console.log = originalLog;
    console.error = originalError;
  }
}

test("health API exposes app, state, and runtime endpoint summary", async () => {
  const { server, port } = await startServer({ port: 0 });
  try {
    const response = await fetch(`http://127.0.0.1:${port}/api/health`);
    const payload = await response.json();

    assert.equal(response.status, 200);
    assert.equal(payload.ok, true);
    assert.equal(payload.app, "RHODES OBS COMMANDER3373");
    assert.equal(typeof payload.version, "string");
    assert.equal(typeof payload.state.campaignId === "string" || payload.state.campaignId === null, true);
    assert.equal(payload.recognition.active, false);
    assert.equal(payload.endpoints.state, "/api/state");
    assert.equal(payload.endpoints.recognitionScan, undefined);
    assert.equal(payload.endpoints.recognitionMaaResource, "/api/recognition/maa-resource");
    assert.equal(payload.endpoints.glmOcr, "/api/ocr/glm/status");
  } finally {
    await closeServer(server);
  }
});

test("legacy recognition scan routes are retired in favor of MAA Resource recognition", async () => {
  const { server, port } = await startServer({ port: 0 });
  try {
    const scanResponse = await fetch(`http://127.0.0.1:${port}/api/recognition/scan`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ profile: "operatorsFull", source: "adb", operatorClasses: ["sniper"] }),
    });
    const scanPayload = await scanResponse.json();
    const cancelResponse = await fetch(`http://127.0.0.1:${port}/api/recognition/scan/cancel`, { method: "POST" });
    const triggerResponse = await fetch(`http://127.0.0.1:${port}/trigger/scan/sarkaz/age`);

    assert.equal(scanResponse.status, 410);
    assert.equal(scanPayload.details.replacement, "/api/recognition/maa-resource");
    assert.match(scanPayload.error, /MAAFramework/);
    assert.equal(cancelResponse.status, 410);
    assert.equal(triggerResponse.status, 410);
  } finally {
    await closeServer(server);
  }
});

test("MAA Resource recognition API converts task detail JSON into RHODES candidates", async () => {
  const { server, port } = await startServer({ port: 0 });
  try {
    const response = await fetch(`http://127.0.0.1:${port}/api/recognition/maa-resource`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        profile: "runStatusFull",
        pipeline: {
          RhodesOcrRegion_run_ingot: { attach: { id: "run.ingot" } },
        },
        taskResults: [
          {
            entry: "RhodesOcrRegion_run_ingot",
            algorithm: "OCR",
            recognitionDetailJson: JSON.stringify({ best: { text: "20", score: 0.96, box: [1190, 10, 90, 52] } }),
          },
        ],
      }),
    });
    const payload = await response.json();
    const candidates = payload.result.candidates;

    assert.equal(response.status, 200);
    assert.equal(payload.result.source, "maa-framework");
    assert.equal(candidates.find((candidate) => candidate.field === "ingot")?.value, 20);
    assert.equal(candidates.some((candidate) => ["hope", "maxHope", "lifePoints", "shield", "commandLevel"].includes(candidate.field)), false);
    assert.equal(payload.result.suggestions.length, 1);
  } finally {
    await closeServer(server);
  }
});

test("ADB detect API uses saved settings and returns candidates", async () => {
  const { entries } = await captureConsoleDuring(async () => {
    const { server, port } = await startServer({
      port: 0,
      adbDetector: async ({ settings }) => ({
        settings,
        runtime: { adbPath: settings.adbPath || "adb", serial: settings.serial || "", autoDetect: settings.autoDetect, connectionPreset: settings.connectionPreset },
        selectedAdbPath: settings.adbPath || "adb",
        adbCandidates: [{ path: settings.adbPath || "adb", source: "settings", preset: settings.connectionPreset, exists: true, available: true, error: null }],
        devices: [{ serial: settings.serial || "127.0.0.1:16384", state: "device", detail: "product:MuMu" }],
      }),
    });
    try {
      const response = await fetch(`http://127.0.0.1:${port}/api/adb/detect`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ settings: { autoDetect: false, connectionPreset: "mumu", adbPath: "C:/adb.exe", serial: "127.0.0.1:16384" } }),
      });
      const payload = await response.json();

      assert.equal(response.status, 200);
      assert.equal(payload.settings.connectionPreset, "mumu");
      assert.equal(payload.devices[0].serial, "127.0.0.1:16384");
    } finally {
      await closeServer(server);
    }
  });

  assert.ok(entries.some((entry) => entry.message.includes("[adb-diagnostic]") && entry.message.includes("\"event\":\"adb_detect_start\"")));
  assert.ok(entries.some((entry) => entry.message.includes("[adb-diagnostic]") && entry.message.includes("\"event\":\"adb_detect_success\"") && entry.message.includes("127.0.0.1:16384")));
});

test("ADB test API reports resolution and optional screenshot size", async () => {
  const { server, port } = await startServer({
    port: 0,
    adbTester: async ({ settings, capture }) => ({
      ok: true,
      settings,
      runtime: { adbPath: settings.adbPath || "adb", serial: settings.serial || "" },
      resolution: { width: 2560, height: 1440 },
      screenshot: capture ? { bytes: 123456, capturedAt: "2026-06-27T00:00:00.000Z" } : null,
    }),
  });
  try {
    const response = await fetch(`http://127.0.0.1:${port}/api/adb/test`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ capture: true, settings: { adbPath: "adb", serial: "127.0.0.1:16384" } }),
    });
    const payload = await response.json();

    assert.equal(response.status, 200);
    assert.deepEqual(payload.resolution, { width: 2560, height: 1440 });
    assert.equal(payload.screenshot.bytes, 123456);
  } finally {
    await closeServer(server);
  }
});

test("ADB test API logs diagnostic details when connection fails", async () => {
  const { entries } = await captureConsoleDuring(async () => {
    const { server, port } = await startServer({
      port: 0,
      adbTester: async () => {
        const error = new Error("ADB device was not found. Start the emulator and confirm adb devices can see it.");
        error.status = 503;
        error.details = { code: "adb_device_not_found" };
        throw error;
      },
    });
    try {
      const response = await fetch(`http://127.0.0.1:${port}/api/adb/test`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ capture: true, settings: { connectionPreset: "googlePlayGames", adbPath: "adb", serial: "127.0.0.1:6520" } }),
      });
      const payload = await response.json();

      assert.equal(response.status, 503);
      assert.match(payload.error, /ADB device was not found/);
      assert.equal(payload.details.code, "adb_device_not_found");
    } finally {
      await closeServer(server);
    }
  });

  assert.ok(entries.some((entry) => entry.message.includes("[adb-diagnostic]") && entry.message.includes("\"event\":\"adb_test_start\"") && entry.message.includes("127.0.0.1:6520")));
  assert.ok(entries.some((entry) => entry.level === "error" && entry.message.includes("\"event\":\"adb_test_error\"") && entry.message.includes("adb_device_not_found")));
});

test("system hypervisor API returns injected diagnostics", async () => {
  const { server, port } = await startServer({
    port: 0,
    hypervisorDetector: async () => ({
      platform: "win32",
      supported: true,
      available: false,
      requiresBiosChange: true,
      severity: "error",
      message: "BIOS/UEFIでCPU仮想化支援を有効化してください。",
    }),
  });
  try {
    const response = await fetch(`http://127.0.0.1:${port}/api/system/hypervisor`);
    const payload = await response.json();

    assert.equal(response.status, 200);
    assert.equal(payload.requiresBiosChange, true);
    assert.match(payload.message, /BIOS/);
  } finally {
    await closeServer(server);
  }
});

test("GLM OCR runtime APIs expose status, async install, and uninstall", async () => {
  const calls = [];
  const glmOcrRuntimeManager = {
    status: async () => {
      calls.push("status");
      return { status: "missing", installed: false, installing: false, installRoot: "D:/state/glm-ocr-runtime" };
    },
    install: async () => {
      calls.push("install");
      return { status: "missing", installed: false, installing: true, installJob: { status: "installing", log: [] } };
    },
    uninstall: async () => {
      calls.push("uninstall");
      return { status: "missing", installed: false, installing: false };
    },
  };
  const { server, port } = await startServer({ port: 0, glmOcrRuntimeManager });
  try {
    const statusResponse = await fetch(`http://127.0.0.1:${port}/api/ocr/glm/status`);
    const statusPayload = await statusResponse.json();
    const installResponse = await fetch(`http://127.0.0.1:${port}/api/ocr/glm/install`, { method: "POST" });
    const installPayload = await installResponse.json();
    const uninstallResponse = await fetch(`http://127.0.0.1:${port}/api/ocr/glm/uninstall`, { method: "POST" });
    const uninstallPayload = await uninstallResponse.json();

    assert.equal(statusResponse.status, 200);
    assert.equal(statusPayload.status, "missing");
    assert.equal(installResponse.status, 202);
    assert.equal(installPayload.installing, true);
    assert.equal(uninstallResponse.status, 200);
    assert.equal(uninstallPayload.installed, false);
    assert.deepEqual(calls, ["status", "install", "uninstall"]);
  } finally {
    await closeServer(server);
  }
});

test("GLM OCR Ollama runtime APIs expose status, async install, start, and uninstall", async () => {
  const calls = [];
  const ollamaRuntimeManager = {
    status: async () => {
      calls.push("status");
      return { status: "missing", installed: false, installing: false, installRoot: "D:/state/ollama-runtime" };
    },
    install: async () => {
      calls.push("install");
      return { status: "partial", installed: true, installing: true, installJob: { status: "installing", log: [] } };
    },
    start: async () => {
      calls.push("start");
      return { status: "ready", installed: true, installing: false, serverReachable: true, modelPresent: true };
    },
    uninstall: async () => {
      calls.push("uninstall");
      return { status: "missing", installed: false, installing: false };
    },
  };
  const { server, port } = await startServer({ port: 0, ollamaRuntimeManager });
  try {
    const statusResponse = await fetch(`http://127.0.0.1:${port}/api/ocr/glm/ollama/status`);
    const statusPayload = await statusResponse.json();
    const installResponse = await fetch(`http://127.0.0.1:${port}/api/ocr/glm/ollama/install`, { method: "POST" });
    const installPayload = await installResponse.json();
    const startResponse = await fetch(`http://127.0.0.1:${port}/api/ocr/glm/ollama/start`, { method: "POST" });
    const startPayload = await startResponse.json();
    const uninstallResponse = await fetch(`http://127.0.0.1:${port}/api/ocr/glm/ollama/uninstall`, { method: "POST" });
    const uninstallPayload = await uninstallResponse.json();

    assert.equal(statusResponse.status, 200);
    assert.equal(statusPayload.status, "missing");
    assert.equal(installResponse.status, 202);
    assert.equal(installPayload.installing, true);
    assert.equal(startResponse.status, 200);
    assert.equal(startPayload.status, "ready");
    assert.equal(uninstallResponse.status, 200);
    assert.equal(uninstallPayload.installed, false);
    assert.deepEqual(calls, ["status", "install", "start", "uninstall"]);
  } finally {
    await closeServer(server);
  }
});


test("ADB path picker API returns the desktop selected path", async () => {
  const selectedPath = "M:/Program Files/Netease/MuMu Player 12/shell/adb.exe";
  const { server, port } = await startServer({
    port: 0,
    adbPathPicker: async () => ({ canceled: false, path: selectedPath }),
  });
  try {
    const response = await fetch(`http://127.0.0.1:${port}/api/adb/select-path`, { method: "POST" });
    const payload = await response.json();

    assert.equal(response.status, 200);
    assert.equal(payload.canceled, false);
    assert.equal(payload.path, selectedPath);
  } finally {
    await closeServer(server);
  }
});

test("saveAdbScreenshotFrame writes PNG bytes to the local state screenshot directory", async () => {
  const stateDir = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-adb-shot-"));
  const screenshot = await saveAdbScreenshotFrame({
    bytes: Buffer.from([0x89, 0x50, 0x4e, 0x47]),
    capturedAt: "2026-06-27T00:00:00.000Z",
  }, { stateDir, now: new Date("2026-06-27T00:00:00.000Z") });

  assert.equal(screenshot.bytes, 4);
  assert.equal(screenshot.capturedAt, "2026-06-27T00:00:00.000Z");
  assert.equal(screenshot.path.endsWith(path.join("adb-screenshots", "adb-test-2026-06-27T00-00-00-000Z.png")), true);
  assert.deepEqual([...await fs.readFile(screenshot.path)], [0x89, 0x50, 0x4e, 0x47]);
});

test("saveRecognitionAdbCaptureFrame writes scan screenshots to a readable debug directory", async () => {
  const baseDir = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-adb-debug-shot-"));
  const screenshot = await saveRecognitionAdbCaptureFrame({
    bytes: Buffer.from([0x89, 0x50, 0x4e, 0x47]),
    capturedAt: "2026-06-28T09:22:06.623Z",
  }, {
    baseDir,
    scanId: "scan/with unsafe chars",
    profile: { id: "is5AgeFull" },
    source: "adb",
    stage: "scan",
    passIndex: 0,
    iteration: 2,
    scanStartedAt: "2026-06-28T09:22:00.000Z",
  });
  const knownScreen = await saveRecognitionAdbCaptureFrame({
    bytes: Buffer.from([0x89, 0x50]),
    capturedAt: "2026-06-28T09:22:07.000Z",
  }, {
    baseDir,
    scanId: "scan/with unsafe chars",
    profile: { id: "is5AgeFull" },
    source: "adb",
    stage: "known-screen",
    scanStartedAt: "2026-06-28T09:22:00.000Z",
  });

  assert.equal(screenshot.bytes, 4);
  assert.equal(screenshot.path.startsWith(baseDir), true);
  assert.equal(screenshot.path.endsWith(path.join("scan-p0-i2.png")), true);
  assert.equal(path.dirname(screenshot.path), path.dirname(knownScreen.path));
  assert.match(screenshot.path, /is5AgeFull/);
  assert.match(screenshot.path, /scan_with_unsafe_chars/);
  assert.deepEqual([...await fs.readFile(screenshot.path)], [0x89, 0x50, 0x4e, 0x47]);
});

test("recognition scan status API remains available as a passive MAAFramework bridge", async () => {
  const { server, port } = await startServer({ port: 0 });
  try {
    const statusResponse = await fetch(`http://127.0.0.1:${port}/api/recognition/scan/status`);
    const statusPayload = await statusResponse.json();

    assert.equal(statusResponse.status, 200);
    assert.equal(statusPayload.active, null);
    assert.equal(statusPayload.lastScan, null);
  } finally {
    await closeServer(server);
  }
});
