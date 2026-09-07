import { createHash } from "node:crypto";
import { createReadStream } from "node:fs";
import fs from "node:fs/promises";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const privateSegments = new Set([
  ".git", ".hg", ".svn", ".agent-work", ".codex", ".private", ".analysis-private",
  "private-debug", "private-analysis", "private-data", "debug-environment",
  "memory-dumps", "disassembly", "decompiled",
]);
const privateExtensions = new Set([".dmp", ".mdmp", ".dump", ".i64", ".idb", ".gpr", ".wsb"]);
const archiveExtensions = new Set([".zip", ".7z", ".rar", ".tar", ".tgz"]);
const transientExtensions = new Set([".log", ".pdb", ".tmp", ".pyc", ".pyo"]);
const transientSegments = new Set(["__pycache__", ".pytest_cache", ".ruff_cache"]);
const codeExtensions = new Set([".cs", ".py", ".js", ".mjs", ".ts", ".tsx", ".ps1", ".cpp", ".c", ".h", ".rs"]);
const sourceLimit = 8 * 1024 * 1024;
const reviewedArchives = [
  // Existing review archive, verified against the three adjacent public files.
  "ef84a5dfc370d0826cc6b16e9c7a04f0ed0fe819efd7553fdcc00207c55d8626",
  // Maa.AgentBinary 1.2.0 from nuget.org: bin/maatouch/universal/maatouch.
  // Exact Android helper payload; a changed archive still requires a new review.
  "4ea8590cd0349ce900f39ab16ef3751dad2356286b465b4293f80f9858c995d0",
];

function digest(bytes) { return createHash("sha256").update(bytes).digest("hex"); }
function normalize(value) { return String(value).replace(/\\+/g, "/").toLowerCase(); }
function hashSet(values = []) {
  return new Set(values.map(value => {
    if (!/^[a-f0-9]{64}$/i.test(value)) throw new Error("Invalid publication fingerprint.");
    return value.toLowerCase();
  }));
}
function archiveHeader(bytes) {
  return [Buffer.from([0x50, 0x4b, 0x03, 0x04]), Buffer.from([0x50, 0x4b, 0x05, 0x06]),
    Buffer.from([0x37, 0x7a, 0xbc, 0xaf, 0x27, 0x1c]), Buffer.from([0x1f, 0x8b]), Buffer.from("Rar!")]
    .some(header => bytes.subarray(0, header.length).equals(header)) || bytes.subarray(257, 262).toString("ascii") === "ustar";
}
function textSource(bytes) {
  const utf16 = bytes.subarray(0, 2).equals(Buffer.from([0xff, 0xfe])) || bytes.subarray(0, 256).filter(byte => byte === 0).length > 40;
  return utf16 ? bytes.toString("utf16le") : bytes.toString("utf8");
}
function containsCredential(text) {
  return /-----BEGIN (?:RSA |EC |DSA |OPENSSH |ENCRYPTED )?PRIVATE KEY-----/.test(text) ||
    /\bgh[pousr]_[A-Za-z0-9]{36,255}\b/.test(text) ||
    /\bgithub_pat_[A-Za-z0-9_]{82,255}\b/.test(text) ||
    /\bsk-(?:proj-)?[A-Za-z0-9_-]{40,255}\b/.test(text);
}
function contained(root, target) {
  const relative = path.relative(root, target);
  return relative === "" || (!relative.startsWith(`..${path.sep}`) && relative !== ".." && !path.isAbsolute(relative));
}
function git(root, args, options = {}) {
  const result = spawnSync("git", ["-C", root, ...args], {
    encoding: options.binary ? undefined : "utf8", input: options.input,
    maxBuffer: 128 * 1024 * 1024,
  });
  if (result.error) throw result.error;
  if (result.status !== 0 && !options.optional) throw new Error(`Publication check: git ${args[0]} failed.`);
  return result;
}

