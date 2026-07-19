import { renderOverlayPart } from "./overlay-parts.js";
import {
  customOverlayCanvas,
  defaultCustomOverlayLayout,
  normalizeCustomOverlayLayout,
} from "../lib/overlay-layout-state.js";

export { defaultCustomOverlayLayout, normalizeCustomOverlayLayout };

function percent(value, total) {
  return String(Number(((value / total) * 100).toFixed(4)));
}

export function renderCustomOverlayLayout(
  layout,
  args,
  context,
  partRenderer = renderOverlayPart,
) {
  const items = normalizeCustomOverlayLayout(layout).filter((item) => item.enabled);
  return `<main class="overlay-custom-canvas" data-overlay-canvas="1920x1080">
    ${items.map((item) => `<section
      class="overlay-custom-item overlay-custom-item-${item.id}"
      data-overlay-layout-part="${item.id}"
      style="--overlay-x:${percent(item.x, customOverlayCanvas.width)}%;--overlay-y:${percent(item.y, customOverlayCanvas.height)}%;--overlay-width:${percent(item.width, customOverlayCanvas.width)}%;--overlay-height:${percent(item.height, customOverlayCanvas.height)}%;--overlay-z:${item.zIndex};"
    >${partRenderer(item.id, args, context)}</section>`).join("")}
  </main>`;
}
