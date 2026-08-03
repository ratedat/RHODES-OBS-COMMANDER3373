import {
  createEditorStateSignature,
  decideEditorRefresh,
} from "/assets/editor-refresh-policy.js";
import {
  buildDraftState,
  difficultyTierEntries,
  operationKey,
  upsertDraftOperation,
} from "/assets/editor-draft.js";
import {
  buildOperatorCatalogView,
  buildRelicCatalogView,
} from "/assets/catalog-filters.js";

const sessionId = location.pathname.split("/").filter(Boolean).at(-1) || "";
const query = new URLSearchParams(location.search);
const storageKey = `rhodes-tournament-editor:${sessionId}`;
const editorCode = (query.get("code") || localStorage.getItem(storageKey) || "").trim().toUpperCase();

if (editorCode) localStorage.setItem(storageKey, editorCode);

const elements = {
  playerLabel: document.querySelector("#player-label"),
  connectionState: document.querySelector("#connection-state"),
  stateSummary: document.querySelector("#state-summary"),
  selectedOperators: document.querySelector("#selected-operators"),
  selectedRelics: document.querySelector("#selected-relics"),
  selectedBosses: document.querySelector("#selected-bosses"),
  operatorCount: document.querySelector("#operator-count"),
  relicCount: document.querySelector("#relic-count"),
  editor: document.querySelector("#editor"),
  tabs: document.querySelector("#input-tabs"),
  history: document.querySelector("#history-list"),
  toast: document.querySelector("#toast"),
  refresh: document.querySelector("#refresh-button"),
  clearRun: document.querySelector("#clear-run-button"),
  pendingBar: document.querySelector("#pending-bar"),
  pendingSummary: document.querySelector("#pending-summary"),
  pendingDetail: document.querySelector("#pending-detail"),
  discard: document.querySelector("#discard-button"),
  send: document.querySelector("#send-button"),
};

let bootstrap = null;
let activeTab = "run";
const catalogFilters = {
  operators: {
    search: "",
    className: "all",
    branch: "all",
    rarity: "all",
    sort: "rarity",
    selectedFirst: false,
    selectedOnly: false,
    columns: 2,
  },
  relics: {
    search: "",
    category: "all",
    sort: "category",
    selectedFirst: false,
    selectedOnly: false,
    columns: 2,
  },
};
let pollTimer = 0;
let toastTimer = 0;
let requestPending = false;
let editorRenderSignature = "";
let editorRefreshPending = false;
let pendingOperations = [];
let draftState = null;
let submittedOperationId = "";

function node(tag, properties = {}, children = []) {
  const element = document.createElement(tag);
  for (const [key, value] of Object.entries(properties)) {
    if (key === "className") element.className = value;
    else if (key === "text") element.textContent = value;
    else if (key === "dataset") Object.assign(element.dataset, value);
    else if (key.startsWith("on") && typeof value === "function") element.addEventListener(key.slice(2), value);
    else if (value !== undefined && value !== null) element[key] = value;
  }
  for (const child of Array.isArray(children) ? children : [children]) {
    if (child !== null && child !== undefined) element.append(child);
  }
  return element;
}

function showToast(message, error = false) {
  clearTimeout(toastTimer);
  elements.toast.textContent = message;
  elements.toast.classList.toggle("error", error);
  elements.toast.classList.add("visible");
  toastTimer = setTimeout(() => elements.toast.classList.remove("visible"), 2_800);
}

async function request(path, options = {}) {
  const response = await fetch(path, {
    cache: "no-store",
    ...options,
    headers: {
      ...(options.body ? { "content-type": "application/json" } : {}),
      ...(options.headers || {}),
    },
  });
  const payload = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(payload.error || `HTTP ${response.status}`);
  return payload;
}

async function loadBootstrap({ announce = false, forceEditor = false } = {}) {
  if (!sessionId || !editorCode) {
    setConnection("参加コードがありません", false);
    elements.editor.replaceChildren(node("p", {
      className: "empty",
      text: "配信担当者から受け取った入力URLを開き直してください。",
    }));
    return;
  }
  if (requestPending) return;
  requestPending = true;
  try {
    bootstrap = await request(`/api/sessions/${encodeURIComponent(sessionId)}/bootstrap?code=${encodeURIComponent(editorCode)}`);
    const submitted = submittedOperationId
      ? (bootstrap.history || []).find((entry) => entry.id === submittedOperationId)
      : null;
    if (submitted?.status === "applied") {
      pendingOperations = [];
      draftState = null;
      submittedOperationId = "";
      forceEditor = true;
      showToast("配信PCへ反映しました。");
    } else if (submitted?.status === "rejected") {
      submittedOperationId = "";
      forceEditor = true;
      showToast(submitted.error || "配信PCへ反映できませんでした。", true);
    }
    rebuildDraftState();
    setConnection(bootstrap.snapshot ? "接続済み" : "配信PC待機中", true);
    render({ forceEditor });
    if (announce) showToast("最新状態を読み込みました。");
  } catch (error) {
    setConnection(error.message, false);
  } finally {
    requestPending = false;
  }
}

