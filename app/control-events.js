import * as controlActions from "./control-actions.js";
import { clampCoinCount, normalizeCoinFace } from "./domain/special-values.js";
import { adbDetectUrl, adbSelectPathUrl, adbTestUrl, apiJson, glmOcrInstallUrl, glmOcrOllamaInstallUrl, glmOcrOllamaStartUrl, glmOcrOllamaStatusUrl, glmOcrOllamaUninstallUrl, glmOcrStatusUrl, glmOcrUninstallUrl, hypervisorStatusUrl, resetStateUrl } from "./lib/api.js";

function parseImportDraft(ui) {
  if (!ui.importDraft.trim()) throw new Error("JSONが空です");
  const parsed = JSON.parse(ui.importDraft);
  if (!parsed || typeof parsed !== "object") throw new Error("状態JSONではありません");
  return parsed;
}

async function postAdbDetect(settings) {
  return apiJson(adbDetectUrl, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ settings }),
  });
}

async function postAdbTest(settings, { capture = false } = {}) {
  return apiJson(adbTestUrl, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ settings, capture }),
  });
}

async function postAdbPathPicker() {
  return apiJson(adbSelectPathUrl, { method: "POST" });
}

async function getHypervisorStatus() {
  return apiJson(hypervisorStatusUrl);
}

async function getGlmOcrStatus() {
  return apiJson(glmOcrStatusUrl);
}

async function postGlmOcrInstall() {
  return apiJson(glmOcrInstallUrl, { method: "POST" });
}

async function postGlmOcrUninstall() {
  return apiJson(glmOcrUninstallUrl, { method: "POST" });
}

async function getGlmOcrOllamaStatus() {
  return apiJson(glmOcrOllamaStatusUrl);
}

async function postGlmOcrOllamaInstall() {
  return apiJson(glmOcrOllamaInstallUrl, { method: "POST" });
}

async function postGlmOcrOllamaStart() {
  return apiJson(glmOcrOllamaStartUrl, { method: "POST" });
}

async function postGlmOcrOllamaUninstall() {
  return apiJson(glmOcrOllamaUninstallUrl, { method: "POST" });
}

function stopGlmOcrStatusPolling(context) {
  if (context.ui.glmOcrStatusTimer) clearInterval(context.ui.glmOcrStatusTimer);
  context.ui.glmOcrStatusTimer = null;
}

async function refreshGlmOcrStatus(context, { render = true } = {}) {
  context.ui.glmOcrStatus = await getGlmOcrStatus();
  if (!context.ui.glmOcrStatus?.installing) stopGlmOcrStatusPolling(context);
  if (render) context.renderControl();
  return context.ui.glmOcrStatus;
}

function startGlmOcrStatusPolling(context) {
  stopGlmOcrStatusPolling(context);
  context.ui.glmOcrStatusTimer = setInterval(() => {
    refreshGlmOcrStatus(context).catch(() => {});
  }, 2000);
}

function stopGlmOcrOllamaStatusPolling(context) {
  if (context.ui.ollamaStatusTimer) clearInterval(context.ui.ollamaStatusTimer);
  context.ui.ollamaStatusTimer = null;
}

async function refreshGlmOcrOllamaStatus(context, { render = true } = {}) {
  context.ui.ollamaStatus = await getGlmOcrOllamaStatus();
  if (!context.ui.ollamaStatus?.installing) stopGlmOcrOllamaStatusPolling(context);
  if (render) context.renderControl();
  return context.ui.ollamaStatus;
}

function startGlmOcrOllamaStatusPolling(context) {
  stopGlmOcrOllamaStatusPolling(context);
  context.ui.ollamaStatusTimer = setInterval(() => {
    refreshGlmOcrOllamaStatus(context).catch(() => {});
  }, 2500);
}

export function getChoiceActive(type, id, state, context = {}) {
  if (typeof context.getChoiceActive === "function") return context.getChoiceActive(type, id);
  const key = type === "relic" ? "relics" : "operators";
  return (state[key] || []).includes(id);
}

export function markChoiceRenderDirtyAfterStateReplace(ui) {
  ui.forceFullChoiceRender = true;
}

function replaceControlState(context, nextState) {
  context.replaceState(nextState);
  markChoiceRenderDirtyAfterStateReplace(context.ui);
}

