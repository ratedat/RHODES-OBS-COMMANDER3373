import fs from "node:fs/promises";
import nodeFs from "node:fs";
import path from "node:path";

import { buildMaaOcrAssetManifest, maaOcrAssets, maaOcrRawUrl } from "../app/domain/recognition/maa-ocr-assets.js";

const root = process.cwd();
const args = new Set(process.argv.slice(2));
const includeModels = args.has("--models");
const dryRun = args.has("--dry-run");
const manifestFile = path.join(root, "data", "recognition", "maa-onnx-ocr-assets.json");

async function downloadAsset(asset) {
  const url = maaOcrRawUrl(asset);
  const target = path.join(root, asset.localPath);
  if (dryRun) return { id: asset.id, localPath: asset.localPath, url, skipped: true };
  const response = await fetch(url, { headers: { "User-Agent": "RHODES-OBS-COMMANDER3373" } });
  if (!response.ok) throw new Error(`failed to download ${asset.id}: ${response.status} ${response.statusText}`);
  const body = Buffer.from(await response.arrayBuffer());
  await fs.mkdir(path.dirname(target), { recursive: true });
  await fs.writeFile(target, body);
  return { id: asset.id, localPath: asset.localPath, url, bytes: body.length };
}

const assets = maaOcrAssets({ includeModels });
const downloaded = [];
for (const asset of assets) {
  downloaded.push(await downloadAsset(asset));
}

const manifest = buildMaaOcrAssetManifest({
  includeModels,
  exists: (localPath) => nodeFs.existsSync(path.join(root, localPath)),
});
manifest.syncedAt = new Date().toISOString();
manifest.downloaded = downloaded;

if (!dryRun) {
  await fs.mkdir(path.dirname(manifestFile), { recursive: true });
  await fs.writeFile(manifestFile, `${JSON.stringify(manifest, null, 2)}\n`, "utf8");
}

console.log(JSON.stringify({ includeModels, dryRun, downloaded, manifest: path.relative(root, manifestFile) }, null, 2));
