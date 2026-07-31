import test from "node:test";
import assert from "node:assert/strict";

import {
  createTournamentRelayServer,
  startTournamentRelayServer,
} from "../services/tournament-relay/server.mjs";

async function withServer(run, options = {}) {
  const server = createTournamentRelayServer(options);
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

async function jsonRequest(url, options = {}) {
  const response = await fetch(url, options);
  const body = response.status === 204 ? null : await response.json();
  return { response, body };
}

test("relay HTTP API keeps the host token out of editor bootstrap", async () => {
  await withServer(async (baseUrl) => {
    const created = await jsonRequest(`${baseUrl}/api/sessions`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ playerLabel: "Player A" }),
    });
    assert.equal(created.response.status, 201);

    const bootstrap = await jsonRequest(
      `${baseUrl}/api/sessions/${created.body.sessionId}/bootstrap?code=${created.body.editorCode}`,
    );
    assert.equal(bootstrap.response.status, 200);
    assert.equal(bootstrap.body.playerLabel, "Player A");
    assert.equal(bootstrap.body.hostToken, undefined);
    assert.equal(bootstrap.body.editorCode, undefined);
  });
});

test("relay HTTP API accepts the full editor snapshot above the legacy 1 MB limit", async () => {
  await withServer(async (baseUrl) => {
    const created = (
      await jsonRequest(`${baseUrl}/api/sessions`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ playerLabel: "Large snapshot" }),
      })
    ).body;
    const catalog = "x".repeat(1_600_000);

    const updated = await jsonRequest(`${baseUrl}/api/sessions/${created.sessionId}/snapshot`, {
      method: "PUT",
      headers: {
        authorization: `Bearer ${created.hostToken}`,
        "content-type": "application/json",
      },
      body: JSON.stringify({ snapshot: { master: { catalog } } }),
    });
    assert.equal(updated.response.status, 200);

    const bootstrap = await jsonRequest(
      `${baseUrl}/api/sessions/${created.sessionId}/bootstrap?code=${created.editorCode}`,
    );
    assert.equal(bootstrap.response.status, 200);
    assert.equal(bootstrap.body.snapshot.master.catalog.length, catalog.length);
  });
});

test("relay HTTP API still rejects requests above its configured body limit", async () => {
  await withServer(
    async (baseUrl) => {
      const response = await jsonRequest(`${baseUrl}/api/sessions`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ playerLabel: "x".repeat(2_000) }),
      });
      assert.equal(response.response.status, 413);
      assert.equal(response.body.code, "payload_too_large");
    },
    { maxBodyBytes: 1_024 },
  );
});

test("relay HTTP API accepts editor operations and exposes them only to the host", async () => {
  await withServer(async (baseUrl) => {
    const created = (
      await jsonRequest(`${baseUrl}/api/sessions`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ playerLabel: "Player B" }),
      })
    ).body;

    const queued = await jsonRequest(`${baseUrl}/api/sessions/${created.sessionId}/operations`, {
      method: "POST",
      headers: {
        "content-type": "application/json",
        "x-editor-code": created.editorCode,
      },
      body: JSON.stringify({
        operation: { type: "run.set", field: "ingot", value: 31 },
      }),
    });
    assert.equal(queued.response.status, 202);
    assert.equal(queued.body.sequence, 1);

    const denied = await jsonRequest(`${baseUrl}/api/sessions/${created.sessionId}/operations`);
    assert.equal(denied.response.status, 401);

    const hostView = await jsonRequest(`${baseUrl}/api/sessions/${created.sessionId}/operations`, {
      headers: { authorization: `Bearer ${created.hostToken}` },
    });
    assert.equal(hostView.response.status, 200);
    assert.equal(hostView.body.operations[0].operation.value, 31);
  });
});

test("relay serves the two-pane editor assets", async () => {
  await withServer(async (baseUrl) => {
    const html = await fetch(`${baseUrl}/input/example`);
    assert.equal(html.status, 200);
    assert.match(await html.text(), /現在の配信状態/);

    const script = await fetch(`${baseUrl}/assets/input.js`);
    assert.equal(script.status, 200);
    assert.match(await script.text(), /renderRunEditor/);

    const refreshPolicy = await fetch(`${baseUrl}/assets/editor-refresh-policy.js`);
    assert.equal(refreshPolicy.status, 200);
    assert.match(await refreshPolicy.text(), /decideEditorRefresh/);

    const editorDraft = await fetch(`${baseUrl}/assets/editor-draft.js`);
    assert.equal(editorDraft.status, 200);
    assert.match(await editorDraft.text(), /buildDraftState/);

    const catalogFilters = await fetch(`${baseUrl}/assets/catalog-filters.js`);
    assert.equal(catalogFilters.status, 200);
    assert.match(await catalogFilters.text(), /buildOperatorCatalogView/);
  });
});

test("relay refuses external binding without an admin token", async () => {
  await assert.rejects(
    startTournamentRelayServer({ host: "0.0.0.0", port: 0, adminToken: "" }),
    /TOURNAMENT_RELAY_ADMIN_TOKEN/,
  );
});

test("relay permits external binding when an admin token is configured", async () => {
  const { server } = await startTournamentRelayServer({
    host: "0.0.0.0",
    port: 0,
    adminToken: "test-admin-token",
  });
  await new Promise((resolve) => server.close(resolve));
});
