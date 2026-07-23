import assert from "node:assert/strict";
import test from "node:test";

import {
  normalizeOperatorCounts,
  operatorCountFor,
  operatorRosterCount,
  withOperatorCounts,
} from "../app/domain/operator-counts.js";

test("operator counts accept selected reserve operators only", () => {
  assert.deepEqual(
    normalizeOperatorCounts(
      {
        reserve_sniper: 3,
        reserve_caster: 120,
        gummy: 4,
        reserve_melee: 2,
        reserve_logistic: 0,
      },
      ["reserve_sniper", "reserve_caster", "gummy"],
    ),
    { reserve_sniper: 3, reserve_caster: 99 },
  );
});

test("operator roster count expands reserve counts without duplicating cards", () => {
  const operators = withOperatorCounts(
    [
      { id: "reserve_sniper", name: "予備隊員-狙撃" },
      { id: "gummy", name: "グム" },
    ],
    { reserve_sniper: 3, gummy: 8 },
  );

  assert.equal(operators.length, 2);
  assert.equal(operators[0].count, 3);
  assert.equal(operators[1].count, 1);
  assert.equal(operatorCountFor("reserve_sniper", { reserve_sniper: 3 }), 3);
  assert.equal(operatorCountFor("gummy", { gummy: 8 }), 1);
  assert.equal(operatorRosterCount(operators), 4);
});