function reloadAfterReset(context) {
  if (typeof context.reloadView !== "function") return;
  setTimeout(() => context.reloadView(), 0);
}

function toggleChoiceElement(element, type, id, context) {
  context.mutate((state) => controlActions.toggleChoice(state, type, id));
  context.ui.forceFullChoiceRender = false;
}

function isControlView(context) {
  return context.view === "sidecar";
}

export function registerControlEvents(app, context) {
  app.addEventListener("click", async (event) => {
    const button = event.target.closest("[data-action]");
    if (!button || !isControlView(context)) return;
    const action = button.dataset.action;
    const id = button.dataset.id;

    if (action === "add-special-effect") {
      const fieldId = button.dataset.specialPickerField;
      const container = button.closest("[data-special-picker]");
      const value = container?.querySelector("[data-special-picker-select]")?.value;
      if (fieldId && value) {
        context.mutate((state) => controlActions.addSpecialEffect(state, context.getCampaign().id, fieldId, value));
      }
      return;
    }
    if (action === "remove-special-effect") {
      const fieldId = button.dataset.specialPickerField;
      if (fieldId && id) {
        context.mutate((state) => controlActions.removeSpecialEffect(state, context.getCampaign().id, fieldId, id));
      }
      return;
    }
    if (action === "add-revelation-board-rhetoric") {
      const fieldId = button.dataset.revelationBoardField;
      const rhetoricId = button.dataset.rhetoricId;
      if (fieldId && rhetoricId) {
        const campaignId = context.getCampaign().id;
        const fieldConfig = context.getSpecialFieldConfig(campaignId, fieldId) || { id: fieldId };
        context.mutate((state) => controlActions.addRevelationBoardRhetoric(state, campaignId, fieldId, rhetoricId, fieldConfig, context.normalizeRevelationBoardValue));
      }
      return;
    }
    if (action === "remove-revelation-board-rhetoric") {
      const fieldId = button.dataset.revelationBoardField;
      const index = Number(button.dataset.index);
      if (fieldId && Number.isInteger(index)) {
        const campaignId = context.getCampaign().id;
        const fieldConfig = context.getSpecialFieldConfig(campaignId, fieldId) || { id: fieldId };
        context.mutate((state) => controlActions.removeRevelationBoardRhetoric(state, campaignId, fieldId, index, fieldConfig, context.normalizeRevelationBoardValue));
      }
      return;
    }
    if (action === "add-effect-stack-entry") {
      const fieldId = button.dataset.effectStackField;
      const container = button.closest("[data-effect-stack-builder]");
      const effectId = container?.querySelector('[data-effect-stack-input="effect"]')?.value;
      if (fieldId && effectId) {
        const campaignId = context.getCampaign().id;
        const fieldConfig = context.getSpecialFieldConfig(campaignId, fieldId) || { id: fieldId };
        const count = clampCoinCount(container?.querySelector('[data-effect-stack-input="count"]')?.value);
        const stateId = fieldConfig.hideStateInput
          ? context.getStackEmptyStateId(fieldConfig)
          : context.normalizeStackState(fieldConfig, container?.querySelector('[data-effect-stack-input="state"]')?.value, campaignId);
        context.mutate((state) => controlActions.addEffectStackEntry(state, campaignId, fieldId, { effectId, count, stateId }, fieldConfig, context.mergeEffectStackEntries));
      }
      return;
    }
    if (action === "remove-effect-stack-entry") {
      const fieldId = button.dataset.effectStackField;
      const index = Number(button.dataset.index);
      if (fieldId && Number.isInteger(index)) {
        context.mutate((state) => controlActions.removeEffectStackEntry(state, context.getCampaign().id, fieldId, index));
      }
      return;
    }
    if (action === "add-coin-entry") {
      const fieldId = button.dataset.coinField;
      const container = button.closest("[data-coin-builder]");
      const coinId = container?.querySelector('[data-coin-input="coin"]')?.value;
      if (fieldId && coinId) {
        const count = clampCoinCount(container?.querySelector('[data-coin-input="count"]')?.value);
        const statusId = container?.querySelector('[data-coin-input="status"]')?.value || null;
        const face = normalizeCoinFace(container?.querySelector('[data-coin-input="face"]')?.value);
        context.mutate((state) => controlActions.addCoinEntry(state, context.getCampaign().id, fieldId, { coinId, count, statusId, face }));
      }
      return;
    }
    if (action === "remove-coin-entry") {
      const fieldId = button.dataset.coinField;
      const index = Number(button.dataset.index);
      if (fieldId && Number.isInteger(index)) {
        context.mutate((state) => controlActions.removeCoinEntry(state, context.getCampaign().id, fieldId, index));
      }
      return;
    }
    if (action === "toggle-relic") { toggleChoiceElement(button, "relic", id, context); return; }
    if (action === "toggle-relic-used") { context.mutate((state) => controlActions.toggleRelicUsed(state, id)); return; }
    if (action === "toggle-operator") { toggleChoiceElement(button, "operator", id, context); return; }
    if (action === "toggle-relic-excluded") { context.mutate((state) => controlActions.toggleChoiceExcluded(state, "relic", id)); return; }
    if (action === "toggle-operator-excluded") { context.mutate((state) => controlActions.toggleChoiceExcluded(state, "operator", id)); return; }
    if (action === "clear-relics") context.mutate(controlActions.clearRelics);
    if (action === "adb-browse-path") {
      button.disabled = true;
      context.setNotice("ADB実行ファイルを選択してください。");
      try {
        const result = await postAdbPathPicker();
        if (!result.canceled && result.path) {
          context.mutate((state) => controlActions.updateAdbSetting(state, "adbPath", result.path));
          context.setNotice("ADBパスを反映しました。");
        } else {
          context.setNotice("ADBパス選択をキャンセルしました。");
        }
      } catch (error) {
        context.setNotice(`ADBパス選択失敗: ${error.message}`);
      } finally {
        button.disabled = false;
      }
      return;
    }
    if (action === "adb-detect") {
      button.disabled = true;
      context.setNotice("ADB接続候補を検出しています。");
      try {
        context.ui.adbDetection = await postAdbDetect(context.getState().adb);
        context.ui.adbTestResult = null;
        context.renderControl();
        const deviceCount = context.ui.adbDetection?.devices?.length || 0;
        context.setNotice(`ADB検出完了: 端末${deviceCount}件`);
      } catch (error) {
        context.ui.adbDetection = null;
        context.setNotice(`ADB検出失敗: ${error.message}`);
      } finally {
        button.disabled = false;
      }
      return;
    }
    if (action === "hypervisor-check") {
      button.disabled = true;
      context.setNotice("Hyper-V / CPU仮想化状態を確認しています。");
      try {
        context.ui.hypervisorStatus = await getHypervisorStatus();
        context.renderControl();
        context.setNotice(context.ui.hypervisorStatus?.message || "Hyper-V確認が完了しました。");
      } catch (error) {
        context.ui.hypervisorStatus = { severity: "warning", message: error.message };
        context.renderControl();
        context.setNotice(`Hyper-V確認失敗: ${error.message}`);
      } finally {
        button.disabled = false;
      }
      return;
    }
    if (action === "glm-ocr-refresh") {
      button.disabled = true;
      context.setNotice("GLM-OCRランタイム状態を確認しています。");
      try {
        const status = await refreshGlmOcrStatus(context);
        await refreshGlmOcrOllamaStatus(context);
        context.setNotice(status.message || "GLM-OCRランタイム状態を確認しました。");
      } catch (error) {
        context.setNotice(`GLM-OCR状態確認失敗: ${error.message}`);
      } finally {
        button.disabled = false;
      }
      return;
    }
    if (action === "glm-ocr-install") {
      button.disabled = true;
      context.setNotice("GLM-OCRランタイムのダウンロード/インストールを開始しています。");
      try {
        context.ui.glmOcrStatus = await postGlmOcrInstall();
        context.renderControl();
        startGlmOcrStatusPolling(context);
        context.setNotice("GLM-OCRランタイムのインストールを開始しました。完了まで時間がかかる場合があります。");
      } catch (error) {
        context.setNotice(`GLM-OCRインストール開始失敗: ${error.message}`);
      } finally {
        button.disabled = false;
      }
      return;
    }
    if (action === "glm-ocr-uninstall") {
      if (!confirm("GLM-OCRランタイムを削除しますか？モデル/キャッシュを含む実行フォルダを削除します。")) return;
      button.disabled = true;
      context.setNotice("GLM-OCRランタイムを削除しています。");
      try {
        stopGlmOcrStatusPolling(context);
        context.ui.glmOcrStatus = await postGlmOcrUninstall();
        context.renderControl();
        context.setNotice("GLM-OCRランタイムを削除しました。");
      } catch (error) {
        context.setNotice(`GLM-OCR削除失敗: ${error.message}`);
      } finally {
        button.disabled = false;
      }
      return;
    }
    if (action === "glm-ocr-ollama-refresh") {
      button.disabled = true;
      context.setNotice("Ollamaローカル実行状態を確認しています。");
      try {
        const status = await refreshGlmOcrOllamaStatus(context);
        context.setNotice(status.message || "Ollama状態を確認しました。");
      } catch (error) {
        context.setNotice(`Ollama状態確認失敗: ${error.message}`);
      } finally {
        button.disabled = false;
      }
      return;
    }
    if (action === "glm-ocr-ollama-install") {
      button.disabled = true;
      context.setNotice("OllamaとGLM-OCRモデルのダウンロード/インストールを開始しています。");
      try {
        context.ui.ollamaStatus = await postGlmOcrOllamaInstall();
        context.renderControl();
        startGlmOcrOllamaStatusPolling(context);
        context.setNotice("OllamaとGLM-OCRモデルの導入を開始しました。初回は大容量ダウンロードになります。");
      } catch (error) {
        context.setNotice(`Ollama導入開始失敗: ${error.message}`);
      } finally {
        button.disabled = false;
      }
      return;
    }
    if (action === "glm-ocr-ollama-start") {
      button.disabled = true;
      context.setNotice("Ollamaサーバーを起動しています。");
      try {
        context.ui.ollamaStatus = await postGlmOcrOllamaStart();
        context.renderControl();
        context.setNotice(context.ui.ollamaStatus?.message || "Ollamaサーバーを起動しました。");
      } catch (error) {
        context.setNotice(`Ollama起動失敗: ${error.message}`);
      } finally {
        button.disabled = false;
      }
      return;
    }
    if (action === "glm-ocr-ollama-uninstall") {
      if (!confirm("OllamaランタイムとGLM-OCRモデルを削除しますか？モデル保存フォルダを含む実行フォルダを削除します。")) return;
      button.disabled = true;
      context.setNotice("Ollamaランタイムを削除しています。");
      try {
        stopGlmOcrOllamaStatusPolling(context);
        context.ui.ollamaStatus = await postGlmOcrOllamaUninstall();
        context.renderControl();
        context.setNotice("Ollamaランタイムを削除しました。");
      } catch (error) {
        context.setNotice(`Ollama削除失敗: ${error.message}`);
      } finally {
        button.disabled = false;
      }
      return;
    }
    if (action === "adb-test" || action === "adb-screenshot-test") {
      button.disabled = true;
      const capture = action === "adb-screenshot-test";
      context.setNotice(capture ? "ADBスクリーンショットテストを実行しています。" : "ADB接続テストを実行しています。");
      try {
        context.ui.adbTestResult = await postAdbTest(context.getState().adb, { capture });
        context.renderControl();
        const path = context.ui.adbTestResult?.screenshot?.path;
        context.setNotice(path ? `ADBスクリーンショットを保存しました: ${path}` : "ADBテストが完了しました。");
      } catch (error) {
        context.ui.adbTestResult = { ok: false, error: error.message };
        context.renderControl();
        context.setNotice(`ADBテスト失敗: ${error.message}`);
      } finally {
        button.disabled = false;
      }
      return;
    }
    if (action === "adb-use-candidate") {
      const adbPath = button.dataset.adbPath || "";
      context.mutate((state) => controlActions.updateAdbSetting(state, "adbPath", adbPath));
      context.setNotice("ADBパスを反映しました。");
      return;
    }
    if (action === "adb-use-device") {
      const serial = button.dataset.adbSerial || "";
      context.mutate((state) => controlActions.updateAdbSetting(state, "serial", serial));
      context.setNotice("接続先を反映しました。");
      return;
    }
    if (action === "reset-state") {
      if (confirm("状態を初期化しますか？")) {
        replaceControlState(context, await apiJson(resetStateUrl, { method: "POST" }));
        context.setNotice("状態を初期化しました。画面を再読み込みします。");
        reloadAfterReset(context);
      }
      return;
    }
    if (action === "add-boss-flag") {
      const text = context.ui.bossDraft.trim();
      if (text) context.mutate((state) => { controlActions.addBossFlag(state, text); context.ui.bossDraft = ""; });
    }
    if (action === "remove-boss-flag") context.mutate((state) => controlActions.removeBossFlag(state, Number(button.dataset.index)));
    if (action === "dismiss-suggestion") context.mutate((state) => controlActions.dismissSuggestion(state, Number(button.dataset.index)));
    if (action === "copy-text") {
      const value = button.dataset.value || "";
      if (!value) return;
      await navigator.clipboard.writeText(value);
      context.setNotice(`${button.dataset.copyLabel || "URL"}をコピーしました。`);
      return;
    }
    if (action === "copy-state-json") {
      await navigator.clipboard.writeText(JSON.stringify(context.getState(), null, 2));
      context.setNotice("状態JSONをコピーしました。");
    }
    if (action === "import-state-now") {
      try {
        replaceControlState(context, parseImportDraft(context.ui));
        context.renderControl();
        context.scheduleSave();
        context.setNotice("JSONを直接反映しました。");
      } catch (error) { context.setNotice(error.message); }
    }
    if (action === "submit-tournament-state") {
      try {
        const pending = parseImportDraft(context.ui);
        context.mutate((state) => controlActions.holdTournamentState(state, pending));
        context.setNotice("大会入力として保留しました。ボス/大会タブで反映できます。");
      } catch (error) { context.setNotice(error.message); }
    }
    if (action === "approve-tournament") {
      const pending = context.getState().tournament?.pendingState;
      if (pending) {
        replaceControlState(context, pending);
        context.getState().tournament = { pendingState: null, lastSubmissionAt: null, submittedBy: null };
        context.renderControl();
        context.scheduleSave();
        context.setNotice("大会入力を反映しました。");
      }
    }
    if (action === "reject-tournament") context.mutate(controlActions.clearTournamentState);
  });

  app.addEventListener("keydown", (event) => {
    if (!isControlView(context)) return;
    if (event.key !== "Enter" && event.key !== " ") return;
    const target = event.target.closest('.operator-choice[data-action="toggle-operator"], .relic-choice[data-action="toggle-relic"]');
    if (!target) return;
    event.preventDefault();
    const id = target.dataset.id;
    if (target.dataset.action === "toggle-relic") {
      toggleChoiceElement(target, "relic", id, context);
    } else {
      toggleChoiceElement(target, "operator", id, context);
    }
  });

  app.addEventListener("input", (event) => {
    if (!isControlView(context)) return;
    const target = event.target;
    if (target.matches("[data-adb-setting]")) {
      context.ui.adbDetection = null;
      context.ui.adbTestResult = null;
      context.mutate((state) => controlActions.updateAdbSetting(state, target.dataset.adbSetting, target.value, target.checked), { render: false });
      return;
    }
    if (!target.matches("[data-ui]")) return;
    const key = target.dataset.ui;
    context.ui[key] = target.value;
    if (key === "relicSearch" && !event.isComposing) context.renderControl();
  });

  app.addEventListener("compositionend", (event) => {
    if (!isControlView(context)) return;
    const target = event.target;
    if (!target.matches('[data-ui="relicSearch"]')) return;
    context.ui.relicSearch = target.value;
    context.renderControl();
  });

  app.addEventListener("change", (event) => {
    if (!isControlView(context)) return;
    const target = event.target;
    if (target.matches("[data-adb-setting]")) {
      context.ui.adbDetection = null;
      context.ui.adbTestResult = null;
      context.mutate((state) => controlActions.updateAdbSetting(state, target.dataset.adbSetting, target.value, target.checked));
      return;
    }
    if (target.matches("[data-ui]")) {
      context.ui[target.dataset.ui] = target.value;
      context.renderControl();
      return;
    }
    const field = target.dataset.field;
    if (field) {
      context.mutate((state) => controlActions.updateRunField(state, field, target.value, target.checked));
      return;
    }
    const bossSelect = target.dataset.bossSelect;
    if (bossSelect) {
      context.mutate((state) => controlActions.updateBossSelect(state, context.getCampaign().id, bossSelect, target.value));
    }
    const bossToggle = target.dataset.bossToggle;
    if (bossToggle) {
      context.mutate((state) => controlActions.updateBossToggle(state, context.getCampaign().id, bossToggle, target.value, target.checked));
    }
    const specialVisibility = target.dataset.specialVisibility;
    if (specialVisibility) {
      const campaign = context.getCampaign();
      const fieldConfig = (campaign.specialFields || []).find((field) => field.id === specialVisibility) || { id: specialVisibility };
      const key = context.getSpecialOverlayToggleKey(fieldConfig);
      context.mutate((state) => controlActions.updateSpecialVisibility(state, campaign.id, key, target.checked));
    }
    const revelationBoardSelect = target.dataset.revelationBoardSelect;
    if (revelationBoardSelect) {
      const campaignId = context.getCampaign().id;
      const fieldConfig = context.getSpecialFieldConfig(campaignId, revelationBoardSelect) || { id: revelationBoardSelect };
      context.mutate((state) => controlActions.updateRevelationBoardSlot(state, campaignId, revelationBoardSelect, target.dataset.kind, target.value, fieldConfig, context.normalizeRevelationBoardValue));
    }
    const specialField = target.dataset.specialField;
    if (specialField) {
      const campaignId = context.getCampaign().id;
      const fieldConfig = context.getSpecialFieldConfig(campaignId, specialField);
      context.mutate((state) => controlActions.updateSpecialField(state, campaignId, specialField, target.value, fieldConfig));
    }
    const specialEffectToggle = target.dataset.specialEffectToggle;
    if (specialEffectToggle) {
      context.mutate((state) => controlActions.updateSpecialEffectToggle(state, context.getCampaign().id, specialEffectToggle, target.value, target.checked));
    }
    const specialRankedField = target.dataset.specialRankedField;
    if (specialRankedField) {
      context.mutate((state) => controlActions.updateSpecialRankedField(state, context.getCampaign().id, specialRankedField, target.dataset.effectParent, target.value));
    }

    const effectStackEntryCount = target.dataset.effectStackEntryCount;
    if (effectStackEntryCount) {
      const campaignId = context.getCampaign().id;
      const fieldConfig = context.getSpecialFieldConfig(campaignId, effectStackEntryCount) || { id: effectStackEntryCount };
      context.mutate((state) => controlActions.updateEffectStackEntryCount(state, campaignId, effectStackEntryCount, Number(target.dataset.index), target.value, fieldConfig, context.mergeEffectStackEntries));
    }
    const effectStackEntryState = target.dataset.effectStackEntryState;
    if (effectStackEntryState) {
      const campaignId = context.getCampaign().id;
      const fieldConfig = context.getSpecialFieldConfig(campaignId, effectStackEntryState) || { id: effectStackEntryState };
      const stateId = context.normalizeStackState(fieldConfig, target.value, campaignId);
      context.mutate((state) => controlActions.updateEffectStackEntryState(state, campaignId, effectStackEntryState, Number(target.dataset.index), stateId, fieldConfig, context.mergeEffectStackEntries));
    }
    const revelationRhetoricCount = target.dataset.revelationBoardRhetoricCount;
    if (revelationRhetoricCount) {
      const campaignId = context.getCampaign().id;
      const fieldConfig = context.getSpecialFieldConfig(campaignId, revelationRhetoricCount) || { id: revelationRhetoricCount };
      context.mutate((state) => controlActions.updateRevelationBoardRhetoricCount(state, campaignId, revelationRhetoricCount, Number(target.dataset.index), target.value, fieldConfig, context.normalizeRevelationBoardValue));
    }
    const coinEntryCount = target.dataset.coinEntryCount;
    if (coinEntryCount) {
      context.mutate((state) => controlActions.updateCoinEntryCount(state, context.getCampaign().id, coinEntryCount, Number(target.dataset.index), target.value));
    }
    const coinEntryStatus = target.dataset.coinEntryStatus;
    if (coinEntryStatus) {
      context.mutate((state) => controlActions.updateCoinEntryStatus(state, context.getCampaign().id, coinEntryStatus, Number(target.dataset.index), target.value));
    }
    const coinEntryFace = target.dataset.coinEntryFace;
    if (coinEntryFace) {
      context.mutate((state) => controlActions.updateCoinEntryFace(state, context.getCampaign().id, coinEntryFace, Number(target.dataset.index), target.value));
    }
  });
}
