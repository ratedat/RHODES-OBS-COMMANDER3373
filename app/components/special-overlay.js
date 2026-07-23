import { html } from "../lib/format.js";
import { specialEffectImageSrc } from "../lib/media.js";

function renderSpecialOverlayItems(items) {
  return `<div class="special-overlay-grid">
    ${items.map((item) => {
      const imageSrc = specialEffectImageSrc(item);
      const typeLabel = item.groupLabel && item.groupLabel !== item.slotLabel ? `${item.slotLabel} / ${item.groupLabel}` : item.slotLabel;
      const label = [item.activationLabel, typeLabel].filter(Boolean).join(" / ");
      return `<div class="special-overlay-chip" title="${html(item.effect)}">
        ${imageSrc ? `<img src="${html(imageSrc)}" alt="" />` : `<span class="special-overlay-fallback">${html((item.name || "?").slice(0, 1))}</span>`}
        <div class="special-overlay-chip-copy"><span>${html(label || "特殊")}</span><strong>${html(item.name)}</strong>${item.effect ? `<small>${html(item.effect)}</small>` : ""}</div>
      </div>`;
    }).join("")}
  </div>`;
}

function groupSpecialOverlayItems(items) {
  const groups = new Map();
  for (const item of items) {
    const id = item.overlayGroupId || "default";
    if (!groups.has(id)) {
      groups.set(id, {
        id,
        label: item.overlayGroupLabel || "",
        unit: item.overlayGroupUnit || "枚",
        items: [],
      });
    }
    groups.get(id).items.push(item);
  }
  return [...groups.values()];
}

function renderSpecialOverlayGroups(items) {
  const groups = groupSpecialOverlayItems(items);
  if (groups.length === 1 && groups[0].id === "default") return renderSpecialOverlayItems(items);
  return `<div class="special-overlay-groups">
    ${groups.map((group) => {
      const count = group.items.reduce((sum, item) => sum + Math.max(1, Number(item.quantity) || 1), 0);
      const classId = String(group.id).replace(/[^a-z0-9_-]/gi, "-");
      return `<section class="special-overlay-group special-overlay-group-${html(classId)}">
        <header><strong>${html(group.label || "特殊値")}</strong><span>${count}${html(group.unit)}</span></header>
        ${renderSpecialOverlayItems(group.items)}
      </section>`;
    }).join("")}
  </div>`;
}

export function renderSpecialOverlayBlock(items, mode, speedKey, getOverlayScrollSpeed) {
  if (!items.length) return "";
  const isCompact = mode === "compact";
  return `<section class="${isCompact ? "compact-section compact-special-section" : "stream-special-section"}">
    <div class="${isCompact ? "compact-section-head" : "stream-section-head"}"><span>Special</span><span>${items.length}</span></div>
    <div class="stream-scroll ${isCompact ? "compact-special-scroll" : "stream-special-scroll"}" data-autoscroll data-scroll-speed="${getOverlayScrollSpeed(speedKey)}">
      ${renderSpecialOverlayGroups(items)}
    </div>
  </section>`;
}
