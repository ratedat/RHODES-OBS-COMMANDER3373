export function renderOperatorPromotionBadge(item) {
  return Number(item?.promotionLevel) >= 2
    ? `<span class="operator-promotion-badge">昇進2</span>`
    : "";
}
