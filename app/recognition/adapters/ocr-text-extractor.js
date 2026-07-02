import { createMaaOnnxOcrTextExtractor } from "./maa-onnx-ocr-adapter.js";
import { createGlmOcrTextExtractor } from "./glm-ocr-adapter.js";

function hasText(frame) {
  if (typeof frame?.text === "string" && frame.text.trim()) return true;
  return Array.isArray(frame?.ocrResults) && frame.ocrResults.some((item) => item?.text?.trim?.());
}

function resultKey(item = {}) {
  const roi = item.roi ? `${item.roi.x}:${item.roi.y}:${item.roi.width}:${item.roi.height}` : "_";
  return `${item.regionId || "_"}:${item.text || ""}:${roi}`;
}

export function mergeOcrFrames(baseFrame, frames = [], { engine = "merged", minConfidence = 0.2 } = {}) {
  const byKey = new Map();
  for (const frame of frames) {
    for (const item of frame?.ocrResults || []) {
      if (!item?.text?.trim?.()) continue;
      if (item.confidence != null && Number(item.confidence) < minConfidence) continue;
      const key = resultKey(item);
      const previous = byKey.get(key);
      if (!previous || Number(item.confidence || 0) > Number(previous.confidence || 0)) byKey.set(key, item);
    }
  }
  const ocrResults = [...byKey.values()];
  return {
    ...baseFrame,
    text: ocrResults.map((item) => item.text).join(" "),
    ocrResults,
    ocrEngine: engine,
  };
}

export function createMergedTextExtractor(extractors = [], { engine = "merged" } = {}) {
  return {
    async extract(frame, context = {}) {
      const frames = [];
      const errors = [];
      for (const extractor of extractors) {
        try {
          const nextFrame = await extractor.extract(frame, context);
          if (hasText(nextFrame)) frames.push(nextFrame);
        } catch (error) {
          errors.push(error instanceof Error ? error.message : String(error));
        }
      }
      if (frames.length) return mergeOcrFrames(frame, frames, { engine });
      return errors.length ? { ...frame, ocrError: errors.join("; ") } : frame;
    },
  };
}

export function createFallbackTextExtractor(extractors = []) {
  return {
    async extract(frame, context = {}) {
      const errors = [];
      for (const extractor of extractors) {
        try {
          const nextFrame = await extractor.extract(frame, context);
          if (hasText(nextFrame)) return nextFrame;
        } catch (error) {
          errors.push(error instanceof Error ? error.message : String(error));
        }
      }
      return errors.length ? { ...frame, ocrError: errors.join("; ") } : frame;
    },
  };
}

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
