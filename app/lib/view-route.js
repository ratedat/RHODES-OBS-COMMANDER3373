const viewIds = new Set(["sidecar", "overlay", "licenses"]);

export function normalizeAppView(value) {
  return viewIds.has(value) ? value : "sidecar";
}

export function resolveAppView(pathname = "/", search = "") {
  const params = new URLSearchParams(String(search || "").replace(/^\?/, ""));
  const requested = params.get("view");
  if (requested) return normalizeAppView(requested);
  if (String(pathname).startsWith("/overlay")) return "overlay";
  if (pathname === "/sidecar") return "sidecar";
  if (pathname === "/licenses") return "licenses";
  return "sidecar";
}

export function isAppShellPath(pathname = "/") {
  return pathname === "/sidecar"
    || pathname === "/licenses"
    || pathname === "/overlay"
    || String(pathname).startsWith("/overlay/");
}
