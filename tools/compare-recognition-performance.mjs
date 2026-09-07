import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const median = values => {
  const sorted = [...values].sort((a, b) => a - b);
  const mid = Math.floor(sorted.length / 2);
  return sorted.length % 2 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
};
const profiles = operation => [...operation.profileIds].sort().join('|');
const valid = operation => operation?.schemaVersion === 1
  && typeof operation.operationId === 'string' && operation.operationId.length > 0
  && operation.status === 'completed'
  && Number.isFinite(operation.durationMs) && operation.durationMs > 0
  && Array.isArray(operation.stages) && operation.stages.length > 0
  && Array.isArray(operation.profileIds) && operation.profileIds.length > 0
  && operation.profileIds.every(id => typeof operation.resultSignatures?.[id] === 'string');

// Inputs must be paired runs of the same manually prepared scene and configuration.
// Result equality is a regression check, not proof that either OCR result is correct.
export function compareRecognitionOperations(pairs) {
  const eligible = pairs.length > 0 && pairs.every(({ baseline, candidate }) =>
    valid(baseline) && valid(candidate) && profiles(baseline) === profiles(candidate))
    && new Set(pairs.map(pair => profiles(pair.baseline))).size === 1
    && new Set(pairs.flatMap(pair => [pair.baseline.operationId, pair.candidate.operationId])).size === pairs.length * 2;
  if (!eligible) return { eligible: false, passesTimingAndParity: false, accuracyVerified: false,
    reason: 'Require unique, completed end-to-end operation logs with matching profiles and result signatures.' };
  const baseline = pairs.map(pair => pair.baseline.durationMs);
  const candidate = pairs.map(pair => pair.candidate.durationMs);
  const resultParity = pairs.every(pair => pair.baseline.profileIds.every(id =>
    pair.baseline.resultSignatures[id] === pair.candidate.resultSignatures[id]));
  const baselineMedianMs = median(baseline);
  const candidateMedianMs = median(candidate);
  const maximumDidNotRegress = Math.max(...candidate) <= Math.max(...baseline);
  return {
    eligible: true, samples: pairs.length, resultParity, accuracyVerified: false,
    baselineMedianMs, candidateMedianMs,
    baselineMaximumMs: Math.max(...baseline), candidateMaximumMs: Math.max(...candidate),
    medianReductionPercent: Math.round((1 - candidateMedianMs / baselineMedianMs) * 10000) / 100,
    passesTimingAndParity: pairs.length >= 5 && resultParity && candidateMedianMs < baselineMedianMs && maximumDidNotRegress,
    note: 'Confirm scene/configuration parity and compare results with independently verified expected game state before adoption.',
  };
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  const manifestPath = process.argv[2];
  if (!manifestPath) throw new Error('Usage: node tools/compare-recognition-performance.mjs <paired-runs.json>');
  const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));
  const groups = new Map();
  for (const pair of manifest.pairs ?? []) {
    if (!pair.scenario || !pair.baseline || !pair.candidate) throw new Error('Each pair requires scenario, baseline and candidate file paths.');
    const directory = path.dirname(path.resolve(manifestPath));
    const [baseline, candidate] = await Promise.all([pair.baseline, pair.candidate]
      .map(file => readFile(path.resolve(directory, file), 'utf8').then(JSON.parse)));
    if (!groups.has(pair.scenario)) groups.set(pair.scenario, []);
    groups.get(pair.scenario).push({ baseline, candidate });
  }
  const reports = Object.fromEntries([...groups].map(([scenario, pairs]) => [scenario, compareRecognitionOperations(pairs)]));
  console.log(JSON.stringify(reports, null, 2));
  if (!groups.size || Object.values(reports).some(report => !report.passesTimingAndParity)) process.exitCode = 2;
}