function setConnection(message, online) {
  elements.connectionState.textContent = message;
  elements.connectionState.classList.toggle("offline", !online);
}

async function sendOperation(operation, successMessage = "配信PCへ送信しました。") {
  if (submittedOperationId) {
    showToast("前の変更を配信PCへ反映中です。", true);
    return;
  }
  pendingOperations = upsertDraftOperation(pendingOperations, operation);
  rebuildDraftState();
  renderPending();
  refreshEditor({ force: true });
  showToast(successMessage.replace("送信しました", "下書きに追加しました"));
}

function liveSnapshot() {
  return bootstrap?.snapshot || { state: {}, master: {} };
}

function snapshot() {
  const live = liveSnapshot();
  return draftState ? { ...live, state: draftState } : live;
}

function rebuildDraftState() {
  const live = liveSnapshot();
  draftState = buildDraftState(live.state, pendingOperations, live.master);
}

function renderPending() {
  const count = pendingOperations.length;
  const waiting = Boolean(submittedOperationId);
  elements.pendingBar.classList.toggle("has-changes", count > 0);
  elements.pendingSummary.textContent = waiting
    ? `${count}件の変更を送信済み`
    : count
      ? `${count}件の未送信変更`
      : "未送信の変更はありません";
  elements.pendingDetail.textContent = waiting
    ? "配信PCでの反映完了を待っています。"
    : count
      ? "内容を確認し、「変更を送信」でまとめて即時反映します。"
      : "入力内容は、この端末内だけに保持されています。";
  elements.discard.disabled = count === 0 || waiting;
  elements.send.disabled = count === 0 || waiting;
  elements.send.textContent = waiting ? "反映待ち" : "変更を送信";
  elements.editor.toggleAttribute("inert", waiting);
}

async function submitPendingOperations() {
  if (!pendingOperations.length || submittedOperationId) return;
  try {
    const entry = await request(
      `/api/sessions/${encodeURIComponent(sessionId)}/operations?code=${encodeURIComponent(editorCode)}`,
      {
        method: "POST",
        body: JSON.stringify({
          operation: {
            type: "batch",
            operations: pendingOperations,
          },
        }),
      },
    );
    submittedOperationId = entry.id;
    renderPending();
    showToast("変更を送信しました。配信PCの反映を待っています。");
    await loadBootstrap();
  } catch (error) {
    showToast(error.message, true);
  }
}

function discardPendingOperations() {
  if (submittedOperationId) return;
  pendingOperations = [];
  draftState = null;
  renderPending();
  refreshEditor({ force: true });
  showToast("未送信の変更を破棄しました。");
}

function campaign(source = snapshot()) {
  const { state, master } = source;
  return (master.campaigns || []).find((item) => item.id === state.run?.campaignId) || master.campaigns?.[0] || null;
}

function findById(entries, id) {
  return (entries || []).find((item) => item.id === id);
}

function labelOf(item, fallback = "未選択") {
  return item?.shortTitle || item?.title || item?.name || item?.label || item?.effect || item?.bossName || fallback;
}

function selectedSpecial() {
  const { state } = snapshot();
  const campaignId = state.run?.campaignId;
  return state.run?.special?.[campaignId] || {};
}

function metric(label, value) {
  return node("div", { className: "metric" }, [
    node("span", { text: label }),
    node("strong", { text: value === null || value === undefined || value === "" ? "未入力" : String(value) }),
  ]);
}

function chip(text) {
  return node("span", { className: "chip", text });
}

function renderState() {
  const current = liveSnapshot();
  const { state, master } = current;
  const run = state.run || {};
  const currentCampaign = campaign(current);
  const squad = findById(master.squads, run.squadId || run.squad);
  const operators = (state.operators || []).map((id) => findById(master.operators, id)).filter(Boolean);
  const relics = (state.relics || []).map((id) => findById(master.relics, id)).filter(Boolean);
  const used = new Set(state.usedRelicIds || []);

  elements.playerLabel.textContent = bootstrap?.playerLabel || "大会入力";
  elements.stateSummary.replaceChildren(
    metric("統合戦略", labelOf(currentCampaign)),
    metric("源石錐", run.ingot ?? 0),
    metric("等級", run.difficulty),
    metric("分隊", labelOf(squad)),
  );

  elements.operatorCount.textContent = String(operators.reduce((total, item) =>
    total + Math.max(1, Number(state.operatorCounts?.[item.id]) || 1), 0));
  elements.selectedOperators.replaceChildren(...(
    operators.length
      ? operators.map((item) => {
        const count = Math.max(1, Number(state.operatorCounts?.[item.id]) || 1);
        return chip(`${labelOf(item)}${count > 1 ? ` ×${count}` : ""}`);
      })
      : [node("span", { className: "empty", text: "未選択" })]
  ));

  elements.relicCount.textContent = String(relics.length);
  elements.selectedRelics.replaceChildren(...(
    relics.length
      ? relics.map((item) => chip(`${labelOf(item)}${used.has(item.id) ? "（使用済み）" : ""}`))
      : [node("span", { className: "empty", text: "未選択" })]
  ));

  const selections = state.bossSelections?.[run.campaignId] || {};
  const selectedBossLabels = bossSections(currentCampaign, relics.map((item) => item.id))
    .map((section) => findById(section.options, selections[section.field]))
    .filter(Boolean)
    .map((item) => `${item.stageName || item.label || ""} / ${item.bossName || ""}`);
  elements.selectedBosses.replaceChildren(...(
    selectedBossLabels.length
      ? selectedBossLabels.map(chip)
      : [node("span", { className: "empty", text: "未選択" })]
  ));
}