// Compare substantive source windows, without storing source text in a policy
// or returning matching private material in diagnostic messages.
export function sourceFingerprints(bytes) {
  const lines = textSource(bytes).split(/\r?\n/).map(line => line.trim().replace(/\s+/g, " "))
    .filter(line => line.length >= 24 && !/^(\/\/|\/\*|\*|#|using |import |from |namespace |package )/.test(line));
  const hashes = [];
  for (const count of [3, 5]) for (let index = 0; index + count <= lines.length; index += 1) {
    const window = lines.slice(index, index + count).join("\n");
    if (window.length >= (count === 3 ? 160 : 240)) hashes.push(digest(window));
  }
  return hashes;
}

async function readLocalPolicy(repoRoot) {
  const common = git(repoRoot, ["rev-parse", "--git-common-dir"], { optional: true });
  if (common.status !== 0) throw new Error("Publication checks require a Git working tree.");
  const required = git(repoRoot, ["config", "--get", "publication.policyRequired"], { optional: true }).stdout.trim() === "true";
  const commonDir = path.resolve(repoRoot, common.stdout.trim());
  const policyPath = path.join(commonDir, "info", "publication-private.json");
  let policy;
  try { policy = JSON.parse(await fs.readFile(policyPath, "utf8")); }
  catch (error) {
    if (error.code === "ENOENT" && !required) return { schemaVersion: 1 };
    throw new Error("The required local publication policy is missing or unreadable.");
  }
  if (policy.schemaVersion !== 1) throw new Error("Unsupported local publication policy.");
  return policy;
}

async function collectPrivateFingerprints(policy) {
  const files = new Set();
  const roots = (policy.privateRoots ?? []).map(value => path.resolve(value));
  async function visit(target, extensions = null) {
    const info = await fs.lstat(target);
    if (info.isSymbolicLink()) throw new Error("A private fingerprint input is redirected; review the local policy.");
    if (!roots.some(root => contained(root, path.resolve(target)))) throw new Error("A fingerprint input is outside its declared private root.");
    if (info.isDirectory()) {
      for (const entry of await fs.readdir(target, { withFileTypes: true })) {
        if ([".git", "__pycache__", "bin", "obj", "node_modules", ".venv"].includes(entry.name.toLowerCase())) continue;
        await visit(path.join(target, entry.name), extensions);
      }
    } else if (info.isFile() && (!extensions || extensions.has(path.extname(target).toLowerCase()))) files.add(target);
    else if (!info.isFile()) throw new Error("An unsupported private fingerprint input was found.");
  }
  for (const entry of policy.fingerprintRoots ?? []) {
    await visit(path.resolve(entry.path), new Set(entry.extensions.map(value => value.toLowerCase())));
  }
  for (const file of policy.fingerprintFiles ?? []) await visit(path.resolve(file));
  const hashes = hashSet(policy.blockedSha256);
  const fragments = hashSet(policy.blockedSourceFingerprints);
  for (const file of files) {
    const info = await fs.stat(file);
    if (info.size <= sourceLimit) {
      const bytes = await fs.readFile(file);
      // Empty files and trivial common boilerplate are not private signatures.
      if (bytes.length >= 64) hashes.add(digest(bytes));
      if (codeExtensions.has(path.extname(file).toLowerCase())) {
        for (const hash of sourceFingerprints(bytes)) fragments.add(hash);
      }
    } else {
      const hash = createHash("sha256");
      for await (const bytes of createReadStream(file)) hash.update(bytes);
      hashes.add(hash.digest("hex"));
    }
  }
  return { hashes, fragments };
}

export async function createPublicationGuard(repoRoot, options = {}) {
  repoRoot = path.resolve(repoRoot);
  const realRoot = await fs.realpath(repoRoot);
  const policy = options.policy ?? await readLocalPolicy(repoRoot);
  if (policy.schemaVersion !== 1) throw new Error("Unsupported publication policy.");
  const signatures = await collectPrivateFingerprints(policy);
  const privatePaths = (policy.privateRoots ?? []).map(value => path.resolve(value));
  const blockedText = (policy.blockedText ?? []).map(normalize);
  const blockedSegments = new Set([...privateSegments, ...(policy.blockedPathSegments ?? []).map(normalize)]);
  const blockedNames = new Set((policy.blockedFileNames ?? []).map(normalize));
  const allowedArchives = hashSet([...reviewedArchives, ...(policy.allowedArchiveSha256 ?? [])]);
  const checkedFiles = new Map();

  function pathCheck(name, { omitTransient = false } = {}) {
    const parts = normalize(name).split("/").filter(Boolean);
    const base = parts.at(-1) ?? "";
    if (parts.some(part => blockedSegments.has(part)) || blockedNames.has(base)) throw new Error(`Non-public path refused: ${name}`);
    const extension = path.extname(base);
    if (privateExtensions.has(extension) || /\.(decompiled|disasm)\./.test(base)) {
      throw new Error(`Non-public file type refused: ${name}`);
    }
    const transient = transientExtensions.has(extension) || parts.some(part => transientSegments.has(part));
    if (transient && !omitTransient) throw new Error(`Local generated file refused: ${name}`);
    return !transient;
  }
  function textCheck(bytes, name) {
    const decoded = [bytes.toString("utf8"), bytes.toString("utf16le")];
    if (decoded.some(containsCredential)) throw new Error(`Credential material refused: ${name}`);
    const texts = decoded.map(normalize);
    if (blockedText.some(pattern => texts.some(text => text.includes(pattern)))) throw new Error(`Private content refused: ${name}`);
  }
  function bufferCheck(bytes, name) {
    const header = bytes.subarray(0, 24).toString("ascii");
    if (header.startsWith("MDMP") || header.startsWith("PAGEDUMP") || header.startsWith("PAGEDU64")) throw new Error(`Memory dump refused: ${name}`);
    if (header.startsWith("Microsoft C/C++ MSF")) throw new Error(`Debug symbols refused: ${name}`);
    const hash = digest(bytes);
    if (signatures.hashes.has(hash)) throw new Error(`Private file fingerprint refused: ${name}`);
    if ((archiveExtensions.has(path.extname(name).toLowerCase()) || archiveHeader(bytes)) && !allowedArchives.has(hash)) {
      throw new Error(`Nested archive refused: ${name}`);
    }
    textCheck(bytes, name);
    if ((!bytes.subarray(0, 65536).includes(0) || bytes.subarray(0, 2).equals(Buffer.from([0xff, 0xfe]))) && sourceFingerprints(bytes).some(hash => signatures.fragments.has(hash))) {
      throw new Error(`Private source excerpt refused: ${name}`);
    }
  }
  async function fileCheck(file, name) {
    const info = await fs.stat(file);
    const cacheKey = `${info.size}:${info.mtimeMs}`;
    if (checkedFiles.get(file) === cacheKey) return;
    if (info.size <= sourceLimit) bufferCheck(await fs.readFile(file), name);
    else {
      const hash = createHash("sha256");
      let previous = Buffer.alloc(0);
      let first = true;
      let archive = archiveExtensions.has(path.extname(name).toLowerCase());
      for await (const bytes of createReadStream(file, { highWaterMark: 128 * 1024 })) {
        hash.update(bytes);
        if (first) {
          // Inspect headers using a short buffer without matching whole-file hashes.
          const header = bytes.subarray(0, 24).toString("ascii");
          if (/^(MDMP|PAGEDUMP|PAGEDU64|Microsoft C\/C\+\+ MSF)/.test(header)) throw new Error(`Debug artifact refused: ${name}`);
          archive ||= archiveHeader(bytes);
          first = false;
        }
        const combined = Buffer.concat([previous, bytes]);
        textCheck(combined, name);
        if (!combined.includes(0) && sourceFingerprints(combined).some(value => signatures.fragments.has(value))) throw new Error(`Private source excerpt refused: ${name}`);
        previous = bytes.subarray(Math.max(0, bytes.length - 16384));
      }
      const fingerprint = hash.digest("hex");
      if (signatures.hashes.has(fingerprint)) throw new Error(`Private file fingerprint refused: ${name}`);
      if (archive && !allowedArchives.has(fingerprint)) throw new Error(`Nested archive refused: ${name}`);
    }
    checkedFiles.set(file, cacheKey);
  }
  async function walk(directory, opts, visitor = fileCheck) {
    directory = path.resolve(directory);
    const rootInfo = await fs.lstat(directory);
    if (rootInfo.isSymbolicLink()) throw new Error("Redirected publication root refused.");
    const actualRoot = await fs.realpath(directory);
    if (!contained(realRoot, actualRoot) || privatePaths.some(root => contained(root, actualRoot))) throw new Error("Publication input is outside the repository boundary.");
    const ignoreTop = new Set((opts.ignoreTopLevel ?? []).map(normalize));
    const ignoreDirs = new Set((opts.ignoreDirectoryNames ?? []).map(normalize));
    async function visit(current, relative) {
      const info = await fs.lstat(current);
      if (info.isSymbolicLink()) throw new Error(`Linked publication input refused: ${relative}`);
      if (!contained(actualRoot, await fs.realpath(current))) throw new Error("Publication input escaped its root.");
      if (!pathCheck(relative, opts)) return;
      if (info.isDirectory()) {
        for (const entry of await fs.readdir(current, { withFileTypes: true })) {
          if (!relative && ignoreTop.has(normalize(entry.name))) continue;
          if (entry.isDirectory() && ignoreDirs.has(normalize(entry.name))) continue;
          await visit(path.join(current, entry.name), relative ? `${relative}/${entry.name}` : entry.name);
        }
      } else if (info.isFile()) await visitor(current, relative);
      else throw new Error(`Unsupported publication entry refused: ${relative}`);
    }
    await visit(directory, "");
  }
  async function checkTree(directory, opts = {}) { await walk(directory, opts); }
  async function assertDestination(target) {
    target = path.resolve(target);
    if (target === repoRoot || !contained(repoRoot, target)) throw new Error("Publication destination is outside the output boundary.");
    for (let current = target; contained(repoRoot, current); current = path.dirname(current)) {
      try {
        if ((await fs.lstat(current)).isSymbolicLink() || !contained(realRoot, await fs.realpath(current))) {
          throw new Error("Linked publication destination refused.");
        }
      } catch (error) { if (error.code !== "ENOENT") throw error; }
      if (current === repoRoot) break;
    }
  }
  async function copyTree(source, target, opts = {}) {
    const permitted = new Set([path.resolve(source)]);
    await walk(source, { ...opts, omitTransient: true }, async (file, name) => {
      await fileCheck(file, name);
      for (let current = file; contained(path.resolve(source), current); current = path.dirname(current)) {
        permitted.add(path.resolve(current));
        if (current === path.resolve(source)) break;
      }
    });
    target = path.resolve(target);
    await assertDestination(target);
    try { await walk(target, { omitTransient: true }); } catch (error) { if (error.code !== "ENOENT") throw error; }
    await fs.cp(source, target, { recursive: true, filter: candidate => permitted.has(path.resolve(candidate)) });
  }
  async function checkGitWorktree() {
    if (git(repoRoot, ["ls-files", "--stage"]).stdout.split("\n").some(line => /^(120000|160000) /.test(line))) throw new Error("Linked Git entry refused.");
    const names = git(repoRoot, ["ls-files", "-z", "--cached", "--others", "--exclude-standard"]).stdout.split("\0").filter(Boolean);
    for (const name of new Set(names)) {
      pathCheck(name);
      const file = path.join(repoRoot, name);
      let info;
      try { info = await fs.lstat(file); } catch (error) { if (error.code === "ENOENT") continue; throw error; }
      if (info.isSymbolicLink() || !contained(realRoot, await fs.realpath(file))) throw new Error(`Linked Git input refused: ${name}`);
      if (info.isFile()) await fileCheck(file, name);
    }
  }
  function checkObjects(objects) {
    if (!objects.size) return;
    const ids = [...objects.keys()];
    const metadata = git(repoRoot, ["cat-file", "--batch-check"], { input: `${ids.join("\n")}\n` }).stdout.trim().split("\n");
    let batch = [], size = 0;
    function flush() {
      if (!batch.length) return;
      const data = git(repoRoot, ["cat-file", "--batch"], { input: `${batch.join("\n")}\n`, binary: true }).stdout;
      let offset = 0;
      for (const expected of batch) {
        const end = data.indexOf(10, offset);
        const [id, kind, length] = data.subarray(offset, end).toString("ascii").split(" ");
        if (end < 0 || id !== expected || !/^\d+$/.test(length)) throw new Error("Invalid Git object stream.");
        offset = end + 1;
        // Own aligned storage before UTF-16 decoding and native hashing.
        const bytes = Buffer.from(data.subarray(offset, offset + Number(length)));
        offset += Number(length) + 1;
        const names = objects.get(id);
        if (kind === "blob") for (const name of names) { pathCheck(name); bufferCheck(bytes, name); }
        else if (kind === "commit" || kind === "tag") textCheck(bytes, "Git metadata");
        else if (kind === "tree") {
          let cursor = 0;
          while (cursor < bytes.length) {
            const nul = bytes.indexOf(0, cursor);
            if (nul < 0) throw new Error("Invalid Git tree.");
            const header = bytes.subarray(cursor, nul).toString("utf8");
            const separator = header.indexOf(" ");
            const mode = header.slice(0, separator);
            const name = header.slice(separator + 1);
            pathCheck(name);
            if (mode === "120000" || mode === "160000") throw new Error(`Linked Git object refused: ${name}`);
            cursor = nul + 1 + id.length / 2;
          }
        } else throw new Error("Unsupported Git publication object.");
      }
      batch = []; size = 0;
    }
    for (let index = 0; index < ids.length; index += 1) {
      const [id, kind, length] = metadata[index].trim().split(" ");
      if (id !== ids[index] || !["blob", "tree", "commit", "tag"].includes(kind) || !/^\d+$/.test(length)) throw new Error("Missing Git publication object.");
      if (Number(length) > 100 * 1024 * 1024) throw new Error("Oversized Git object needs separate publication review.");
      // Bound native child-process buffering as well as total object bytes.
      if (size + Number(length) > 8 * 1024 * 1024 || batch.length >= 64) flush();
      batch.push(id); size += Number(length);
    }
    flush();
  }
  function checkStaged() {
    const objects = new Map();
    const entries = git(repoRoot, ["ls-files", "--stage", "-z"]).stdout.split("\0").filter(Boolean);
    for (const entry of entries) {
      const match = /^(\d+) ([a-f0-9]+) (\d+)\t([\s\S]+)$/.exec(entry);
      if (!match || match[3] !== "0") throw new Error("Resolve index conflicts before publication.");
      if (match[1] === "120000" || match[1] === "160000") throw new Error(`Linked Git object refused: ${match[4]}`);
      pathCheck(match[4]);
      if (!objects.has(match[2])) objects.set(match[2], new Set());
      objects.get(match[2]).add(match[4]);
    }
    checkObjects(objects);
  }
  function checkPush(input) {
    const objects = new Map();
    const zero = /^0+$/;
    for (const line of input.trim().split(/\r?\n/).filter(Boolean)) {
      const [localRef, localId, remoteRef, remoteId] = line.trim().split(/\s+/);
      if (!localRef || !remoteRef || !/^[a-f0-9]{40,64}$/.test(localId ?? "") || !/^[a-f0-9]{40,64}$/.test(remoteId ?? "")) throw new Error("Malformed push input; publication refused.");
      if (zero.test(localId)) continue;
      if ((!/^refs\/(heads|tags)\//.test(localRef) && localRef !== "HEAD") || !/^refs\/(heads|tags)\//.test(remoteRef)) throw new Error("Internal Git references cannot be published.");
      pathCheck(localRef); pathCheck(remoteRef);
      textCheck(Buffer.from(`${localRef}\n${remoteRef}`), "Git reference names");
      const revisionArgs = [localId];
      if (!zero.test(remoteId) && git(repoRoot, ["cat-file", "-e", remoteId], { optional: true }).status === 0) revisionArgs.push(`^${remoteId}`);
      const outgoing = git(repoRoot, ["rev-list", "--objects", ...revisionArgs]).stdout.trim().split(/\r?\n/).filter(Boolean);
      // A ref may point directly to a blob/tree; always include its target.
      outgoing.unshift(localId);
      for (const entry of outgoing) {
        const space = entry.indexOf(" ");
        const objectId = space < 0 ? entry : entry.slice(0, space);
        const name = space < 0 ? `(Git object ${objectId.slice(0, 12)})` : entry.slice(space + 1);
        if (space >= 0) pathCheck(name);
        if (!objects.has(objectId)) objects.set(objectId, new Set());
        objects.get(objectId).add(name);
      }
    }
    checkObjects(objects);
  }
  return { checkTree, copyTree, checkGitWorktree, checkStaged, checkPush, pathCheck, assertDestination };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const root = process.argv[3] ? path.resolve(process.argv[3]) : path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
  const guard = await createPublicationGuard(root);
  const mode = process.argv[2] ?? "--worktree";
  if (mode === "--worktree") await guard.checkGitWorktree();
  else if (mode === "--staged") guard.checkStaged();
  else if (mode === "--push") {
    let input = "";
    for await (const chunk of process.stdin) input += chunk;
    guard.checkPush(input);
  } else throw new Error("Unknown publication check mode.");
  console.log("Publication boundary check passed.");
}
