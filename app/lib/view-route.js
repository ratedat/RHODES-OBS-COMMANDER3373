const viewIds = new Set(["control-v2", "sidecar", "overlay", "licenses"]);

export function normalizeAppView(value) {
  if (value === "control") return "control-v2";
  return viewIds.has(value) ? value : "sidecar";
}

export function resolveAppView(pathname = "/", search = "") {
  const params = new URLSearchParams(String(search || "").replace(/^\?/, ""));
  const requested = params.get("view");
  if (requested) return normalizeAppView(requested);
  if (pathname === "/control" || pathname === "/control-v2") return "control-v2";
  if (String(pathname).startsWith("/overlay")) return "overlay";
  if (pathname === "/sidecar") return "sidecar";
  if (pathname === "/licenses") return "licenses";
  return "sidecar";
}

export function isAppShellPath(pathname = "/") {
  return pathname === "/control"
    || pathname === "/control-v2"
    || pathname === "/sidecar"
    || pathname === "/licenses"
    || pathname === "/overlay"
    || String(pathname).startsWith("/overlay/");
}
