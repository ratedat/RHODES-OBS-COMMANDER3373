import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const resourceRoot = path.join(root, "apps", "rhodes-suki", "resource", "base");
const toolProject = path.join(root, "tools", "rhodes-maa-resource-hash", "RhodesMaaResourceHash.csproj");
const outputPath = path.join(root, "apps", "rhodes-suki", "interface.resource-hash.json");

function computeHash() {
  const result = spawnSync(
    "dotnet",
    ["run", "--project", toolProject, "--configuration", "Release", "--", resourceRoot],
    { cwd: root, encoding: "utf8", windowsHide: true },
  );
  if (result.status !== 0) {
    const detail = [result.stdout, result.stderr].filter(Boolean).join("\n").trim();
    throw new Error(`MaaResource hashの取得に失敗しました。${detail ? `\n${detail}` : ""}`);
  }
  const match = /(?:^|\r?\n)resourceHash=([^\r\n]+)/.exec(result.stdout);
  const hash = match?.[1]?.trim() ?? "";
  if (!hash) throw new Error("MaaResource hashの出力を解析できませんでした。");
  return hash;
}

function serialized(hash) {
  return `${JSON.stringify({ schemaVersion: 1, hash }, null, 2)}\n`;
}

try {
  const actualHash = computeHash();
  const expected = serialized(actualHash);
  if (process.argv.includes("--check")) {
    if (!fs.existsSync(outputPath) || fs.readFileSync(outputPath, "utf8") !== expected) {
      console.error("MAA Resource hash metadata is stale. Run npm run maa:resource:hash:generate");
      process.exitCode = 1;
    } else {
      console.log(`MAA Resource hash is current: ${actualHash}`);
    }
  } else {
    fs.writeFileSync(outputPath, expected);
    console.log(`Generated MAA Resource hash: ${actualHash}`);
  }
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exitCode = 1;
}
