import { execFile } from "node:child_process";
import nodeFs from "node:fs";
import path from "node:path";

import { buildMaaOcrAssetManifest } from "../app/domain/recognition/maa-ocr-assets.js";
import { resolveOcrPythonExecutable } from "../app/recognition/adapters/ocr-python-resolver.js";

const root = process.cwd();
const strict = process.argv.includes("--strict");
const pythonPath = resolveOcrPythonExecutable(process.env, process.env.USERPROFILE || process.env.HOME || "", root);

const probeScript = String.raw`
import importlib.util
import json
import sys

modules = {}
for name in ["onnxruntime", "numpy", "PIL", "glmocr"]:
    modules[name] = importlib.util.find_spec(name) is not None
versions = {}
for name in ["onnxruntime", "numpy", "PIL", "glmocr"]:
    if not modules[name]:
        continue
    try:
        module = __import__(name)
        versions[name] = getattr(module, "__version__", None)
    except Exception as exc:
        versions[name] = f"import-error: {exc}"
print(json.dumps({"python": sys.executable, "modules": modules, "versions": versions}, ensure_ascii=False))
`;

function runPythonProbe() {
  return new Promise((resolve) => {
    execFile(pythonPath, ["-c", probeScript], { encoding: "utf8", windowsHide: true, timeout: 30000 }, (error, stdout, stderr) => {
      if (error) {
        resolve({ ok: false, python: pythonPath, error: error.message, stderr });
        return;
      }
      try {
        resolve({ ok: true, ...JSON.parse(stdout.trim()) });
      } catch (parseError) {
        resolve({ ok: false, python: pythonPath, error: parseError.message, stdout, stderr });
      }
    });
  });
}

const python = await runPythonProbe();
const maaAssets = buildMaaOcrAssetManifest({
  includeModels: true,
  exists: (localPath) => nodeFs.existsSync(path.join(root, localPath)),
});
const summary = {
  ok: Boolean(python.ok && python.modules?.onnxruntime && python.modules?.numpy && python.modules?.PIL),
  python,
  glmOcr: {
    present: Boolean(python.modules?.glmocr),
    version: python.versions?.glmocr || null,
    engineValues: ["glm-ocr"],
  },
  maaAssets: maaAssets.assets.map(({ id, role, locale, localPath, present, model }) => ({ id, role, locale, localPath, present, model: Boolean(model) })),
};

console.log(JSON.stringify(summary, null, 2));

if (strict && !summary.ok) {
  process.exitCode = 1;
}
