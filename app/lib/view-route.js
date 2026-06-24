const viewIds = new Set(["control", "control-v2", "sidecar", "overlay"]);

export function normalizeAppView(value) {
  return viewIds.has(value) ? value : "control";
}

export function resolveAppView(pathname = "/", search = "") {
  const params = new URLSearchParams(String(search || "").replace(/^\?/, ""));
  const requested = params.get("view");
  if (requested) return normalizeAppView(requested);
  if (String(pathname).startsWith("/overlay")) return "overlay";
  if (pathname === "/sidecar") return "sidecar";
  if (pathname === "/control-v2") return "control-v2";
  return "control";
}

export function isAppShellPath(pathname = "/") {
  return pathname === "/control"
    || pathname === "/control-v2"
    || pathname === "/sidecar"
    || pathname === "/overlay"
    || String(pathname).startsWith("/overlay/");
}