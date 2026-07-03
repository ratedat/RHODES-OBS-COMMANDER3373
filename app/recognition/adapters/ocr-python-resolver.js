import nodeFs from "node:fs";
import os from "node:os";
import path from "node:path";

export function resolveOcrPythonExecutable(env = process.env, homeDir = os.homedir(), cwd = process.cwd()) {
  const explicit = env.RHODES_GLM_OCR_PYTHON || env.RHODES_PYTHON || env.PYTHON;
  if (explicit) return explicit;
  const candidates = [
    path.join(cwd, ".venv-glm-ocr", "Scripts", "python.exe"),
    path.join(homeDir, "AppData", "Local", "Programs", "Python", "Python312", "python.exe"),
  ];
  return candidates.find((candidate) => nodeFs.existsSync(candidate)) || "python";
}
