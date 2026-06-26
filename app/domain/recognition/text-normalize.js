export function removeJapaneseSpaces(value) {
  return String(value ?? "").replace(/\s+/g, "");
}

export const MAA_NUMBER_OCR_REPLACE = [
  ["[Oo]", "0"],
  ["[Ii]", "1"],
  ["[Ll]", "1"],
  ["[\\{\\}\\[\\]]", "1"],
  ["B", "8"],
  ["台", "8"],
  ["十", "+"],
  ["萬", "万"],
  ["만", "万"],
  ["億", "亿"],
  ["억", "亿"],
  ["^\\.", ""],
  [" ", ""],
];

export function applyOcrReplace(value, replacements = []) {
  let text = String(value ?? "");
  for (const entry of replacements || []) {
    if (!Array.isArray(entry) || entry.length < 2) continue;
    const [pattern, replacement] = entry;
    text = text.replace(new RegExp(String(pattern), "g"), String(replacement));
  }
  return text;
}

export function normalizeNumberLikeText(value) {
  return applyOcrReplace(value, MAA_NUMBER_OCR_REPLACE).replace(/\s+/g, "");
}

export function normalizeRecognitionText(value, normalizers = []) {
  let text = String(value ?? "");
  for (const normalizer of normalizers || []) {
    if (normalizer === "remove_spaces" || normalizer === "remove_japanese_spaces") text = removeJapaneseSpaces(text);
    else if (normalizer === "number_like" || normalizer === "jp_numeric" || normalizer === "maa_number") text = normalizeNumberLikeText(text);
  }
  return text;
}

export function textMatchesExpected(text, expected = [], { fullMatch = false, match = "any" } = {}) {
  const values = Array.isArray(expected) ? expected : [expected];
  if (!values.length) return true;
  const haystack = String(text ?? "");
  const matches = values.map((value) => {
    const needle = String(value ?? "");
    if (!needle) return false;
    if (fullMatch) return haystack === needle;
    return haystack.includes(needle);
  });
  if (match === "all") return matches.every(Boolean);
  return matches.some(Boolean);
}
