import { assetUrl, html, stars } from "../lib/format.js";

export function renderOverlayCompact({ campaign, squad, option, performance, activeEffects, relics, operators, specialFields, special, difficultyGrade }, context) {
  const specialTags = context.getSpecialTags(specialFields, special, { overlay: true });
  const specialItems = context.getOverlaySpecialEffects(campaign.id, specialFields, special);
  const flags = context.getBossFlagEntries(campaign.id);
  return `
    <section class="compact-overlay-shell">
      <header class="compact-head">
        <div class="compact-title-block">
          <div class="compact-kicker">IS#${html(campaign.number)}</div>
          <div class="compact-title">${html(campaign.title)}</div>
        </div>
        <div class="compact-counts">
          <span>秘宝 ${relics.length}</span><span>招集 ${operators.length}</span><span>Boss ${flags.length}</span>
        </div>
      </header>
      <div class="compact-row"><span>分隊</span><strong>${html(squad?.name || "未選択")}</strong></div>
      ${option?.label ? `<div class="compact-row compact-muted"><span>効果</span><strong>${html(option.label)}</strong></div>` : ""}
      ${performance ? `<div class="compact-row compact-muted"><span>演目</span><strong>${html(performance.title || performance.name)}</strong></div>` : ""}
      <div class="compact-chip-row">
        <span class="tag accent">${html(difficultyGrade?.label || "等級未選択")}</span>
        <span class="tag">Tier ${html(context.getDifficultyTierLabel())}</span>
        ${specialTags.map((item) => `<span class="tag info">${html(item.label)} ${html(item.value)}</span>`).join("")}
      </div>
      ${context.renderSpecialOverlayBlock(specialItems, "compact", "compactRelicScrollSpeed")}
      ${activeEffects.length ? `<section class="compact-section compact-effects-section">
        <div class="compact-section-head"><span>Effects</span><span>${activeEffects.length}</span></div>
        <div class="stream-scroll compact-effect-scroll" data-autoscroll data-scroll-speed="${context.getOverlayScrollSpeed("compactRelicScrollSpeed")}">
          ${context.renderEffectList(activeEffects, "compact-effect-list", "発動効果なし")}
        </div>
      </section>` : ""}
      <section class="compact-section">
        <div class="compact-section-head"><span>Relics</span><span>${relics.length}</span></div>
        <div class="stream-scroll compact-relic-scroll" data-autoscroll data-scroll-speed="${context.getOverlayScrollSpeed("compactRelicScrollSpeed")}">
          <div class="compact-relic-strip">
            ${relics.length ? relics.map((item) => `<img src="${html(assetUrl(item.image?.localPath))}" title="${html(item.name)}" alt="" />`).join("") : `<span class="compact-empty">なし</span>`}
          </div>
        </div>
      </section>
      <section class="compact-section">
        <div class="compact-section-head"><span>Operators</span><span>${operators.length}</span></div>
        <div class="compact-operator-strip">
          ${operators.length ? operators.slice(0, 8).map((item) => `<div class="compact-operator"><img src="${html(assetUrl(item.image?.localPath))}" alt="" /><span>${html(item.name)}</span><strong>${stars(item.rarity)}</strong></div>`).join("") : `<span class="compact-empty">なし</span>`}
          ${operators.length > 8 ? `<span class="compact-more">+${operators.length - 8}</span>` : ""}
        </div>
      </section>
      ${flags.length ? `<section class="compact-section"><div class="compact-section-head"><span>Boss</span><span>${flags.length}</span></div><div class="compact-boss-list">${flags.slice(0, 4).map((flag) => context.renderBossChip(flag)).join("")}${flags.length > 4 ? `<span class="compact-more">+${flags.length - 4}</span>` : ""}</div></section>` : ""}
    </section>
  `;
}

export function renderOverlayDense({ campaign, squad, option, performance, activeEffects, relics, operators, specialFields, special, difficultyGrade, orientation }, context) {
  const specialTags = context.getSpecialTags(specialFields, special, { overlay: true });
  const specialItems = context.getOverlaySpecialEffects(campaign.id, specialFields, special);
  const flags = context.getBossFlagEntries(campaign.id);
  return `
    <section class="stream-overlay-shell stream-${orientation}">
      <header class="stream-head">
        <div>
          <div class="stream-kicker">IS#${html(campaign.number)} / ${html(context.mode || "manual")}</div>
          <div class="stream-title">${html(campaign.title)}</div>
        </div>
        <div class="stream-counts">
          <span>秘宝 ${relics.length}</span><span>招集 ${operators.length}</span><span>Boss ${flags.length}</span>
        </div>
      </header>
      <section class="stream-run">
        <div class="stream-line"><span>分隊</span><strong>${html(squad?.name || "未選択")}</strong></div>
        ${option?.label || option?.effect ? `<div class="stream-note">${html(option?.label || option?.effect)}</div>` : ""}
        ${performance ? `<div class="stream-note"><strong>演目</strong> ${html(performance.title || performance.name)}</div>` : ""}
        <div class="stream-chip-row">
          <span class="tag accent">${html(difficultyGrade?.label || "等級未選択")}</span>
          <span class="tag">Tier ${html(context.getDifficultyTierLabel())}</span>
          ${specialTags.map((item) => `<span class="tag info">${html(item.label)} ${html(item.value)}</span>`).join("")}
          ${flags.map((flag) => context.renderBossChip(flag)).join("")}
        </div>
        ${context.renderSpecialOverlayBlock(specialItems, "stream", orientation + "RelicScrollSpeed")}
        ${activeEffects.length ? `<div class="stream-scroll stream-effect-scroll" data-autoscroll data-scroll-speed="${context.getOverlayScrollSpeed(`${orientation}RelicScrollSpeed`)}">
          ${context.renderEffectList(activeEffects, "stream-effect-list", "発動効果なし")}
        </div>` : ""}
      </section>
      <section class="stream-panel stream-relic-panel">
        <div class="stream-section-head"><span>Relics</span><strong>${relics.length}</strong></div>
        <div class="stream-scroll stream-relic-scroll" data-autoscroll data-scroll-speed="${context.getOverlayScrollSpeed(`${orientation}RelicScrollSpeed`)}">
          <div class="stream-relic-grid">
            ${relics.length ? relics.map((item) => `<div class="stream-relic-tile" title="${html(context.relicEffectForDisplay(item))}"><img src="${html(assetUrl(item.image?.localPath))}" alt="" /><strong>${html(item.name)}</strong></div>`).join("") : `<div class="stream-empty">秘宝なし</div>`}
          </div>
        </div>
      </section>
      <section class="stream-panel stream-operator-panel">
        <div class="stream-section-head"><span>Operators</span><strong>${operators.length}</strong></div>
        <div class="stream-scroll stream-operator-scroll" data-autoscroll data-scroll-speed="${context.getOverlayScrollSpeed(`${orientation}OperatorScrollSpeed`)}">
          <div class="stream-operator-grid">
            ${operators.length ? operators.map((item) => `<div class="stream-operator-tile"><img src="${html(assetUrl(item.image?.localPath))}" alt="" /><div><strong>${html(item.name)}</strong><span>${stars(item.rarity)} / ${html(item.class || "-")}</span></div></div>`).join("") : `<div class="stream-empty">未招集</div>`}
          </div>
        </div>
      </section>
    </section>
  `;
}