function renderHistory() {
  const history = bootstrap?.history || [];
  elements.history.replaceChildren(...(
    history.length
      ? history.map((entry) => node("li", { className: entry.status }, [
        node("time", { text: new Date(entry.createdAt).toLocaleTimeString("ja-JP") }),
        node("strong", { text: entry.summary || operationLabel(entry.operation) }),
        node("span", {
          text: entry.status === "pending"
            ? "配信PCへ送信済み"
            : entry.status === "applied"
              ? "反映済み"
              : entry.error || "反映されませんでした",
        }),
      ]))
      : [node("li", {}, [node("span", { text: "操作履歴はありません。" })])]
  ));
}

function operationLabel(operation = {}) {
  if (operation.type === "batch") return `${operation.operations?.length || 0}件の入力を一括変更`;
  if (operation.type === "campaign.set") return "統合戦略を変更";
  if (operation.type === "run.set") return `ラン項目 ${operation.field} を変更`;
  if (operation.type === "special.set") return `特殊値 ${operation.field} を変更`;
  if (operation.type === "operator.set") return "オペレーターを変更";
  if (operation.type === "relic.set") return "秘宝を変更";
  if (operation.type === "boss.set") return "ボスを変更";
  if (operation.type === "run.clear") return "ランをクリア";
  return "入力を更新";
}

function field(label, control, action = null, help = "") {
  const children = [node("label", { text: label }), control];
  if (help) children.push(node("p", { className: "field-help", text: help }));
  if (action) children.push(node("div", { className: "field-actions" }, [action]));
  return node("div", { className: "field" }, children);
}

function selectControl(entries, selected, placeholder = "未選択") {
  const select = node("select");
  select.append(node("option", { value: "", text: placeholder }));
  for (const item of entries || []) {
    select.append(node("option", {
      value: item.id,
      text: labelOf(item),
      selected: item.id === selected,
    }));
  }
  return select;
}

function submitButton(label, handler) {
  return node("button", {
    type: "button",
    className: "command-button primary",
    text: label === "反映" ? "変更に追加" : label,
    onclick: handler,
  });
}

function renderRunEditor() {
  const { state, master } = snapshot();
  const run = state.run || {};
  const currentCampaign = campaign();
  const campaignSelect = selectControl(master.campaigns, currentCampaign?.id, "統合戦略を選択");
  campaignSelect.onchange = () => sendOperation({
    type: "campaign.set",
    campaignId: campaignSelect.value,
  }, "統合戦略の変更を送信しました。");

  const ingot = node("input", { type: "number", min: 0, max: 9999, value: run.ingot ?? 0 });
  const difficulty = node("input", { type: "number", min: 0, max: 99, value: run.difficulty ?? "" });
  const campaignSquads = (master.squads || []).filter((item) => !item.campaignId || item.campaignId === currentCampaign?.id);
  const squad = selectControl(campaignSquads, run.squadId || run.squad, "分隊を選択");
  const selectedSquad = findById(campaignSquads, run.squadId || run.squad);
  const squadRandomEffectOptions = selectedSquad?.randomEffectOptions || [];
  const squadRandomEffect = selectControl(
    squadRandomEffectOptions,
    run.squadRandomEffectOptionId,
    "追加効果を選択",
  );
  const tier = selectControl(difficultyTierEntries(master, currentCampaign?.id), run.difficultyTierId, "Tierを選択");
  const performances = (master.performances || []).filter((item) => !item.campaignId || item.campaignId === currentCampaign?.id);
  const performance = selectControl(performances, run.performanceId, "演目を選択");

  elements.editor.replaceChildren(node("div", { className: "form-grid" }, [
    field("統合戦略", campaignSelect),
    field("源石錐", ingot, submitButton("反映", () => sendOperation({
      type: "run.set", field: "ingot", value: ingot.value,
    }))),
    field("等級", difficulty, submitButton("反映", () => sendOperation({
      type: "run.set", field: "difficulty", value: difficulty.value,
    }))),
    field("分隊", squad, submitButton("反映", () => sendOperation({
      type: "run.set", field: "squadId", value: squad.value,
    }))),
    ...(squadRandomEffectOptions.length ? [
      field("分隊の追加効果", squadRandomEffect, submitButton("反映", () => sendOperation({
        type: "run.set", field: "squadRandomEffectOptionId", value: squadRandomEffect.value,
      }))),
    ] : []),
    field("等級Tier", tier, submitButton("反映", () => sendOperation({
      type: "run.set", field: "difficultyTierId", value: tier.value,
    }))),
    field("演目", performance, submitButton("反映", () => sendOperation({
      type: "run.set", field: "performanceId", value: performance.value,
    }))),
  ]));
}

