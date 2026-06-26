import test from "node:test";
import assert from "node:assert/strict";
import { createSaveRequestTracker } from "../app/lib/save-request-tracker.js";

test("save request tracker rejects stale saves after state replacement", () => {
  const tracker = createSaveRequestTracker();
  const saveBeforeReset = tracker.issue();

  tracker.invalidate();

  assert.equal(tracker.isCurrent(saveBeforeReset), false);
});

test("save request tracker accepts only the latest scheduled save", () => {
  const tracker = createSaveRequestTracker();
  const first = tracker.issue();
  const second = tracker.issue();

  assert.equal(tracker.isCurrent(first), false);
  assert.equal(tracker.isCurrent(second), true);
});
