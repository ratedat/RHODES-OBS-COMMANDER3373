import {
  customOverlayCanvas,
  defaultCustomOverlayLayout,
  normalizeCustomOverlayLayout,
} from "../lib/overlay-layout-state.js";

const editorPartLabels = Object.freeze({
  status: "ラン情報",
  relics: "秘宝",
  operators: "オペレーター",
  effects: "秘宝効果",
  bosses: "ボス",
  special: "思案・特殊値",
});

function clamp(value, minimum, maximum) {
  return Math.min(maximum, Math.max(minimum, value));
}

function sourceDelta(value, viewportSize, sourceSize) {
  if (!Number.isFinite(value) || !Number.isFinite(viewportSize) || viewportSize <= 0) return 0;
  return Math.round((value / viewportSize) * sourceSize);
}

function stylePercent(value, total) {
  return `${Number(((value / total) * 100).toFixed(4))}%`;
}

export function isOverlayEditorMode(searchParams) {
  if (!(searchParams instanceof URLSearchParams)) return false;
  const enabled = String(searchParams.get("edit") || "").trim().toLowerCase();
  return String(searchParams.get("layout") || "").trim().toLowerCase() === "custom"
    && ["1", "true", "yes"].includes(enabled);
}

export function applyOverlayEditorDelta(
  layout,
  partId,
  mode,
  viewportDeltaX,
  viewportDeltaY,
  viewportWidth,
  viewportHeight,
) {
  const normalized = normalizeCustomOverlayLayout(layout);
  const target = normalized.find((item) => item.id === partId);
  if (!target) return normalized;

  const deltaX = sourceDelta(viewportDeltaX, viewportWidth, customOverlayCanvas.width);
  const deltaY = sourceDelta(viewportDeltaY, viewportHeight, customOverlayCanvas.height);
  const next = { ...target };

  if (mode === "move") {
    next.x = clamp(target.x + deltaX, 0, customOverlayCanvas.width - target.width);
    next.y = clamp(target.y + deltaY, 0, customOverlayCanvas.height - target.height);
  } else if (mode === "resize") {
    next.width = clamp(target.width + deltaX, 160, customOverlayCanvas.width - target.x);
    next.height = clamp(target.height + deltaY, 80, customOverlayCanvas.height - target.y);
  }

  return normalizeCustomOverlayLayout(normalized.map((item) => item.id === partId ? next : item));
}

function applyGeometry(element, item) {
  element.style.setProperty("--overlay-x", stylePercent(item.x, customOverlayCanvas.width));
  element.style.setProperty("--overlay-y", stylePercent(item.y, customOverlayCanvas.height));
  element.style.setProperty("--overlay-width", stylePercent(item.width, customOverlayCanvas.width));
  element.style.setProperty("--overlay-height", stylePercent(item.height, customOverlayCanvas.height));
  element.style.setProperty("--overlay-z", String(item.zIndex));
}

function geometryLabel(item) {
  return `X ${item.x} / Y ${item.y} / ${item.width} x ${item.height}`;
}

