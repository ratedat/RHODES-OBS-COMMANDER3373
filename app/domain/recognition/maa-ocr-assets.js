export const MAA_OCR_SOURCE = Object.freeze({
  project: "MaaAssistantArknights/MaaAssistantArknights",
  branch: "dev-v2",
  license: "AGPL-3.0-only",
  rawBaseUrl: "https://raw.githubusercontent.com/MaaAssistantArknights/MaaAssistantArknights/dev-v2",
});

export const MAA_OCR_ASSETS = Object.freeze([
  {
    id: "common.ocr_config",
    role: "config",
    locale: "common",
    rawPath: "resource/ocr_config.json",
    localPath: "third_party/maa/resource/ocr_config.json",
  },
  {
    id: "jp.ocr_config",
    role: "config",
    locale: "YoStarJP",
    rawPath: "resource/global/YoStarJP/resource/ocr_config.json",
    localPath: "third_party/maa/resource/global/YoStarJP/resource/ocr_config.json",
  },
  {
    id: "jp.recruitment",
    role: "operator-data",
    locale: "YoStarJP",
    rawPath: "resource/global/YoStarJP/resource/recruitment.json",
    localPath: "third_party/maa/resource/global/YoStarJP/resource/recruitment.json",
  },
  {
    id: "jp.tasks",
    role: "ocr-rules",
    locale: "YoStarJP",
    rawPath: "resource/global/YoStarJP/resource/tasks/tasks.json",
    localPath: "third_party/maa/resource/global/YoStarJP/resource/tasks/tasks.json",
  },
  {
    id: "common.tasks",
    role: "ocr-rules",
    locale: "common",
    rawPath: "resource/tasks/tasks.json",
    localPath: "third_party/maa/resource/tasks/tasks.json",
  },
  ...[
    ["base", "base"],
    ["sami", "Sami"],
    ["sarkaz", "Sarkaz"],
    ["jie-garden", "JieGarden"],
  ].map(([id, file]) => ({
    id: `roguelike.${id}.tasks`,
    role: "ocr-rules",
    locale: "common",
    rawPath: `resource/tasks/Roguelike/${file}.json`,
    localPath: `third_party/maa/resource/tasks/Roguelike/${file}.json`,
  })),
  ...[
    ["elite-0", "RoguelikeRecruitElite0.png"],
    ["elite-1", "RoguelikeRecruitElite1.png"],
    ["elite-2", "RoguelikeRecruitElite2.png"],
    ["ocr-flag", "RoguelikeRecruitOcrFlag.png"],
  ].map(([id, file]) => ({
    id: `roguelike.recruit.${id}`,
    role: "template",
    locale: "common",
    rawPath: `resource/template/Roguelike/base/${file}`,
    localPath: `third_party/maa/resource/template/Roguelike/base/${file}`,
  })),
  {
    id: "common.det.onnx",
    role: "detector-model",
    locale: "common",
    model: true,
    rawPath: "resource/PaddleOCR/det/inference.onnx",
    localPath: "third_party/maa/resource/PaddleOCR/det/inference.onnx",
  },
  {
    id: "common.det.version",
    role: "detector-version",
    locale: "common",
    rawPath: "resource/PaddleOCR/det/version.txt",
    localPath: "third_party/maa/resource/PaddleOCR/det/version.txt",
  },
  {
    id: "common.rec.onnx",
    role: "recognizer-model",
    locale: "common",
    model: true,
    rawPath: "resource/PaddleOCR/rec/inference.onnx",
    localPath: "third_party/maa/resource/PaddleOCR/rec/inference.onnx",
  },
  {
    id: "common.rec.keys",
    role: "recognizer-keys",
    locale: "common",
    rawPath: "resource/PaddleOCR/rec/keys.txt",
    localPath: "third_party/maa/resource/PaddleOCR/rec/keys.txt",
  },
  {
    id: "common.rec.version",
    role: "recognizer-version",
    locale: "common",
    rawPath: "resource/PaddleOCR/rec/version.txt",
    localPath: "third_party/maa/resource/PaddleOCR/rec/version.txt",
  },
  {
    id: "jp.rec.onnx",
    role: "recognizer-model",
    locale: "YoStarJP",
    model: true,
    rawPath: "resource/global/YoStarJP/resource/PaddleOCR/rec/inference.onnx",
    localPath: "third_party/maa/resource/global/YoStarJP/resource/PaddleOCR/rec/inference.onnx",
  },
  {
    id: "jp.rec.keys",
    role: "recognizer-keys",
    locale: "YoStarJP",
    rawPath: "resource/global/YoStarJP/resource/PaddleOCR/rec/keys.txt",
    localPath: "third_party/maa/resource/global/YoStarJP/resource/PaddleOCR/rec/keys.txt",
  },
  {
    id: "jp.rec.version",
    role: "recognizer-version",
    locale: "YoStarJP",
    rawPath: "resource/global/YoStarJP/resource/PaddleOCR/rec/version.txt",
    localPath: "third_party/maa/resource/global/YoStarJP/resource/PaddleOCR/rec/version.txt",
  },
]);

function encodeRawPath(rawPath) {
  return String(rawPath).split("/").map(encodeURIComponent).join("/");
}

export function maaOcrRawUrl(asset, source = MAA_OCR_SOURCE) {
  if (!asset?.rawPath) throw new Error("MAA OCR asset rawPath is required");
  return `${source.rawBaseUrl}/${encodeRawPath(asset.rawPath)}`;
}

export function maaOcrAssets({ includeModels = false } = {}) {
  return MAA_OCR_ASSETS.filter((asset) => includeModels || !asset.model).map((asset) => ({ ...asset }));
}

export function buildMaaOcrAssetManifest({ includeModels = false, exists = () => false } = {}) {
  const assets = maaOcrAssets({ includeModels });
  return {
    version: 1,
    source: { ...MAA_OCR_SOURCE },
    modelFilesIncluded: Boolean(includeModels),
    notes: [
      "MAA uses FastDeploy PPOCRv3 with ONNX Runtime for these model files.",
      "RHODES keeps model files optional so normal Git history stays lightweight.",
    ],
    assets: assets.map((asset) => ({
      ...asset,
      url: maaOcrRawUrl(asset),
      present: Boolean(exists(asset.localPath)),
    })),
  };
}
