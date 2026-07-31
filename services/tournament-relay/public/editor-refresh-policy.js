export function createEditorStateSignature(snapshot) {
  return JSON.stringify(snapshot?.state || {});
}

export function decideEditorRefresh({
  previousSignature,
  nextSignature,
  force = false,
  editorActive = false,
}) {
  if (!force && previousSignature === nextSignature) return "skip";
  if (!force && editorActive) return "defer";
  return "render";
}