export function installOverlayLayoutEditor({
  root,
  getLayout,
  onLayoutInput,
  onLayoutCommit,
  onInteractionChange = () => {},
} = {}) {
  const canvas = root?.querySelector?.(".overlay-custom-canvas");
  if (!canvas || typeof getLayout !== "function") return () => {};

  const abortController = new AbortController();
  const { signal } = abortController;
  let selectedId = normalizeCustomOverlayLayout(getLayout()).find((item) => item.enabled)?.id || "status";
  let interaction = null;

  root.classList.add("overlay-custom-editor");
  document.documentElement.classList.add("overlay-editor-mode");

  const toolbar = document.createElement("aside");
  toolbar.className = "overlay-editor-toolbar";
  toolbar.innerHTML = `<div class="overlay-editor-toolbar-title">
      <strong>実出力レイアウト編集</strong>
      <span>部品名をドラッグ / 右下でサイズ変更</span>
    </div>
    <output class="overlay-editor-geometry" aria-live="polite"></output>
    <span class="overlay-editor-save-status save-status">保存済み</span>
    <button type="button" data-overlay-editor-reset>初期配置</button>
    <button type="button" data-overlay-editor-finish>表示だけ確認</button>`;
  root.append(toolbar);

  const geometryOutput = toolbar.querySelector(".overlay-editor-geometry");

  function currentLayout() {
    return normalizeCustomOverlayLayout(getLayout());
  }

  function updateSelection(layout = currentLayout()) {
    const selected = layout.find((item) => item.id === selectedId) || layout[0];
    if (selected) selectedId = selected.id;
    for (const element of canvas.querySelectorAll("[data-overlay-layout-part]")) {
      element.classList.toggle("is-overlay-editor-selected", element.dataset.overlayLayoutPart === selectedId);
    }
    if (geometryOutput && selected) {
      geometryOutput.textContent = `${editorPartLabels[selected.id] || selected.id} · ${geometryLabel(selected)}`;
    }
  }

  function updateCanvas(layout) {
    for (const item of layout) {
      const element = canvas.querySelector(`[data-overlay-layout-part="${item.id}"]`);
      if (element) applyGeometry(element, item);
    }
    updateSelection(layout);
  }

  for (const element of canvas.querySelectorAll("[data-overlay-layout-part]")) {
    const id = element.dataset.overlayLayoutPart;
    const chrome = document.createElement("div");
    chrome.className = "overlay-editor-item-chrome";
    chrome.innerHTML = `<button type="button" class="overlay-editor-drag-handle" aria-label="${editorPartLabels[id] || id}を移動">
        <span>${editorPartLabels[id] || id}</span>
      </button>
      <button type="button" class="overlay-editor-resize-handle" aria-label="${editorPartLabels[id] || id}のサイズを変更"></button>`;
    element.append(chrome);

    const startInteraction = (mode, event) => {
      if (event.button !== 0) return;
      event.preventDefault();
      selectedId = id;
      const rect = canvas.getBoundingClientRect();
      interaction = {
        pointerId: event.pointerId,
        mode,
        startX: event.clientX,
        startY: event.clientY,
        viewportWidth: rect.width,
        viewportHeight: rect.height,
        startLayout: currentLayout(),
        latestLayout: currentLayout(),
      };
      event.currentTarget.setPointerCapture?.(event.pointerId);
      onInteractionChange(true);
      updateSelection(interaction.startLayout);
    };

    chrome.querySelector(".overlay-editor-drag-handle")?.addEventListener(
      "pointerdown",
      (event) => startInteraction("move", event),
      { signal },
    );
    chrome.querySelector(".overlay-editor-resize-handle")?.addEventListener(
      "pointerdown",
      (event) => startInteraction("resize", event),
      { signal },
    );
    element.addEventListener("pointerdown", () => {
      selectedId = id;
      updateSelection();
    }, { signal });
  }

  window.addEventListener("pointermove", (event) => {
    if (!interaction || event.pointerId !== interaction.pointerId) return;
    event.preventDefault();
    interaction.latestLayout = applyOverlayEditorDelta(
      interaction.startLayout,
      selectedId,
      interaction.mode,
      event.clientX - interaction.startX,
      event.clientY - interaction.startY,
      interaction.viewportWidth,
      interaction.viewportHeight,
    );
    onLayoutInput?.(interaction.latestLayout);
    updateCanvas(interaction.latestLayout);
  }, { signal });

  const finishInteraction = (event) => {
    if (!interaction || event.pointerId !== interaction.pointerId) return;
    const next = interaction.latestLayout;
    interaction = null;
    onLayoutCommit?.(next);
    onInteractionChange(false);
    updateCanvas(next);
  };
  window.addEventListener("pointerup", finishInteraction, { signal });
  window.addEventListener("pointercancel", finishInteraction, { signal });

  toolbar.querySelector("[data-overlay-editor-reset]")?.addEventListener("click", () => {
    const next = normalizeCustomOverlayLayout(defaultCustomOverlayLayout);
    onLayoutInput?.(next);
    onLayoutCommit?.(next);
    updateCanvas(next);
  }, { signal });

  toolbar.querySelector("[data-overlay-editor-finish]")?.addEventListener("click", () => {
    const url = new URL(location.href);
    url.searchParams.delete("edit");
    location.assign(url);
  }, { signal });

  updateCanvas(currentLayout());
  return () => {
    abortController.abort();
    document.documentElement.classList.remove("overlay-editor-mode");
  };
}