function effectsFor(fieldDefinition) {
  const { master } = snapshot();
  const campaignId = campaign()?.id;
  return (master.selectableEffects || []).filter((item) =>
    item.campaignId === campaignId
      && (!fieldDefinition.effectSlot || item.slot === fieldDefinition.effectSlot));
}

function checkboxEditor(entries, selectedIds, onApply, label = "反映") {
  const selected = new Set(selectedIds || []);
  const list = node("div", { className: "checkbox-list" });
  for (const item of entries) {
    const input = node("input", { type: "checkbox", checked: selected.has(item.id) });
    input.onchange = () => input.checked ? selected.add(item.id) : selected.delete(item.id);
    list.append(node("label", { className: "checkbox-row" }, [
      input,
      node("span", {}, [
        node("strong", { text: labelOf(item) }),
        node("small", { text: item.effect || item.description || "" }),
      ]),
    ]));
  }
  return node("div", {}, [
    list,
    node("div", { className: "field-actions" }, [
      submitButton(label, () => onApply([...selected])),
    ]),
  ]);
}

function selectedOperatorTargets(value = {}) {
  const counts = new Map();
  for (const target of value.operatorTargets || []) {
    const id = target.operatorId || target.id;
    if (id) counts.set(id, Math.max(counts.get(id) || 0, Number(target.instance) || 1));
  }
  for (const id of value.operatorIds || []) if (!counts.has(id)) counts.set(id, 1);
  return counts;
}

function operatorTargetEditor(value, onApply, { effectEntries = null } = {}) {
  const { state, master } = snapshot();
  const recruited = new Set(state.operators || []);
  const available = (master.operators || []).filter((item) => recruited.has(item.id));
  const counts = selectedOperatorTargets(value);
  const container = node("div");
  let effectSelect = null;
  if (effectEntries) {
    effectSelect = selectControl(effectEntries, value?.effectId, "効果を選択");
    container.append(field("効果", effectSelect));
  }
  const list = node("div", { className: "catalog" });
  for (const item of available) {
    const selectedCount = counts.get(item.id) || 0;
    const checkbox = node("input", { type: "checkbox", checked: selectedCount > 0 });
    const count = node("input", {
      className: "count-control",
      type: "number",
      min: 1,
      max: Math.max(1, Number(state.operatorCounts?.[item.id]) || 1),
      value: selectedCount || 1,
    });
    checkbox.onchange = () => checkbox.checked ? counts.set(item.id, Number(count.value) || 1) : counts.delete(item.id);
    count.onchange = () => {
      if (checkbox.checked) counts.set(item.id, Number(count.value) || 1);
    };
    list.append(node("label", { className: "catalog-item" }, [
      checkbox,
      node("span", {}, [
        node("strong", { text: labelOf(item) }),
        node("small", { text: `${item.class || item.profession || ""} ${item.branch || item.archetype || ""}`.trim() }),
      ]),
      count,
    ]));
  }
  container.append(list);
  container.append(node("div", { className: "field-actions" }, [
    submitButton("反映", () => {
      const operatorTargets = [];
      for (const [operatorId, count] of counts) {
        for (let instance = 1; instance <= count; instance += 1) operatorTargets.push({ operatorId, instance });
      }
      onApply({
        ...(effectSelect ? { effectId: effectSelect.value || null } : {}),
        operatorIds: [...counts.keys()],
        operatorTargets,
      });
    }),
  ]));
  return container;
}

function stackLoadoutEditor(fieldDefinition, value, onApply) {
  const entries = effectsFor(fieldDefinition);
  const selected = new Map((Array.isArray(value) ? value : []).map((item) => [item.effectId || item.coinId, { ...item }]));
  const list = node("div", { className: "catalog" });
  for (const item of entries) {
    const current = selected.get(item.id);
    const count = node("input", {
      className: "count-control",
      type: "number",
      min: 0,
      max: 99,
      value: current?.count || 0,
    });
    count.onchange = () => {
      const nextCount = Math.max(0, Number(count.value) || 0);
      if (nextCount) selected.set(item.id, { ...(current || {}), effectId: item.id, coinId: item.id, count: nextCount });
      else selected.delete(item.id);
    };
    list.append(node("div", { className: `catalog-item${current ? " selected" : ""}` }, [
      node("span", {}, [
        node("strong", { text: labelOf(item) }),
        node("small", { text: item.effect || item.groupLabel || "" }),
      ]),
      count,
    ]));
  }
  return node("div", {}, [
    list,
    node("div", { className: "field-actions" }, [
      submitButton("反映", () => onApply([...selected.values()])),
    ]),
  ]);
}

