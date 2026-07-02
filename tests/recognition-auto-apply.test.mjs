import test from "node:test";
import assert from "node:assert/strict";

import { applyRecognitionScanCompletionToState } from "../app/domain/recognition/auto-apply.js";

test("thought full scan auto-apply aggregates duplicate thought instances into counts", () => {
  const state = {
    run: {
      campaignId: "is5_sarkaz",
      special: { is5_sarkaz: { thought: [] } },
    },
  };
  const suggestions = [
    {
      profileId: "is5ThoughtFull",
      recognitionKey: "thought:is5_sarkaz:t1:_:_:roi:100,200",
      candidate: { kind: "thought", campaignId: "is5_sarkaz", thoughtId: "t1", instanceId: "roi:100,200" },
    },
    {
      profileId: "is5ThoughtFull",
      recognitionKey: "thought:is5_sarkaz:t1:_:_:roi:800,200",
      candidate: { kind: "thought", campaignId: "is5_sarkaz", thoughtId: "t1", instanceId: "roi:800,200" },
    },
  ];

  const result = applyRecognitionScanCompletionToState(state, { profileId: "is5ThoughtFull", suggestions });

  assert.deepEqual(result.state.run.special.is5_sarkaz.thought, [{ effectId: "t1", count: 2, stateId: null }]);
  assert.equal(result.autoApplied.length, 2);
  assert.equal(result.remainingSuggestions.length, 0);
});

test("run status auto-apply ignores candidates from other campaigns", () => {
  const state = {
    run: {
      campaignId: "is5_sarkaz",
      difficulty: 1,
    },
  };
  const suggestions = [
    {
      profileId: "runStatusFull",
      recognitionKey: "runStatus:is5_sarkaz:difficulty:18",
      candidate: { kind: "runStatus", field: "difficulty", value: 18, campaignId: "is5_sarkaz" },
    },
    {
      profileId: "runStatusFull",
      recognitionKey: "runStatus:is4_sami:difficulty:14",
      candidate: { kind: "runStatus", field: "difficulty", value: 14, campaignId: "is4_sami" },
    },
  ];

  const result = applyRecognitionScanCompletionToState(state, { profileId: "runStatusFull", suggestions });

  assert.equal(result.state.run.difficulty, 18);
  assert.deepEqual(result.autoApplied.map((item) => item.recognitionKey), ["runStatus:is5_sarkaz:difficulty:18"]);
  assert.deepEqual(result.remainingSuggestions.map((item) => item.recognitionKey), ["runStatus:is4_sami:difficulty:14"]);
});

test("run status auto-apply works for the current non-IS5 campaign", () => {
  const state = {
    run: {
      campaignId: "is4_sami",
    },
  };
  const suggestions = [
    {
      profileId: "runStatusFull",
      recognitionKey: "runStatus:is4_sami:difficulty:12",
      candidate: { kind: "runStatus", field: "difficulty", value: 12, campaignId: "is4_sami" },
    },
    {
      profileId: "runStatusFull",
      recognitionKey: "runStatus:_:ingot:20",
      candidate: { kind: "runStatus", field: "ingot", value: 20 },
    },
    {
      profileId: "runStatusFull",
      recognitionKey: "runStatus:is5_sarkaz:idea:7",
      candidate: { kind: "runStatus", field: "idea", value: 7, campaignId: "is5_sarkaz" },
    },
  ];

  const result = applyRecognitionScanCompletionToState(state, { profileId: "runStatusFull", suggestions });

  assert.equal(result.state.run.difficulty, 12);
  assert.equal(result.state.run.ingot, 20);
  assert.equal(result.state.run.special?.is5_sarkaz?.idea, undefined);
  assert.deepEqual(result.autoApplied.map((item) => item.recognitionKey), ["runStatus:is4_sami:difficulty:12", "runStatus:_:ingot:20"]);
  assert.deepEqual(result.remainingSuggestions.map((item) => item.recognitionKey), ["runStatus:is5_sarkaz:idea:7"]);
});

test("relic full scan auto-apply replaces only the current campaign relic set", () => {
  const state = {
    run: {
      campaignId: "is4_sami",
    },
    relics: ["is4_sami_relic_old", "is5_sarkaz_relic_keep"],
  };
  const suggestions = [
    {
      profileId: "relicsFull",
      recognitionKey: "relic:is4_sami_relic_001",
      candidate: { kind: "relic", relicId: "is4_sami_relic_001", campaignId: "is4_sami" },
    },
    {
      profileId: "relicsFull",
      recognitionKey: "relic:is4_sami_relic_002",
      candidate: { kind: "relic", relicId: "is4_sami_relic_002", campaignId: "is4_sami" },
    },
    {
      profileId: "relicsFull",
      recognitionKey: "relic:is5_sarkaz_relic_001",
      candidate: { kind: "relic", relicId: "is5_sarkaz_relic_001", campaignId: "is5_sarkaz" },
    },
  ];

  const result = applyRecognitionScanCompletionToState(state, { profileId: "relicsFull", suggestions });

  assert.deepEqual(result.state.relics, ["is5_sarkaz_relic_keep", "is4_sami_relic_001", "is4_sami_relic_002"]);
  assert.deepEqual(result.autoApplied.map((item) => item.recognitionKey), ["relic:is4_sami_relic_001", "relic:is4_sami_relic_002"]);
  assert.deepEqual(result.remainingSuggestions.map((item) => item.recognitionKey), ["relic:is5_sarkaz_relic_001"]);
});
