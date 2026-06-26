import test from "node:test";
import assert from "node:assert/strict";

import { startServer } from "../app/server.mjs";

async function closeServer(server) {
  await new Promise((resolve, reject) => server.close((error) => error ? reject(error) : resolve()));
}

test("recognition scan API accepts POST profile requests without using the default ADB runner", async () => {
  const { server, port } = await startServer({
    port: 0,
    recognitionRunner: async ({ profile, source }) => ({
      scanId: "api-scan",
      profileId: profile.id,
      source,
      status: "completed",
      suggestions: [],
      candidates: [],
      log: [],
    }),
  });
  try {
    const response = await fetch(`http://127.0.0.1:${port}/api/recognition/scan`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ profile: "operatorsFull", source: "adb" }),
    });
    const payload = await response.json();

    assert.equal(response.status, 200);
    assert.equal(response.headers.get("cache-control"), "no-store, max-age=0, must-revalidate");
    assert.equal(payload.result.profileId, "operatorsFull");
    assert.equal(payload.result.source, "adb");
  } finally {
    await closeServer(server);
  }
});

test("external trigger routes map to full scan profiles and return aborted scans as 409", async () => {
  const { server, port } = await startServer({
    port: 0,
    recognitionRunner: async ({ profile }) => ({
      scanId: "api-scan",
      profileId: profile.id,
      source: "adb",
      status: "aborted",
      reason: "unknown_screen",
      suggestions: [],
      candidates: [],
      log: [],
    }),
  });
  try {
    const response = await fetch(`http://127.0.0.1:${port}/trigger/scan/operators/full`);
    const payload = await response.json();

    assert.equal(response.status, 409);
    assert.equal(payload.result.profileId, "operatorsFull");
    assert.equal(payload.result.reason, "unknown_screen");
  } finally {
    await closeServer(server);
  }
});
test("default recognition runner reports missing adb as service unavailable", async () => {
  const previousAdbPath = process.env.ARKNIGHTS_ADB_PATH;
  process.env.ARKNIGHTS_ADB_PATH = "definitely-missing-adb-for-test";
  const { server, port } = await startServer({ port: 0 });
  try {
    const response = await fetch(`http://127.0.0.1:${port}/api/recognition/scan`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ profile: "relicsFull", source: "adb" }),
    });
    const payload = await response.json();

    assert.equal(response.status, 503);
    assert.match(payload.error, /ADB executable was not found/);
    assert.equal(payload.details.code, "adb_not_found");
  } finally {
    await closeServer(server);
    if (previousAdbPath == null) delete process.env.ARKNIGHTS_ADB_PATH;
    else process.env.ARKNIGHTS_ADB_PATH = previousAdbPath;
  }
});

test("ADB detect API uses saved settings and returns candidates", async () => {
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
