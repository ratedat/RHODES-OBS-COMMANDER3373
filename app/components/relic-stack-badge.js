import { html } from "../lib/format.js";

export function renderRelicStackBadge(item) {
  const count = Number(item?.stackCount);
  if (!Number.isSafeInteger(count) || count <= 0) return "";
  return `<span class="relic-stack-badge" aria-label="スタック${html(count)}" title="スタック数 ${html(count)}">×${html(count)}</span>`;
}
