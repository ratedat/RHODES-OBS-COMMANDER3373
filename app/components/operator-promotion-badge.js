export function renderOperatorPromotionBadge(item) {
  return Number(item?.promotionLevel) >= 2
    ? `<span class="operator-promotion-badge" title="昇進2" aria-label="昇進2"><img src="/assets/ui/operator-promotions/elite-2.png" alt="" aria-hidden="true" /></span>`
    : "";
}

export function renderOperatorPortrait(item, imageSource) {
  const promotionClass = Number(item?.promotionLevel) >= 2 ? " is-elite-two" : "";
  return `<div class="operator-portrait${promotionClass}"><img class="operator-portrait-image" src="${imageSource}" alt="" />${renderOperatorPromotionBadge(item)}</div>`;
}