function rankedEditor(fieldDefinition, value, onApply) {
  const effects = effectsFor(fieldDefinition);
  const groups = new Map();
  for (const effect of effects) {
    const key = effect.parentKey || effect.group || effect.id;
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(effect);
  }
  const draft = { ...(value || {}) };
  const fields = [];
  for (const [key, entries] of groups) {
    const select = selectControl(entries, draft[key], "なし");
    select.onchange = () => {
      if (select.value) draft[key] = select.value;
      else delete draft[key];
    };
    fields.push(field(entries[0]?.parentName || entries[0]?.groupLabel || key, select));
  }
  return node("div", {}, [
    node("div", { className: "form-grid" }, fields),
    node("div", { className: "field-actions" }, [submitButton("反映", () => onApply(draft))]),
  ]);
}

function revelationBoardEditor(fieldDefinition, value, onApply) {
  const effects = effectsFor(fieldDefinition);
  const byGroup = (labels) => effects.filter((item) => labels.includes(item.groupLabel));
  const cause = selectControl(byGroup(fieldDefinition.causeGroupLabels || ["本因"]), value?.causeId, "なし");
  const structure = selectControl(byGroup(fieldDefinition.structureGroupLabels || ["構成"]), value?.structureId, "なし");
  const rhetoric = byGroup(fieldDefinition.rhetoricGroupLabels || ["修辞"]);
  const rhetoricCounts = new Map((value?.rhetorics || []).map((item) => [item.effectId, item.count]));
  const rhetoricList = node("div", { className: "catalog" });
  for (const item of rhetoric) {
    const count = node("input", {
      className: "count-control",
      type: "number",
      min: 0,
      max: 99,
      value: rhetoricCounts.get(item.id) || 0,
    });
    count.onchange = () => rhetoricCounts.set(item.id, Math.max(0, Number(count.value) || 0));
    rhetoricList.append(node("div", { className: "catalog-item" }, [
      node("span", {}, [node("strong", { text: labelOf(item) }), node("small", { text: item.effect || "" })]),
      count,
    ]));
  }
  return node("div", {}, [
    node("div", { className: "form-grid" }, [
      field(fieldDefinition.causeLabel || "本因", cause),
      field(fieldDefinition.structureLabel || "構成", structure),
    ]),
    node("p", { className: "field-label", text: fieldDefinition.rhetoricLabel || "修辞" }),
    rhetoricList,
    node("div", { className: "field-actions" }, [submitButton("反映", () => onApply({
      causeId: cause.value || null,
      structureId: structure.value || null,
      rhetorics: [...rhetoricCounts].filter(([, count]) => count > 0).map(([effectId, count]) => ({ effectId, count })),
    }))]),
  ]);
}

function renderSpecialField(fieldDefinition, currentValue) {
  const apply = (value) => sendOperation({ type: "special.set", field: fieldDefinition.id, value });
  let editor;
  if (fieldDefinition.type === "number") {
    const input = node("input", {
      type: "number",
      min: fieldDefinition.min ?? 0,
      max: fieldDefinition.max ?? 9999,
      value: currentValue ?? 0,
    });
    editor = node("div", {}, [input, node("div", { className: "field-actions" }, [
      submitButton("反映", () => apply(input.value)),
    ])]);
  } else if (fieldDefinition.type === "effectSelect") {
    const select = selectControl(effectsFor(fieldDefinition), currentValue, "なし");
    editor = node("div", {}, [select, node("div", { className: "field-actions" }, [
      submitButton("反映", () => apply(select.value)),
    ])]);
  } else if (fieldDefinition.type === "effectMultiSelect") {
    editor = checkboxEditor(effectsFor(fieldDefinition), currentValue, apply);
  } else if (fieldDefinition.type === "effectRankedMultiSelect") {
    editor = rankedEditor(fieldDefinition, currentValue, apply);
  } else if (fieldDefinition.type === "effectStackLoadout" || fieldDefinition.type === "coinLoadout") {
    editor = stackLoadoutEditor(fieldDefinition, currentValue, apply);
  } else if (fieldDefinition.type === "revelationBoardLoadout") {
    editor = revelationBoardEditor(fieldDefinition, currentValue || {}, apply);
  } else if (fieldDefinition.type === "operatorMultiSelect") {
    editor = operatorTargetEditor(currentValue || {}, apply);
  } else if (fieldDefinition.type === "operatorEffectAssignment") {
    editor = operatorTargetEditor(currentValue || {}, apply, { effectEntries: effectsFor(fieldDefinition) });
  } else if (fieldDefinition.type === "textMultiSelect") {
    if (fieldDefinition.options?.length) {
      const options = fieldDefinition.options.map((item) =>
        typeof item === "string" ? { id: item, name: item } : { ...item, id: item.id || item.value || item.label });
      editor = checkboxEditor(options, currentValue, apply);
    } else {
      const input = node("textarea", { value: (currentValue || []).join("\n") });
      editor = node("div", {}, [input, node("div", { className: "field-actions" }, [
        submitButton("反映", () => apply(input.value.split(/\r?\n|,/).map((item) => item.trim()).filter(Boolean))),
      ])]);
    }
  } else if (fieldDefinition.type === "boolean" || fieldDefinition.type === "overlayToggle") {
    const checkbox = node("input", { type: "checkbox", checked: Boolean(currentValue) });
    editor = node("label", { className: "checkbox-row" }, [checkbox, node("span", { text: "有効" })]);
    checkbox.onchange = () => apply(checkbox.checked);
  } else {
    editor = node("p", { className: "empty", text: `未対応形式: ${fieldDefinition.type}` });
  }
  return node("section", { className: "editor-section" }, [
    node("h3", { text: fieldDefinition.label }),
    fieldDefinition.description ? node("p", { className: "field-help", text: fieldDefinition.description }) : null,
    editor,
  ]);
}

