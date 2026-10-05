import assert from "node:assert/strict";
import test from "node:test";
import { cancelOverlayAutoScroll, captureOverlayAutoScrollState, setupOverlayAutoScroll } from "../app/overlay/autoscroll.js";

function classList(...initial) {
  const values = new Set(initial);
  return {
    contains: (value) => values.has(value),
    remove: (value) => values.delete(value),
    toggle: (value, enabled) => enabled ? values.add(value) : values.delete(value),
  };
}

function harness(t, { height = 80, grouped = true, speed = 14 } = {}) {
  let now = 0;
  let tick;
  let resize;
  let disconnected = false;
  let cleared = false;
  t.mock.method(performance, "now", () => now);
  t.mock.method(globalThis, "setInterval", (callback) => { tick = callback; return 1; });
  t.mock.method(globalThis, "clearInterval", () => { cleared = true; });
  const previousObserver = Object.getOwnPropertyDescriptor(globalThis, "ResizeObserver");
  globalThis.ResizeObserver = class {
    constructor(callback) { resize = callback; }
    observe() {}
    disconnect() { disconnected = true; }
  };
  t.after(() => {
    cancelOverlayAutoScroll();
    if (previousObserver) Object.defineProperty(globalThis, "ResizeObserver", previousObserver);
    else delete globalThis.ResizeObserver;
  });
  const content = { innerHTML: "same cards", style: {}, scrollHeight: 400, firstElementChild: { offsetHeight: 48 } };
  const container = {
    classList: classList(), clientHeight: 100, dataset: { scrollKey: "special-groups", scrollSpeed: speed },
    firstElementChild: { innerHTML: "same groups", style: {}, scrollHeight: 480, classList: classList(...(grouped ? ["special-overlay-groups"] : [])) },
    querySelectorAll: () => [child],
  };
  const child = {
    clientHeight: height, firstElementChild: content, dataset: { scrollKey: "special-group", scrollSpeed: speed },
    closest: () => container,
  };
  const root = { querySelectorAll: (selector) => selector === "[data-autoscroll]" ? [child] : [container] };
  return {
    root, child, container, content,
    advance(ticks = 50) { for (let i = 0; i < ticks; i++) { now += 80; tick(); } },
    resize: () => resize(),
    wasDisconnected: () => disconnected,
    wasCleared: () => cleared,
  };
}

test("special groups keep their headings still when a card fits below each heading", (t) => {
  const h = harness(t);
  setupOverlayAutoScroll(h.root);
  h.advance();
  assert.notEqual(h.content.style.transform, "translateY(0px)");
  assert.equal(h.container.firstElementChild.style.transform, "translateY(0px)");
});

test("cramped special groups scroll together without also moving their inner lists", (t) => {
  const h = harness(t, { height: 7 });
  setupOverlayAutoScroll(h.root);
  h.advance();
  assert.notEqual(h.container.firstElementChild.style.transform, "translateY(0px)");
  assert.equal(h.content.style.transform, "translateY(0px)");
});

test("resizing restores independent group scrolling and clears the previous translation", (t) => {
  const h = harness(t, { height: 7 });
  setupOverlayAutoScroll(h.root);
  h.advance();
  h.child.clientHeight = 80;
  h.resize();
  h.advance();
  assert.equal(h.container.firstElementChild.style.transform, "translateY(0px)");
  assert.notEqual(h.content.style.transform, "translateY(0px)");
  h.child.clientHeight = 7;
  h.resize();
  h.advance();
  assert.notEqual(h.container.firstElementChild.style.transform, "translateY(0px)");
  assert.equal(h.content.style.transform, "translateY(0px)");
});

test("an ungrouped thought list keeps its own scrolling even below one card height", (t) => {
  const h = harness(t, { height: 30, grouped: false });
  setupOverlayAutoScroll(h.root);
  h.advance();
  assert.notEqual(h.content.style.transform, "translateY(0px)");
  assert.equal(h.container.firstElementChild.style.transform, undefined);
});

