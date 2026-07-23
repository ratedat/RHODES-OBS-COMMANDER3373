import { assetUrl, html, stars } from "../lib/format.js";

export function renderRelicControlRow(item, active, effectText, meta = {}) {
  const autoOnly = meta.template && !meta.manual;
  const excluded = Boolean(meta.excluded);
  const usedButton = meta.supportsUsedFlag && active
    ? `<button type="button" class="choice-used-button ${meta.used ? "active" : ""}" data-action="toggle-relic-used" data-id="${html(item.id)}" aria-pressed="${meta.used ? "true" : "false"}">${meta.used ? "使用済" : "未使用"}</button>`
    : "";
  const excludeButton = meta.showExclude === false ? "" : `<button type="button" class="choice-exclude-button ${excluded ? "active" : ""}" data-action="toggle-relic-excluded" data-id="${html(item.id)}" aria-pressed="${excluded ? "true" : "false"}">${excluded ? "除外中" : "表示除外"}</button>`;
  const badges = [
    autoOnly ? '<span class="item-badge template">自動</span>' : '',
    meta.manual && meta.template ? '<span class="item-badge template">手動+自動</span>' : '',
    excluded ? '<span class="item-badge excluded">除外</span>' : '',
    meta.used ? '<span class="item-badge used">使用済</span>' : '',
  ].filter(Boolean).join("");
  return `
    <div class="item-row relic-choice ${active ? "active" : ""} ${autoOnly ? "template-active" : ""} ${excluded ? "choice-excluded" : ""}">
      <button type="button" class="item-choice-button" data-action="toggle-relic" data-id="${html(item.id)}" aria-pressed="${active ? "true" : "false"}">
        <img class="item-thumb" src="${html(assetUrl(item.image?.localPath))}" alt="" loading="lazy" />
        <span class="item-choice-main">
          <span class="item-title">No.${html(item.number)} ${html(item.name)}</span>
          <span class="item-meta">${html(item.category || "")}</span>
          <span class="item-effect">${html(effectText)}</span>
        </span>
      </button>
      <div class="item-badges">${badges}</div>
      ${usedButton}
      ${excludeButton}
    </div>
  `;
}

export function renderOperatorControlRow(item, active, meta = {}) {
  const excluded = Boolean(meta.excluded);
  const countBadge = active && Number(meta.count) > 1
    ? `<span class="operator-count-badge">×${html(Math.trunc(Number(meta.count)))}</span>`
    : "";
  const excludeButton = meta.showExclude === false ? "" : `<button type="button" class="choice-exclude-button ${excluded ? "active" : ""}" data-action="toggle-operator-excluded" data-id="${html(item.id)}" aria-pressed="${excluded ? "true" : "false"}">${excluded ? "除外中" : "表示除外"}</button>`;
  return `
    <div class="item-row operator-choice ${active ? "active" : ""} ${excluded ? "choice-excluded" : ""}">
      <button type="button" class="item-choice-button" data-action="toggle-operator" data-id="${html(item.id)}" aria-pressed="${active ? "true" : "false"}">
        <img class="item-thumb" src="${html(assetUrl(item.image?.localPath))}" alt="" loading="lazy" />
        <span class="item-choice-main">
          <span class="item-title">${html(item.name)} ${countBadge} <span class="stars">${stars(item.rarity)}</span></span>
          <span class="item-meta">${html(item.class)} / ${html(item.branch)}${item.hiddenByDefault ? " / 日本未実装" : ""}</span>
        </span>
      </button>
      ${excluded ? '<div class="item-badges"><span class="item-badge excluded">除外</span></div>' : '<div class="item-badges"></div>'}
      ${excludeButton}
    </div>
  `;
}
