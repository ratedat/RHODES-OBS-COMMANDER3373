let overlayAutoScrollFrame = null;
let overlayAutoScrollResizeObserver = null;

export function cancelOverlayAutoScroll() {
  overlayAutoScrollResizeObserver?.disconnect();
  overlayAutoScrollResizeObserver = null;
  if (overlayAutoScrollFrame !== null) {
    clearInterval(overlayAutoScrollFrame);
    overlayAutoScrollFrame = null;
  }
}

export function setupOverlayAutoScroll(root = document) {
  cancelOverlayAutoScroll();
  const scrollers = [...root.querySelectorAll("[data-autoscroll]")];
  if (!scrollers.length) return;
  const groupViewports = [...root.querySelectorAll("[data-autoscroll-groups]")]
    .filter((element) => element.firstElementChild?.classList.contains("special-overlay-groups"));
  const createEntries = () => {
    for (const element of [...scrollers, ...groupViewports]) {
      if (element.firstElementChild) element.firstElementChild.style.transform = "translateY(0px)";
    }
    const groupModes = new Map(groupViewports.map((viewport) => {
      viewport.classList.remove("special-overlay-scroll-all");
      const children = [...viewport.querySelectorAll("[data-autoscroll]")];
      // When wrapping leaves less than one card per group, scroll the whole
      // collection instead of moving cards through unreadably small strips.
      const scrollAll = children.some((child) => {
        const content = child.firstElementChild;
        const firstCard = content?.firstElementChild;
        return firstCard && child.clientHeight < Math.min(firstCard.offsetHeight, content.scrollHeight);
      });
      viewport.classList.toggle("special-overlay-scroll-all", scrollAll);
      return [viewport, scrollAll];
    }));
    const activeScrollers = scrollers.filter((element) => !groupModes.get(element.closest("[data-autoscroll-groups]")));
    activeScrollers.push(...groupViewports.filter((element) => groupModes.get(element)));
    return activeScrollers.map((element, index) => {
      const content = element.firstElementChild;
      return {
        element,
        content,
        offset: 0,
        direction: 1,
        last: performance.now(),
        pauseUntil: performance.now() + 900 + index * 700,
        speed: Number.isFinite(Number(element.dataset.scrollSpeed)) ? Number(element.dataset.scrollSpeed) : 14,
      };
    }).filter((entry) => entry.content);
  };
  let entries = createEntries();
  if (!entries.length) return;
  if (groupViewports.length && typeof ResizeObserver !== "undefined") {
    overlayAutoScrollResizeObserver = new ResizeObserver(() => { entries = createEntries(); });
    groupViewports.forEach((element) => overlayAutoScrollResizeObserver.observe(element));
  }
  overlayAutoScrollFrame = setInterval(() => {
    const now = performance.now();
    for (const entry of entries) {
      const max = Math.max(0, entry.content.scrollHeight - entry.element.clientHeight);
      if (max <= 1) {
        entry.offset = 0;
        entry.content.style.transform = "translateY(0px)";
        entry.last = now;
        continue;
      }
      const delta = Math.min(120, now - entry.last);
      entry.last = now;
      if (now < entry.pauseUntil) continue;
      entry.offset += entry.direction * entry.speed * delta / 1000;
      if (entry.offset >= max) {
        entry.offset = max;
        entry.direction = -1;
        entry.pauseUntil = now + 1600;
      } else if (entry.offset <= 0) {
        entry.offset = 0;
        entry.direction = 1;
        entry.pauseUntil = now + 1200;
      }
      entry.content.style.transform = `translateY(${-entry.offset}px)`;
    }
  }, 80);
}
