export function renderRelicUsedBadge(item) {
  if (!item?.used) return "";
  return '<b class="relic-used-badge" role="img" aria-label="使用済" title="使用済"><i class="relic-used-icon" aria-hidden="true">✓</i></b>';
}
