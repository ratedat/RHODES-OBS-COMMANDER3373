export function bindCatalogSearchInput(input, onSearch, currentInput) {
  let composing = false;
  let appliedValue = input.value;
  const applySearch = () => {
    const { value, selectionStart, selectionEnd, selectionDirection } = input;
    if (value === appliedValue) return;
    appliedValue = value;
    onSearch(value);
    const next = currentInput();
    next?.focus();
    if (selectionStart !== null && selectionEnd !== null) {
      next?.setSelectionRange(selectionStart, selectionEnd, selectionDirection);
    }
  };
  // Rebuilding the catalog replaces its search input and cancels the browser's IME.
  // Wait for committed text, including engines that omit InputEvent.isComposing.
  input.addEventListener("compositionstart", () => { composing = true; });
  input.addEventListener("compositionend", () => {
    composing = false;
    applySearch();
  });
  input.addEventListener("input", (event) => {
    if (!composing && !event.isComposing) applySearch();
  });
}