function renderSpecialEditor() {
  const currentCampaign = campaign();
  const values = selectedSpecial();
  const fields = currentCampaign?.specialFields || [];
  elements.editor.replaceChildren(...(
    fields.length
      ? fields.map((definition) => renderSpecialField(definition, values[definition.id]))
      : [node("p", { className: "empty", text: "この統合戦略に特殊値はありません。" })]
  ));
}

function filterSelect(label, value, options, onChange, className = "") {
  const select = node("select", {
    className: "catalog-filter-select",
    title: label,
  });
  for (const option of options) {
    select.append(node("option", {
      value: option.value,
      text: option.label,
      selected: String(option.value) === String(value),
    }));
  }
  select.onchange = () => onChange(select.value);
  return node("label", { className: `catalog-filter${className ? ` ${className}` : ""}` }, [
    node("span", { text: label }),
    select,
  ]);
}

function filterToggle(label, checked, onChange) {
  const input = node("input", { type: "checkbox", checked });
  input.onchange = () => onChange(input.checked);
  return node("label", { className: "catalog-toggle" }, [
    input,
    node("span", { text: label }),
  ]);
}

function catalogToolbar(kind, placeholder, view, rerender) {
  const filters = catalogFilters[kind];
  const input = node("input", {
    className: "search-input",
    type: "search",
    placeholder,
    value: filters.search,
    dataset: { filterSearch: kind },
  });
  input.oninput = () => {
    filters.search = input.value;
    rerender();
    const next = elements.editor.querySelector(`[data-filter-search="${kind}"]`);
    next?.focus();
    next?.setSelectionRange(filters.search.length, filters.search.length);
  };
  const setFilter = (key, value) => {
    filters[key] = value;
    rerender();
  };
  const controls = kind === "operators"
    ? [
      filterSelect("職業", filters.className, [
        { value: "all", label: "すべて" },
        ...view.options.classes.map((value) => ({ value, label: value })),
      ], (value) => setFilter("className", value)),
      filterSelect("職分", filters.branch, [
        { value: "all", label: "すべて" },
        ...view.options.branches.map((value) => ({ value, label: value })),
      ], (value) => setFilter("branch", value), "wide"),
      filterSelect("レア度", filters.rarity, [
        { value: "all", label: "すべて" },
        ...view.options.rarity.map((value) => ({ value: String(value), label: `★${value}` })),
      ], (value) => setFilter("rarity", value)),
      filterSelect("並び順", filters.sort, [
        { value: "rarity", label: "レア度順" },
        { value: "class", label: "職業・職分順" },
        { value: "name", label: "名前順" },
      ], (value) => setFilter("sort", value), "wide"),
    ]
    : [
      filterSelect("秘宝種別", filters.category, [
        { value: "all", label: "すべて" },
        ...view.options.categories.map((value) => ({ value, label: value })),
      ], (value) => setFilter("category", value), "category"),
      filterSelect("並び順", filters.sort, [
        { value: "category", label: "秘宝種別順" },
        { value: "number", label: "番号順" },
        { value: "name", label: "名前順" },
      ], (value) => setFilter("sort", value), "wide"),
    ];
  controls.push(filterSelect(
    "表示列",
    filters.columns,
    [1, 2, 3, 4].map((value) => ({ value: String(value), label: `${value}列` })),
    (value) => setFilter("columns", Number(value)),
  ));

  return node("div", { className: "catalog-toolbar" }, [
    node("div", { className: "catalog-search-row" }, [
      input,
      node("span", {
        className: "catalog-summary",
        text: `${view.items.length}件 / 選択${view.selectedCount}${kind === "operators" ? "名" : "件"} / 全${view.total}件`,
      }),
    ]),
    node("div", { className: "catalog-filter-row" }, controls),
    node("div", { className: "catalog-toggle-row" }, [
      filterToggle("選択を先頭", filters.selectedFirst, (value) => setFilter("selectedFirst", value)),
      filterToggle("選択のみ", filters.selectedOnly, (value) => setFilter("selectedOnly", value)),
    ]),
  ]);
}

