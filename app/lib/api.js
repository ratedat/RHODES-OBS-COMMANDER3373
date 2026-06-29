export const stateUrl = "/api/state";
export const masterUrl = "/api/master";
export const resetStateUrl = "/api/state/reset";
export const recognitionScanUrl = "/api/recognition/scan";
export const recognitionScanCancelUrl = "/api/recognition/scan/cancel";
export const recognitionScanStatusUrl = "/api/recognition/scan/status";
export const adbDetectUrl = "/api/adb/detect";
export const adbTestUrl = "/api/adb/test";
export const adbSelectPathUrl = "/api/adb/select-path";
export const hypervisorStatusUrl = "/api/system/hypervisor";
export const glmOcrStatusUrl = "/api/ocr/glm/status";
export const glmOcrInstallUrl = "/api/ocr/glm/install";
export const glmOcrUninstallUrl = "/api/ocr/glm/uninstall";
export const glmOcrOllamaStatusUrl = "/api/ocr/glm/ollama/status";
export const glmOcrOllamaInstallUrl = "/api/ocr/glm/ollama/install";
export const glmOcrOllamaStartUrl = "/api/ocr/glm/ollama/start";
export const glmOcrOllamaUninstallUrl = "/api/ocr/glm/ollama/uninstall";

export async function apiJson(url, options) {
  const response = await fetch(url, options);
  const payload = await response.json().catch(() => null);
  if (!response.ok) throw new Error(payload?.error || `${response.status} ${response.statusText}`);
  return payload;
}