test("zero scroll speed is respected in both independent and whole-group modes", (t) => {
  const h = harness(t, { height: 7, speed: 0 });
  setupOverlayAutoScroll(h.root);
  h.advance();
  assert.equal(h.container.firstElementChild.style.transform, "translateY(0px)");
  h.child.clientHeight = 80;
  h.resize();
  h.advance();
  assert.equal(h.content.style.transform, "translateY(0px)");
});

test("cancelling scrolling releases the resize observer and timer", (t) => {
  const h = harness(t);
  setupOverlayAutoScroll(h.root);
  cancelOverlayAutoScroll();
  assert.equal(h.wasDisconnected(), true);
  assert.equal(h.wasCleared(), true);
});

function scrollOffset(content) {
  return Math.abs(Number(content.style.transform.match(/[-\d.]+/)[0]));
}

test("unrelated redraws preserve progress and allow the last card to appear", (t) => {
  const h = harness(t);
  setupOverlayAutoScroll(h.root);
  let furthest = 0;
  for (let redraw = 0; redraw < 40; redraw++) {
    h.advance(13);
    furthest = Math.max(furthest, scrollOffset(h.content));
    setupOverlayAutoScroll(h.root);
  }
  assert.equal(furthest, 320);
});

test("redraws preserve the return direction and the pause at the bottom", (t) => {
  const h = harness(t, { speed: 400 });
  setupOverlayAutoScroll(h.root);
  h.advance(21);
  assert.equal(scrollOffset(h.content), 320);
  setupOverlayAutoScroll(h.root);
  h.advance(10);
  assert.equal(scrollOffset(h.content), 320);
  setupOverlayAutoScroll(h.root);
  h.advance(11);
  assert(scrollOffset(h.content) < 320);
  assert(scrollOffset(h.content) > 0);
});

test("a changed list restarts from the beginning", (t) => {
  const h = harness(t);
  setupOverlayAutoScroll(h.root);
  h.advance();
  h.content.innerHTML = "different cards";
  setupOverlayAutoScroll(h.root);
  assert.equal(scrollOffset(h.content), 0);
});

test("resize callbacks preserve progress unless the group scrolling mode changes", (t) => {
  const h = harness(t);
  setupOverlayAutoScroll(h.root);
  h.advance();
  const before = scrollOffset(h.content);
  h.resize();
  assert.equal(scrollOffset(h.content), before);
  h.advance(2);
  assert(scrollOffset(h.content) > before);
});

test("a replacement DOM list with the same identity keeps its position", (t) => {
  const h = harness(t);
  setupOverlayAutoScroll(h.root);
  h.advance();
  const before = scrollOffset(h.content);
  const replacement = { ...h.content, style: {} };
  h.child.firstElementChild = replacement;
  setupOverlayAutoScroll(h.root);
  assert.equal(scrollOffset(replacement), before);
  h.advance(2);
  assert(scrollOffset(replacement) > before);
});

test("a resized viewport clamps the preserved position to the new end", (t) => {
  const h = harness(t, { speed: 100 });
  setupOverlayAutoScroll(h.root);
  h.advance();
  h.child.clientHeight = 350;
  h.resize();
  assert.equal(scrollOffset(h.content), 50);
});

test("explicit snapshots survive cancellation before the page DOM is replaced", (t) => {
  const h = harness(t);
  setupOverlayAutoScroll(h.root);
  h.advance();
  const before = scrollOffset(h.content);
  const saved = captureOverlayAutoScrollState();
  cancelOverlayAutoScroll();
  assert.equal(captureOverlayAutoScrollState().size, 0);
  const replacement = { ...h.content, style: {} };
  h.child.firstElementChild = replacement;
  setupOverlayAutoScroll(h.root, saved);
  assert.equal(scrollOffset(replacement), before);
});

test("a newly displayed list cannot inherit another list's position", (t) => {
  const h = harness(t);
  setupOverlayAutoScroll(h.root);
  h.advance();
  h.child.dataset.scrollKey = "another-list";
  setupOverlayAutoScroll(h.root);
  assert.equal(scrollOffset(h.content), 0);
});