function renderOperatorEditor() {
  const { state, master } = snapshot();
  const selected = new Set(state.operators || []);
  const promotionLevels = state.operatorPromotionLevels || {};
  const view = buildOperatorCatalogView(
    master.operators || [],
    state.operators || [],
    catalogFilters.operators,
  );
  Object.assign(catalogFilters.operators, view.filters);
  const entries = view.items;
  const catalog = node("div", { className: "catalog" });
  catalog.style.setProperty("--catalog-columns", String(view.filters.columns));
  for (const item of entries) {
    const isSelected = selected.has(item.id);
    const count = Math.max(1, Number(state.operatorCounts?.[item.id]) || 1);
    catalog.append(node("button", {
      type: "button",
      className: `catalog-item${isSelected ? " selected" : ""}`,
      onclick: () => sendOperation({
        type: "operator.set",
        operatorId: item.id,
        selected: !isSelected,
        count,
      }),
    }, [
      node("span", {}, [
        node("strong", { text: labelOf(item) }),
        node("small", {
          text: [
            item.rarity ? `★${item.rarity}` : "",
            item.class || item.profession || "",
            item.branch || item.archetype || "",
            count > 1 ? `×${count}` : "",
          ].filter(Boolean).join(" / "),
        }),
      ]),
    ]));
  }
  const countEditors = (state.operators || [])
    .map((id) => findById(master.operators, id))
    .filter((item) => item && Math.max(1, Number(state.operatorCounts?.[item.id]) || 1) > 1);
  const promotionEditors = (state.operators || [])
    .map((id) => findById(master.operators, id))
    .filter((item) => item && Number(item.rarity) >= 4);
  elements.editor.replaceChildren(
    catalogToolbar("operators", "名前・職業・職分で検索", view, renderOperatorEditor),
    countEditors.length
      ? node("section", { className: "editor-section" }, [
        node("h3", { text: "複数人数" }),
        node("div", { className: "form-grid" }, countEditors.map((item) => {
          const input = node("input", {
            type: "number", min: 1, max: 99, value: state.operatorCounts?.[item.id] || 1,
          });
          return field(labelOf(item), input, submitButton("反映", () => sendOperation({
            type: "operator.set", operatorId: item.id, selected: true, count: input.value,
          })));
        })),
      ])
      : null,
    promotionEditors.length
      ? node("section", { className: "editor-section" }, [
        node("h3", { text: "昇進状態" }),
        node("p", { className: "field-help", text: "星4以上の選択済みオペレーターだけ変更できます。星3以下は昇進1固定です。" }),
        node("div", { className: "promotion-grid" }, promotionEditors.map((item) => {
          const isEliteTwo = Number(promotionLevels[item.id]) >= 2;
          const count = Math.max(1, Number(state.operatorCounts?.[item.id]) || 1);
          return node("div", { className: "promotion-row" }, [
            node("span", {}, [
              node("strong", { text: labelOf(item) }),
              node("small", { text: isEliteTwo ? "昇進2" : "昇進1" }),
            ]),
            node("button", {
              type: "button",
              className: isEliteTwo ? "promotion-toggle active" : "promotion-toggle",
              text: isEliteTwo ? "昇進2" : "昇進1",
              onclick: () => sendOperation({
                type: "operator.set",
                operatorId: item.id,
                selected: true,
                count,
                promotionLevel: isEliteTwo ? 1 : 2,
              }),
            }),
          ]);
        })),
      ])
      : null,
    entries.length
      ? catalog
      : node("p", { className: "empty catalog-empty", text: "条件に一致するオペレーターはいません。" }),
  );
}

