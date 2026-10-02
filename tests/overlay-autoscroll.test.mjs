import assert from "node:assert/strict";
import test from "node:test";
import { cancelOverlayAutoScroll, setupOverlayAutoScroll } from "../app/overlay/autoscroll.js";

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
  const content = { style: {}, scrollHeight: 400, firstElementChild: { offsetHeight: 48 } };
  const container = {
    classList: classList(), clientHeight: 100, dataset: { scrollSpeed: speed },
    firstElementChild: { style: {}, scrollHeight: 480, classList: classList(...(grouped ? ["special-overlay-groups"] : [])) },
    querySelectorAll: () => [child],
  };
  const child = {
    clientHeight: height, firstElementChild: content, dataset: { scrollSpeed: speed },
    closest: () => container,
  };
  const root = { querySelectorAll: (selector) => selector === "[data-autoscroll]" ? [child] : [container] };
  return {
    root, child, container, content,
    advance() { for (let i = 0; i < 50; i++) { now += 80; tick(); } },
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
