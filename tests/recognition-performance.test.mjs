import test from 'node:test';
import assert from 'node:assert/strict';
import { compareRecognitionOperations } from '../tools/compare-recognition-performance.mjs';

let nextId = 0;
const operation = (durationMs, signature = 'same', status = 'completed') => ({
  schemaVersion: 1, operationId: `id-${nextId++}`, profileIds: ['operatorsFull'], status, durationMs,
  stages: [{ name: 'apply-and-save', durationMs: 10 }], resultSignatures: { operatorsFull: signature },
});

test('recognition comparison does not count repeated copies as independent runs', () => {
  const pair = { baseline: operation(100), candidate: operation(80) };
  assert.equal(compareRecognitionOperations(Array(5).fill(pair)).eligible, false);
});

test('recognition comparison rejects old scan durations and failed runs', () => {
  assert.equal(compareRecognitionOperations([{ baseline: { durationMs: 100 }, candidate: operation(10) }]).eligible, false);
  assert.equal(compareRecognitionOperations([{ baseline: operation(100), candidate: operation(10, 'same', 'partial') }]).eligible, false);
});

test('recognition comparison cannot accept a faster but different result', () => {
  const pairs = Array.from({ length: 5 }, () => ({ baseline: operation(100), candidate: operation(10, 'changed-promotion') }));
  const result = compareRecognitionOperations(pairs);
  assert.equal(result.resultParity, false);
  assert.equal(result.passesTimingAndParity, false);
});

test('recognition comparison requires five paired samples and no maximum regression', () => {
  const pairs = Array.from({ length: 5 }, () => ({ baseline: operation(100), candidate: operation(80) }));
  const result = compareRecognitionOperations(pairs);
  assert.equal(result.medianReductionPercent, 20);
  assert.equal(result.passesTimingAndParity, true);
  assert.equal(result.accuracyVerified, false);
  assert.equal(compareRecognitionOperations(pairs.slice(0, 4)).passesTimingAndParity, false);
  pairs[4].candidate.durationMs = 101;
  assert.equal(compareRecognitionOperations(pairs).passesTimingAndParity, false);
});