function renderRelicEditor() {
  const { state, master } = snapshot();
  const selected = new Set(state.relics || []);
  const used = new Set(state.usedRelicIds || []);
  const view = buildRelicCatalogView(
    master.relics || [],
    state.relics || [],
    state.run?.campaignId || "",
    catalogFilters.relics,
  );
  Object.assign(catalogFilters.relics, view.filters);
  const entries = view.items;
  const catalog = node("div", { className: "catalog" });
  catalog.style.setProperty("--catalog-columns", String(view.filters.columns));
  for (const item of entries) {
    const isSelected = selected.has(item.id);
    const button = node("button", {
      type: "button",
      className: `catalog-item${isSelected ? " selected" : ""}`,
      onclick: () => sendOperation({
        type: "relic.set", relicId: item.id, selected: !isSelected, used: false,
      }),
    }, [
      node("span", {}, [
        node("strong", { text: labelOf(item) }),
        node("small", { text: [item.number ? `No.${item.number}` : "", item.category || "", used.has(item.id) ? "使用済み" : ""].filter(Boolean).join(" / ") }),
      ]),
    ]);
    if (isSelected) {
      const usedToggle = node("input", { type: "checkbox", checked: used.has(item.id), title: "使用済み" });
      usedToggle.onclick = (event) => event.stopPropagation();
      usedToggle.onchange = () => sendOperation({
        type: "relic.set", relicId: item.id, selected: true, used: usedToggle.checked,
      });
      button.append(node("label", { className: "used-toggle", onclick: (event) => event.stopPropagation() }, [
        usedToggle, node("span", { text: "使用済み" }),
      ]));
    }
    catalog.append(button);
  }
  elements.editor.replaceChildren(
    catalogToolbar("relics", "秘宝名・番号・カテゴリ・効果で検索", view, renderRelicEditor),
    entries.length
      ? catalog
      : node("p", { className: "empty catalog-empty", text: "条件に一致する秘宝はありません。" }),
  );
}

function bossSections(currentCampaign, selectedRelicIds = []) {
  const bossFlags = currentCampaign?.bossFlags;
  if (!bossFlags) return [];
  const sections = [];
  if (Array.isArray(bossFlags.manualSections)) sections.push(...bossFlags.manualSections);
  else {
    for (const value of Object.values(bossFlags)) {
      if (value && typeof value === "object" && value.field && Array.isArray(value.options)) sections.push(value);
    }
  }
  const relicSet = new Set(selectedRelicIds);
  return sections.filter((section) => !section.visibleWhenRelicId || relicSet.has(section.visibleWhenRelicId));
}

function renderBossEditor() {
  const { state } = snapshot();
  const currentCampaign = campaign();
  const selections = state.bossSelections?.[currentCampaign?.id] || {};
  const sections = bossSections(currentCampaign, state.relics || []);
  const cards = sections.map((section) => {
    const select = selectControl(section.options || [], selections[section.field], "未選択");
    return field(section.label, select, submitButton("反映", () => sendOperation({
      type: "boss.set", field: section.field, value: select.value || null,
    })), section.helper || "");
  });
  elements.editor.replaceChildren(...(
    cards.length
      ? [node("div", { className: "form-grid" }, cards)]
      : [node("p", { className: "empty", text: "現在選択できるボス項目はありません。" })]
  ));
}

function renderEditor() {
  if (!bootstrap?.snapshot) {
    elements.editor.replaceChildren(node("p", {
      className: "empty",
      text: "配信PCが状態を送信するまでお待ちください。",
    }));
    return;
  }
  if (activeTab === "run") renderRunEditor();
  else if (activeTab === "special") renderSpecialEditor();
  else if (activeTab === "operators") renderOperatorEditor();
  else if (activeTab === "relics") renderRelicEditor();
  else renderBossEditor();
}

function editorIsActive() {
  const activeElement = document.activeElement;
  return Boolean(activeElement && elements.editor.contains(activeElement));
}

function refreshEditor({ force = false } = {}) {
  const nextSignature = createEditorStateSignature(snapshot());
  const decision = decideEditorRefresh({
    previousSignature: editorRenderSignature,
    nextSignature,
    force,
    editorActive: editorIsActive(),
  });
  if (decision === "skip") return;
  if (decision === "defer") {
    editorRefreshPending = true;
    return;
  }
  renderEditor();
  editorRenderSignature = nextSignature;
  editorRefreshPending = false;
}

function render({ forceEditor = false } = {}) {
  renderState();
  renderHistory();
  renderPending();
  refreshEditor({ force: forceEditor });
}

elements.tabs.addEventListener("click", (event) => {
  const button = event.target.closest("[data-tab]");
  if (!button) return;
  activeTab = button.dataset.tab;
  for (const tab of elements.tabs.querySelectorAll("[data-tab]")) {
    tab.classList.toggle("active", tab.dataset.tab === activeTab);
  }
  refreshEditor({ force: true });
});

elements.editor.addEventListener("focusout", () => {
  setTimeout(() => {
    if (editorRefreshPending && !editorIsActive()) refreshEditor({ force: true });
  }, 0);
});

elements.refresh.addEventListener("click", () => loadBootstrap({ announce: true, forceEditor: true }));
elements.send.addEventListener("click", submitPendingOperations);
elements.discard.addEventListener("click", discardPendingOperations);
elements.clearRun.addEventListener("click", () => {
  if (confirm("現在のラン入力をすべてクリアします。よろしいですか？")) {
    sendOperation({ type: "run.clear" }, "ランのクリアを送信しました。");
  }
});

loadBootstrap();
pollTimer = setInterval(loadBootstrap, 750);
window.addEventListener("beforeunload", () => clearInterval(pollTimer));
