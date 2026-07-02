import { normalizeRecognitionText } from "./text-normalize.js";

function escapeRegExp(value) {
  return String(value).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function asTextResults(frame = {}) {
  const results = [];
  if (typeof frame.text === "string") results.push({ text: frame.text });
  if (typeof frame.ocrText === "string") results.push({ text: frame.ocrText });
  for (const key of ["ocrResults", "textResults", "texts"]) {
    const entries = Array.isArray(frame[key]) ? frame[key] : [];
    for (const entry of entries) {
      if (typeof entry === "string") results.push({ text: entry });
      else if (entry && typeof entry.text === "string") results.push(entry);
    }
  }
  return results;
}

function combinedText(frame, normalizers = ["remove_spaces"]) {
  return normalizeRecognitionText(asTextResults(frame).map((item) => item.text).join(" "), normalizers);
}

function normalizeSquadEffectText(value) {
  return normalizeRecognitionText(value, ["remove_spaces"])
    .replace(/[「」『』【】\[\]（）()]/g, "")
    .replace(/[・･：:．.,，、。;；]/g, "")
    .replace(/＋/g, "+")
    .replace(/[−－–—]/g, "-")
    .toLowerCase();
}

function uniqueValues(values) {
  return [...new Set(values.filter(Boolean))];
}

const squadOcrAliases = {
  is5_sarkaz: [
    { name: "博学多識分隊", aliases: ["多狙分隊"] },
  ],
};

function digitText(value, { allowRoman = false } = {}) {
  let text = normalizeRecognitionText(value, ["remove_spaces"])
    .replace(/[０-９]/g, (char) => String(char.charCodeAt(0) - 0xff10))
    .replace(/[Oo]/g, "0")
    .replace(/図/g, "2")
    .replace(/[イィ]/g, "1");
  if (allowRoman && /^[IiLl一丨イィ]+$/.test(text)) return String(text.length);
  if (allowRoman) text = text.replace(/[IiLl一丨イィ]/g, "1");
  return text.replace(/[^0-9]/g, "");
}

function digitValue(value, { allowRoman = false } = {}) {
  const text = digitText(value, { allowRoman });
  if (!text) return null;
  const valueNumber = Number(text);
  return Number.isFinite(valueNumber) ? valueNumber : null;
}

function numericValuesFromText(text, options = {}) {
  const compact = normalizeRecognitionText(text, ["remove_spaces"]);
  return [...compact.matchAll(/[0-9０-９Oo図IiLl一丨イィ]+/g)]
    .map((match) => digitValue(match[0], options))
    .filter((value) => Number.isFinite(value));
}

function findSquadByText(text, { campaignId, squads = [] } = {}) {
  const campaignSquads = squads
    .filter((item) => item.campaignId === campaignId);
  const exact = campaignSquads
    .find((item) => text.includes(normalizeRecognitionText(item.name, ["remove_spaces"])));
  if (exact) return exact;
  for (const aliasGroup of squadOcrAliases[campaignId] ?? []) {
    const matched = aliasGroup.aliases
      .map((alias) => normalizeRecognitionText(alias, ["remove_spaces"]))
      .some((alias) => alias && text.includes(alias));
    if (!matched) continue;
    const canonicalName = normalizeRecognitionText(aliasGroup.name, ["remove_spaces"]);
    const squad = campaignSquads.find((item) => normalizeRecognitionText(item.name, ["remove_spaces"]) === canonicalName);
    if (squad) return squad;
  }
  return null;
}

function findSquadCandidate(text, { campaignId, squads = [] } = {}) {
  const squad = findSquadByText(text, { campaignId, squads });
  if (!squad) return null;
  return {
    kind: "runStatus",
    field: "squadId",
    label: "分隊",
    value: squad.id,
    rawText: squad.name,
    confidence: 0.86,
    needsReview: true,
  };
}

function optionEffectPhrases(effect = "") {
  return uniqueValues(String(effect)
    .split(/[。、，,；;]/g)
    .map((part) => normalizeSquadEffectText(part))
    .filter((part) => part.length >= 6));
}

function optionEffectTokens(effect = "") {
  const raw = String(effect);
  const bracketTokens = [...raw.matchAll(/[【「]([^】」]+)[】」]/g)]
    .map((match) => normalizeSquadEffectText(match[1]))
    .filter((part) => part.length >= 2);
  const normalized = normalizeSquadEffectText(raw);
  const numericTokens = [...normalized.matchAll(/[★]?[0-9]+%?|[+-][0-9]+/g)]
    .map((match) => match[0])
    .filter((part) => part.length >= 2);
  return uniqueValues([...bracketTokens, ...numericTokens]);
}

function scoreRandomEffectOption(normalizedText, option = {}) {
  const normalizedEffect = normalizeSquadEffectText(option.effect || "");
  if (!normalizedEffect) return null;
  let score = 0;
  let matches = 0;
  if (normalizedText.includes(normalizedEffect)) {
    score += 120;
    matches += 3;
  }
  for (const phrase of optionEffectPhrases(option.effect)) {
    if (!normalizedText.includes(phrase)) continue;
    score += Math.min(32, Math.max(10, Math.floor(phrase.length / 2)));
    matches += 1;
  }
  for (const token of optionEffectTokens(option.effect)) {
    if (!normalizedText.includes(token)) continue;
    score += token.length >= 4 ? 18 : 8;
    matches += 1;
  }
  if (!matches || score < 20) return null;
  return { option, score, matches };
}

function findRandomEffectOption(normalizedText, options = []) {
  const scored = options
    .map((option) => scoreRandomEffectOption(normalizedText, option))
    .filter(Boolean)
    .sort((a, b) => b.score - a.score || b.matches - a.matches);
  if (!scored.length) return null;
  if (scored[1] && scored[0].score === scored[1].score && scored[0].matches === scored[1].matches) return null;
  return scored[0];
}

function findSquadRandomEffectCandidate(text, { campaignId, squads = [] } = {}) {
  const squad = findSquadByText(text, { campaignId, squads });
  if (!squad || !normalizeRecognitionText(squad.name, ["remove_spaces"]).includes("奇想天外分隊")) return null;
  const options = Array.isArray(squad.randomEffectOptions) ? squad.randomEffectOptions : [];
  if (!options.length) return null;
  const match = findRandomEffectOption(normalizeSquadEffectText(text), options);
  if (!match?.option?.id) return null;
  return {
    kind: "runStatus",
    field: "squadRandomEffectOptionId",
    label: "ランダム分隊効果",
    value: match.option.id,
    rawText: match.option.effect || match.option.label || match.option.id,
    confidence: Math.min(0.93, 0.68 + (match.score / 500)),
    needsReview: true,
  };
}

function findDifficultyCandidate(text, { campaignId, difficultyGrades = {}, frame = null } = {}) {
  const config = difficultyGrades[campaignId];
  const grades = config?.grades || [];
  const name = normalizeRecognitionText(config?.difficultyName || "", ["remove_spaces"]);
  if (!name || !grades.length) return null;

  const validGrade = (value) => grades.some((item) => Number(item.grade) === value);
  const difficultyBlockText = normalizeRecognitionText(asTextResults(frame || {})
    .filter((item) => String(item.regionId || "").includes("difficulty_block"))
    .map((item) => item.text)
    .join(" "), ["remove_spaces"]);
  const textSources = difficultyBlockText ? [difficultyBlockText] : [text];
  const textGrade = textSources
    .map((sourceText) => sourceText.includes(name) ? sourceText.match(new RegExp(`${escapeRegExp(name)}[^0-9０-９Oo図イィA-Za-z]{0,16}([0-9０-９Oo図イィ]{1,2})`)) : null)
    .map((match) => digitValue(match?.[1]))
    .find((value) => Number.isFinite(value) && validGrade(value));
  const regionGrade = asTextResults(frame || {})
    .filter((item) => String(item.regionId || "").includes("difficulty_grade"))
    .flatMap((item) => numericValuesFromText(item.text))
    .find(validGrade);
  const grade = textGrade ?? regionGrade;
  const difficulty = grades.find((item) => Number(item.grade) === grade);
  if (!difficulty) return null;
  return {
    kind: "runStatus",
    field: "difficulty",
    label: "等級",
    value: difficulty.grade,
    rawText: difficulty.label,
    confidence: textGrade == null && regionGrade != null ? 0.87 : 0.84,
    needsReview: true,
  };
}


function findRegionNumberCandidate(frame, { field, label, regionIdPart, min = 0, max = 999, confidence = 0.7, prefer = "last", allowRoman = false }) {
  const entry = asTextResults(frame)
    .filter((item) => String(item.regionId || "").includes(regionIdPart))
    .map((item) => ({ item, values: numericValuesFromText(item.text, { allowRoman }) }))
    .find((candidate) => candidate.values.some((value) => value >= min && value <= max));
  if (!entry) return null;
  const valid = entry.values.filter((candidateValue) => candidateValue >= min && candidateValue <= max);
  const value = prefer === "first" ? valid[0] : valid.at(-1);
  return {
    kind: "runStatus",
    field,
    label,
    value,
    rawText: `${label} ${value}`,
    confidence: Math.min(0.98, confidence + 0.05),
    needsReview: true,
  };
}

function findBestRegionNumberCandidate(frame, { field, label, regionIdPart, regionIdPattern = null, min = 0, max = 999, confidence = 0.7, prefer = "last", allowRoman = false }) {
  const candidates = asTextResults(frame)
    .filter((item) => {
      const regionId = String(item.regionId || "");
      return regionIdPattern ? regionIdPattern.test(regionId) : regionId.includes(regionIdPart);
    })
    .map((item) => {
      const values = numericValuesFromText(item.text, { allowRoman }).filter((value) => value >= min && value <= max);
      if (!values.length) return null;
      const value = prefer === "first" ? values[0] : values.at(-1);
      return { item, value, confidence: Number(item.confidence ?? confidence) };
    })
    .filter(Boolean)
    .toSorted((a, b) => b.confidence - a.confidence);
  const best = candidates[0];
  if (!best) return null;
  return {
    kind: "runStatus",
    field,
    label,
    value: best.value,
    rawText: `${label} ${best.value}`,
    confidence: Math.min(0.98, Math.max(confidence, best.confidence)),
    needsReview: true,
  };
}

function candidateFromNumber({ field, label, value, confidence = 0.75 }) {
  if (!Number.isFinite(value)) return null;
  return {
    kind: "runStatus",
    field,
    label,
    value,
    rawText: `${label} ${value}`,
    confidence,
    needsReview: true,
  };
}

function firstNumericValueInRange(entry, { min = 0, max = 999 } = {}) {
  return numericValuesFromText(entry?.text).find((value) => Number.isFinite(value) && value >= min && value <= max);
}

function isRegionId(regionId, baseId) {
  const value = String(regionId || "");
  return value === baseId || value.startsWith(`${baseId}.`) || value.startsWith(`${baseId}-`) || value.startsWith(`${baseId}_`);
}

function findTemplateIngotCandidate(frame) {
  for (const item of asTextResults(frame)) {
    const regionId = String(item.regionId || "");
    if (!/^run\.ingot[._-]/.test(regionId)) continue;
    const digits = digitText(item.text);
    if (!digits) continue;
    const value = /^0[1-9]$/.test(digits) ? Number(`${digits[1]}0`) : digitValue(item.text);
    if (!Number.isFinite(value) || value < 0 || value > 9999) continue;
    return candidateFromNumber({ field: "ingot", label: "源石錐", value, confidence: 0.75 });
  }
  return null;
}

function findIngotCandidate(frame) {
  const templateIngot = findTemplateIngotCandidate(frame);
  if (templateIngot) return templateIngot;
  const direct = findBestRegionNumberCandidate(frame, { field: "ingot", label: "源石錐", regionIdPattern: /^run\.ingot$/, min: 0, max: 9999, confidence: 0.86, prefer: "first" });
  if (direct) return direct;
  return findRegionNumberCandidate(frame, { field: "ingot", label: "源石錐", regionIdPart: "ingot", min: 0, max: 9999, prefer: "first" });
}

function ideaCurrentValueFromText(text, { allowCompact = false } = {}) {
  const compact = normalizeRecognitionText(text, ["remove_spaces"]);
  const fraction = compact.match(/([0-9０-９Oo図IiLl一丨イィ]{1,3})[\/／<＜]([0-9０-９Oo図IiLl一丨イィ]{1,3})/);
  if (fraction) {
    const current = digitValue(fraction[1]);
    const max = digitValue(fraction[2]);
    if (Number.isFinite(current) && Number.isFinite(max) && current >= 0 && max >= current) return current;
  }

  const digits = digitText(text);
  if (!allowCompact && digits.length > 1) return null;
  if (digits.length === 2) {
    const current = Number(digits[0]);
    const max = Number(digits[1]);
    if (max >= current) return current;
    if (allowCompact) return max;
  }
  if (digits.length === 3) {
    const current = Number(digits.slice(0, 1));
    const max = Number(digits.slice(1));
    if (max >= current && max <= 99) return current;
  }
  if (digits.length === 4) {
    const current = Number(digits.slice(0, 2));
    const max = Number(digits.slice(2));
    if (max >= current && max <= 99) return current;
  }

  return firstNumericValueInRange({ text }, { min: 0, max: 999 });
}

function findIdeaCandidate(frame, { campaignId } = {}) {
  if (campaignId !== "is5_sarkaz") return null;
  const currentCandidates = asTextResults(frame)
    .filter((item) => isRegionId(item.regionId, "run.idea.current"))
    .map((item) => {
      const value = ideaCurrentValueFromText(item.text, { allowCompact: true });
      if (!Number.isFinite(value) || value < 0 || value > 999) return null;
      return { value, confidence: Number(item.confidence ?? 0.86) };
    })
    .filter(Boolean)
    .toSorted((a, b) => b.confidence - a.confidence);
  if (currentCandidates[0]) {
    return candidateFromNumber({
      field: "idea",
      label: "構想",
      value: currentCandidates[0].value,
      confidence: Math.min(0.98, Math.max(0.86, currentCandidates[0].confidence)),
    });
  }
  return null;
}

export function extractRunStatusCandidates(frame, { campaignId, squads = [], difficultyGrades = {} } = {}) {
  if (!campaignId) return [];
  const compactText = combinedText(frame, ["remove_spaces"]);
  const numericText = normalizeRecognitionText(compactText, ["jp_numeric"]);
  const candidates = [
    findSquadCandidate(numericText, { campaignId, squads }),
    findSquadRandomEffectCandidate(compactText, { campaignId, squads }),
    findDifficultyCandidate(compactText, { campaignId, difficultyGrades, frame }),
    findIngotCandidate(frame),
    findIdeaCandidate(frame, { campaignId }),
  ].filter(Boolean);
  return candidates;
}
