import test from "node:test";
import assert from "node:assert/strict";

import {
  createEditorStateSignature,
  decideEditorRefresh,
} from "../services/tournament-relay/public/editor-refresh-policy.js";

test("relay editor keeps controls mounted while the polled state is unchanged", () => {
  const signature = createEditorStateSignature({
    state: {
      run: { campaignId: "is2", ingot: 0 },
      operators: [],
      relics: [],
    },
  });

  assert.equal(decideEditorRefresh({
    previousSignature: signature,
    nextSignature: signature,
    editorActive: false,
  }), "skip");
});

test("relay editor defers external state changes while a control is active", () => {
  assert.equal(decideEditorRefresh({
    previousSignature: "{\"run\":{\"ingot\":0}}",
    nextSignature: "{\"run\":{\"ingot\":1}}",
    editorActive: true,
  }), "defer");
});

test("relay editor refreshes changed and explicitly refreshed state", () => {
  assert.equal(decideEditorRefresh({
    previousSignature: "{\"operators\":[]}",
    nextSignature: "{\"operators\":[\"operator:amiya\"]}",
    editorActive: false,
  }), "render");

  assert.equal(decideEditorRefresh({
    previousSignature: "{\"relics\":[]}",
    nextSignature: "{\"relics\":[]}",
    force: true,
    editorActive: true,
  }), "render");
});
