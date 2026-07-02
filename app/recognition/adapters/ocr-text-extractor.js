import { createMaaOnnxOcrTextExtractor } from "./maa-onnx-ocr-adapter.js";
import { createGlmOcrTextExtractor } from "./glm-ocr-adapter.js";

export function createDefaultOcrTextExtractor({ engine = process.env.RHODES_OCR_ENGINE || "auto", glmOcrPythonPath, glmOcrEnv } = {}) {
  const normalized = String(engine || "auto").toLowerCase();
  const glmOcrOptions = { pythonPath: glmOcrPythonPath, extraEnv: glmOcrEnv };
  if (["auto", "profile", "maa-ocr", "maa-onnx", "maa", "onnx"].includes(normalized)) {
    return createMaaOnnxOcrTextExtractor({ required: true });
  }
  if (["glm-ocr", "glm"].includes(normalized)) return createGlmOcrTextExtractor({ required: true, ...glmOcrOptions });
  return createMaaOnnxOcrTextExtractor({ required: true });
}

export function createProfileAwareTextExtractor({ defaultExtractor, profileExtractors = {} } = {}) {
  return {
    async extract(frame, context = {}) {
      const profileId = context.profile?.id;
      const extractor = profileExtractors[profileId] || defaultExtractor;
      if (!extractor?.extract) return frame;
      return extractor.extract(frame, context);
    },
  };
}

export function createProfileAwareOcrTextExtractor({ defaultEngine = process.env.RHODES_OCR_ENGINE || "auto", profileEngines = {}, glmOcrPythonPath, glmOcrEnv } = {}) {
  const byEngine = new Map();
  const extractorFor = (engine) => {
    const key = String(engine || "auto").toLowerCase();
    if (!byEngine.has(key)) byEngine.set(key, createDefaultOcrTextExtractor({ engine: key, glmOcrPythonPath, glmOcrEnv }));
    return byEngine.get(key);
  };
  const profileExtractors = Object.fromEntries(
    Object.entries(profileEngines || {}).map(([profileId, engine]) => [profileId, extractorFor(engine)]),
  );
  return createProfileAwareTextExtractor({
    defaultExtractor: extractorFor(defaultEngine),
    profileExtractors,
  });
}
