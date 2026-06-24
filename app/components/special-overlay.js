import { html } from "../lib/format.js";
import { specialEffectImageSrc } from "../lib/media.js";

function renderSpecialOverlayItems(items) {
  return `<div class="special-overlay-grid">
    ${items.map((item) => {
      const imageSrc = specialEffectImageSrc(item);
      const label = item.groupLabel && item.groupLabel !== item.slotLabel ? `${item.slotLabel} / ${item.groupLabel}` : item.slotLabel;
      return `<div class="special-overlay-chip" title="${html(item.effect)}">
        ${imageSrc ? `<img src="${html(imageSrc)}" alt="" />` : `<span class="special-overlay-fallback">${html((item.name || "?").slice(0, 1))}</span>`}
        <div><span>${html(label || "特殊")}</span><strong>${html(item.name)}</strong></div>
      </div>`;
    }).join("")}
  </div>`;
}

export function renderSpecialOverlayBlock(items, mode, speedKey, getOverlayScrollSpeed) {
  if (!items.length) return "";
  const isCompact = mode === "compact";
  return `<section class="${isCompact ? "compact-section compact-special-section" : "stream-special-section"}">
    <div class="${isCompact ? "compact-section-head" : "stream-section-head"}"><span>Special</span><span>${items.length}</span></div>
    <div class="stream-scroll ${isCompact ? "compact-special-scroll" : "stream-special-scroll"}" data-autoscroll data-scroll-speed="${getOverlayScrollSpeed(speedKey)}">
      ${renderSpecialOverlayItems(items)}
    </div>
  </section>`;
}