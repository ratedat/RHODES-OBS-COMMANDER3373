import test from "node:test";
import assert from "node:assert/strict";

import { createAppServer } from "../app/server.mjs";

async function withServer(tournamentRemoteHost, run, tournamentQuickPublishManager = fakeQuickManager(tournamentRemoteHost)) {
  const server = createAppServer({ tournamentRemoteHost, tournamentQuickPublishManager });
  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", resolve);
  });
  const { port } = server.address();
  try {
    await run(`http://127.0.0.1:${port}`);
  } finally {
    await new Promise((resolve) => server.close(resolve));
  }
}

function fakeQuickManager(remoteHost) {
  let installed = false;
  let active = false;
  const calls = [];
  return {
    calls,
    async status() {
      return {
        installed,
        active,
        starting: false,
        publicUrl: active ? "https://sample.trycloudflare.com" : "",
        localRelayUrl: active ? "http://127.0.0.1:31999" : "",
        remote: remoteHost.status(),
      };
    },
    async install() {
      calls.push(["install"]);
      installed = true;
      return this.status();
    },
    async start(options) {
      calls.push(["start", options]);
      installed = true;
      active = true;
      await remoteHost.start({
        relayUrl: "https://sample.trycloudflare.com",
        playerLabel: options.playerLabel,
        adminToken: "generated-secret",
      });
      return this.status();
    },
    async stop() {
      calls.push(["stop"]);
      active = false;
      await remoteHost.stop();
      return this.status();
    },
    async uninstall() {
      calls.push(["uninstall"]);
      active = false;
      installed = false;
      await remoteHost.stop();
      return this.status();
    },
  };
}

function fakeRemoteHost() {
  let active = false;
  const calls = [];
  return {
    calls,
    status() {
      return {
        active,
        inputUrl: active ? "https://relay.example/input/session?code=ABC123" : "",
        editorCode: active ? "ABC123" : "",
      };
    },
    async start(options) {
      calls.push(["start", options]);
      active = true;
      return this.status();
    },
    async sync() {
      calls.push(["sync"]);
      return this.status();
    },
    async stop() {
      calls.push(["stop"]);
      active = false;
      return this.status();
    },
  };
}

async function jsonRequest(url, options = {}) {
  const response = await fetch(url, options);
  return {
    response,
    body: await response.json(),
  };
}

test("local API starts, reports, syncs, and stops a tournament remote session", async () => {
  const host = fakeRemoteHost();
  await withServer(host, async (baseUrl) => {
    const initial = await jsonRequest(`${baseUrl}/api/tournament/remote/status`);
    assert.equal(initial.body.active, false);

    const started = await jsonRequest(`${baseUrl}/api/tournament/remote/start`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        relayUrl: "https://relay.example",
        playerLabel: "Player A",
        adminToken: "secret",
      }),
    });
    assert.equal(started.response.status, 201);
    assert.equal(started.body.active, true);
    assert.deepEqual(host.calls[0], ["start", {
      relayUrl: "https://relay.example",
      playerLabel: "Player A",
      adminToken: "secret",
    }]);

    const synced = await jsonRequest(`${baseUrl}/api/tournament/remote/sync`, { method: "POST" });
    assert.equal(synced.body.active, true);

    const stopped = await jsonRequest(`${baseUrl}/api/tournament/remote/stop`, { method: "POST" });
    assert.equal(stopped.body.active, false);
  });
});

test("local API manages a one-click temporary tournament publication", async () => {
  const host = fakeRemoteHost();
  const quick = fakeQuickManager(host);
  await withServer(host, async (baseUrl) => {
    const initial = await jsonRequest(`${baseUrl}/api/tournament/quick/status`);
    assert.equal(initial.body.installed, false);
    assert.equal(initial.body.active, false);

    const started = await jsonRequest(`${baseUrl}/api/tournament/quick/start`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ playerLabel: "Player B" }),
    });
    assert.equal(started.response.status, 201);
    assert.equal(started.body.active, true);
    assert.equal(started.body.publicUrl, "https://sample.trycloudflare.com");
    assert.equal(started.body.remote.editorCode, "ABC123");
    assert.deepEqual(quick.calls[0], ["start", { playerLabel: "Player B" }]);

    const stopped = await jsonRequest(`${baseUrl}/api/tournament/quick/stop`, { method: "POST" });
    assert.equal(stopped.body.active, false);

    const uninstalled = await jsonRequest(`${baseUrl}/api/tournament/quick/uninstall`, { method: "POST" });
    assert.equal(uninstalled.body.installed, false);
  }, quick);
});
