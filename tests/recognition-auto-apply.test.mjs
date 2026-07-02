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

test("IS4 revelation full scan auto-apply replaces the current revelation board", () => {
  const state = {
    run: {
      campaignId: "is4_sami",
      special: {
        is4_sami: {
          revelation: {
            causeId: "old_cause",
            structureId: "old_structure",
            rhetorics: [{ effectId: "old_rhetoric", count: 5 }],
          },
        },
      },
    },
  };
  const suggestions = [
    {
      profileId: "is4RevelationFull",
      recognitionKey: "revelation:is4_sami:revelationBoard:cause:cause_a:_",
      candidate: { kind: "revelation", campaignId: "is4_sami", fieldId: "revelationBoard", slotKind: "cause", effectId: "cause_a" },
    },
    {
      profileId: "is4RevelationFull",
      recognitionKey: "revelation:is4_sami:revelationBoard:structure:structure_a:_",
      candidate: { kind: "revelation", campaignId: "is4_sami", fieldId: "revelationBoard", slotKind: "structure", effectId: "structure_a" },
    },
    {
      profileId: "is4RevelationFull",
      recognitionKey: "revelation:is4_sami:revelationBoard:rhetoric:rhetoric_a:_",
      candidate: { kind: "revelation", campaignId: "is4_sami", fieldId: "revelationBoard", slotKind: "rhetoric", effectId: "rhetoric_a", count: 2 },
    },
    {
      profileId: "is4RevelationFull",
      recognitionKey: "revelation:is5_sarkaz:revelationBoard:rhetoric:other:_",
      candidate: { kind: "revelation", campaignId: "is5_sarkaz", fieldId: "revelationBoard", slotKind: "rhetoric", effectId: "other" },
    },
  ];

  const result = applyRecognitionScanCompletionToState(state, { profileId: "is4RevelationFull", suggestions });

  assert.deepEqual(result.state.run.special.is4_sami.revelation, {
    causeId: "cause_a",
    structureId: "structure_a",
    rhetorics: [{ effectId: "rhetoric_a", count: 2, stateId: null }],
  });
  assert.deepEqual(result.autoApplied.map((item) => item.recognitionKey), [
    "revelation:is4_sami:revelationBoard:cause:cause_a:_",
    "revelation:is4_sami:revelationBoard:structure:structure_a:_",
    "revelation:is4_sami:revelationBoard:rhetoric:rhetoric_a:_",
  ]);
  assert.deepEqual(result.remainingSuggestions.map((item) => item.recognitionKey), ["revelation:is5_sarkaz:revelationBoard:rhetoric:other:_"]);
});

test("IS6 coin full scan auto-apply replaces and merges current coin entries", () => {
  const state = {
    run: {
      campaignId: "is6_sui",
      special: {
        is6_sui: {
          coins: [{ coinId: "old_coin", count: 9, statusId: null, face: "front" }],
        },
      },
    },
  };
  const suggestions = [
    {
      profileId: "is6CoinsFull",
      recognitionKey: "coin:is6_sui:coin_a:_:front:2",
      candidate: { kind: "coin", campaignId: "is6_sui", fieldId: "coins", coinId: "coin_a", count: 2 },
    },
    {
      profileId: "is6CoinsFull",
      recognitionKey: "coin:is6_sui:coin_a:status_a:back:3",
      candidate: { kind: "coin", campaignId: "is6_sui", fieldId: "coins", coinId: "coin_a", statusId: "status_a", face: "back", count: 3 },
    },
    {
      profileId: "is6CoinsFull",
      recognitionKey: "coin:is6_sui:coin_a:status_a:back:4",
      candidate: { kind: "coin", campaignId: "is6_sui", fieldId: "coins", coinId: "coin_a", statusId: "status_a", face: "back", count: 4 },
    },
    {
      profileId: "is6CoinsFull",
      recognitionKey: "coin:is4_sami:coin_other:_:front:1",
      candidate: { kind: "coin", campaignId: "is4_sami", fieldId: "coins", coinId: "coin_other", count: 1 },
    },
  ];

  const result = applyRecognitionScanCompletionToState(state, { profileId: "is6CoinsFull", suggestions });

  assert.deepEqual(result.state.run.special.is6_sui.coins, [
    { coinId: "coin_a", count: 2, statusId: null, face: "front" },
    { coinId: "coin_a", count: 7, statusId: "status_a", face: "back" },
  ]);
  assert.deepEqual(result.autoApplied.map((item) => item.recognitionKey), [
    "coin:is6_sui:coin_a:_:front:2",
    "coin:is6_sui:coin_a:status_a:back:3",
    "coin:is6_sui:coin_a:status_a:back:4",
  ]);
  assert.deepEqual(result.remainingSuggestions.map((item) => item.recognitionKey), ["coin:is4_sami:coin_other:_:front:1"]);
});
