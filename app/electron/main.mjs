import path from "node:path";
import { app, BrowserWindow, Menu, shell } from "electron";
import {
  appUrl,
  DEFAULT_PORT,
  hasFlag,
  normalizePort,
  normalizeView,
  overlayUrl,
  readArg,
  waitForReady,
} from "../runtime/local-server.mjs";

const port = normalizePort(readArg(process.argv, "--port", process.env.PORT || DEFAULT_PORT));
const initialView = normalizeView(readArg(process.argv, "--view", "control"));
const smokeTest = hasFlag(process.argv, "--smoke-test");

let serverController = null;
let mainWindow = null;
let isQuitting = false;

function loadView(view) {
  if (!mainWindow) return;
  mainWindow.loadURL(appUrl(port, view));
}

function buildMenu() {
  return Menu.buildFromTemplate([
    {
      label: "表示",
      submenu: [
        { label: "Control", accelerator: "CmdOrCtrl+1", click: () => loadView("control") },
        { label: "Overlay Preview", accelerator: "CmdOrCtrl+2", click: () => loadView("overlay") },
        { type: "separator" },
        { label: "OBS Compact URLを開く", click: () => shell.openExternal(overlayUrl(port, "?layout=compact")) },
        { label: "OBS 横長URLを開く", click: () => shell.openExternal(overlayUrl(port, "?layout=horizontal&size=medium")) },
        { label: "OBS 縦長URLを開く", click: () => shell.openExternal(overlayUrl(port, "?layout=vertical&size=medium")) },
      ],
    },
    {
      label: "操作",
      submenu: [
        { role: "reload", label: "再読み込み" },
        { role: "toggleDevTools", label: "開発者ツール" },
        { type: "separator" },
        { role: "quit", label: "終了" },
      ],
    },
  ]);
}

function createWindow(targetUrl) {
  mainWindow = new BrowserWindow({
    width: 1360,
    height: 920,
    minWidth: 980,
    minHeight: 680,
    title: "Arknights Rogue OBS Tool",
    backgroundColor: "#10100f",
    autoHideMenuBar: false,
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  });

  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    shell.openExternal(url);
    return { action: "deny" };
  });
  mainWindow.on("closed", () => {
    mainWindow = null;
  });
  Menu.setApplicationMenu(buildMenu());
  mainWindow.loadURL(targetUrl);
}

async function startDesktopApp() {
  process.env.ARKNIGHTS_STATE_DIR = process.env.ARKNIGHTS_STATE_DIR || path.join(app.getPath("userData"), "state");
  const { startServer } = await import("../server.mjs");
  serverController = await startServer({ port });
  const targetUrl = appUrl(serverController.port, initialView);
  await waitForReady(targetUrl);
  console.log(`Desktop: ${targetUrl}`);
  if (smokeTest) {
    app.quit();
    return;
  }
  createWindow(targetUrl);
}

app.whenReady().then(startDesktopApp).catch((error) => {
  console.error(error instanceof Error ? error.message : String(error));
  app.quit();
});

app.on("activate", () => {
  if (BrowserWindow.getAllWindows().length === 0) createWindow(appUrl(port, initialView));
});

app.on("before-quit", () => {
  isQuitting = true;
  serverController?.server?.close();
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") app.quit();
});