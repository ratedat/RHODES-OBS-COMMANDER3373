export const customOverlayCanvas = Object.freeze({ width: 1920, height: 1080 });

const minimumSize = Object.freeze({ width: 160, height: 80 });

export const defaultCustomOverlayLayout = Object.freeze([
  { id: "status", enabled: true, x: 40, y: 36, width: 1200, height: 120, zIndex: 1 },
  { id: "relics", enabled: true, x: 40, y: 850, width: 1320, height: 190, zIndex: 6 },
  { id: "operators", enabled: true, x: 1460, y: 260, width: 420, height: 620, zIndex: 5 },
  { id: "effects", enabled: true, x: 40, y: 420, width: 520, height: 320, zIndex: 3 },
  { id: "bosses", enabled: true, x: 600, y: 420, width: 760, height: 220, zIndex: 4 },
  { id: "special", enabled: true, x: 1280, y: 36, width: 600, height: 180, zIndex: 2 },
]);

function clampInteger(value, minimum, maximum, fallback) {
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) return fallback;
  return Math.min(maximum, Math.max(minimum, Math.round(numeric)));
}

export function normalizeCustomOverlayLayout(value) {
  const supplied = new Map();
  if (Array.isArray(value)) {
    for (const item of value) {
      const id = String(item?.id || "").trim().toLowerCase();
      if (defaultCustomOverlayLayout.some((known) => known.id === id)) supplied.set(id, item);
    }
  }

  return defaultCustomOverlayLayout.map((fallback) => {
    const source = supplied.get(fallback.id) || fallback;
    const width = clampInteger(source.width, minimumSize.width, customOverlayCanvas.width, fallback.width);
    const height = clampInteger(source.height, minimumSize.height, customOverlayCanvas.height, fallback.height);
    return {
      id: fallback.id,
      enabled: source.enabled !== false && source.enabled !== "false",
      x: clampInteger(source.x, 0, customOverlayCanvas.width - width, fallback.x),
      y: clampInteger(source.y, 0, customOverlayCanvas.height - height, fallback.y),
      width,
      height,
      zIndex: clampInteger(source.zIndex, 1, defaultCustomOverlayLayout.length, fallback.zIndex),
    };
  });
}
