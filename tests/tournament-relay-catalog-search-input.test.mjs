import assert from "node:assert/strict";
import test from "node:test";
import { bindCatalogSearchInput } from "../services/tournament-relay/public/catalog-search-input.js";

function searchBox(value = "") {
  return Object.assign(new EventTarget(), {
    value, selectionStart: value.length, selectionEnd: value.length, selectionDirection: "none",
    focus() { this.focused = true; },
    setSelectionRange(start, end, direction) {
      Object.assign(this, { selectionStart: start, selectionEnd: end, selectionDirection: direction });
    },
  });
}

function fire(input, type, properties = {}) {
  input.dispatchEvent(Object.assign(new Event(type), properties));
}

function setup(value = "") {
  let current = searchBox(value);
  const initial = current;
  const searches = [];
  bindCatalogSearchInput(initial, (text) => {
    searches.push(text);
    current = searchBox(text);
  }, () => current);
  return { initial, searches, current: () => current };
}

test("catalog search keeps the same input throughout Japanese composition", () => {
  const { initial, searches, current } = setup();
  fire(initial, "compositionstart");
  for (const value of ["せ", "せんぽう", "先鋒"]) {
    initial.value = value;
    fire(initial, "input", { isComposing: true });
    assert.equal(current(), initial, "composition must not replace the focused input");
  }
  assert.deepEqual(searches, []);
  fire(initial, "compositionend");
  assert.deepEqual(searches, ["先鋒"]);
  assert.equal(current().value, "先鋒");
});

test("composition events also protect browsers without InputEvent.isComposing", () => {
  const { initial, searches, current } = setup();
  fire(initial, "compositionstart");
  initial.value = "秘宝";
  fire(initial, "input", {});
  assert.equal(current(), initial);
  fire(initial, "compositionend");
  assert.deepEqual(searches, ["秘宝"]);
});

test("composition cancellation restores the search and the final input is not applied twice", () => {
  const { initial, searches } = setup("先鋒");
  fire(initial, "compositionstart");
  initial.value = "先鋒あ";
  fire(initial, "input", { isComposing: true });
  initial.value = "先鋒";
  fire(initial, "compositionend");
  fire(initial, "input", { isComposing: false });
  assert.deepEqual(searches, []);
});

test("committed text updates once even with a following non-composing input event", () => {
  const { initial, searches } = setup();
  fire(initial, "compositionstart");
  initial.value = "術師";
  fire(initial, "input", { isComposing: true });
  fire(initial, "compositionend");
  fire(initial, "input", { isComposing: false });
  assert.deepEqual(searches, ["術師"]);
});

test("ordinary typing, paste and search clear keep working", () => {
  for (const value of ["a", "怒潮ズィマー", ""]) {
    const { initial, searches, current } = setup("old");
    initial.value = value;
    fire(initial, "input", { isComposing: false });
    assert.deepEqual(searches, [value]);
    assert.equal(current().value, value);
    assert.equal(current().focused, true);
  }
});

test("editing the middle of a query preserves the caret and selected range", () => {
  const { initial, searches, current } = setup("先鋒ABC");
  initial.value = "先鋒XYZABC";
  initial.selectionStart = 2;
  initial.selectionEnd = 5;
  initial.selectionDirection = "backward";
  fire(initial, "input", { isComposing: false });
  assert.deepEqual(searches, ["先鋒XYZABC"]);
  assert.equal(current().selectionStart, 2);
  assert.equal(current().selectionEnd, 5);
  assert.equal(current().selectionDirection, "backward");
});
