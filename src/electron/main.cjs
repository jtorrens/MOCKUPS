const { app, BrowserWindow, dialog, ipcMain, session } = require("electron");
const { execFile } = require("node:child_process");
const fs = require("node:fs");
const path = require("node:path");

const appUrl = process.env.MOCKUPS_ELECTRON_URL || "http://127.0.0.1:4173";

function windowStatePath() {
  return path.join(app.getPath("userData"), "window-state.json");
}

function readWindowState() {
  try {
    const parsed = JSON.parse(fs.readFileSync(windowStatePath(), "utf8"));
    if (!parsed || typeof parsed !== "object") return {};
    return parsed;
  } catch {
    return {};
  }
}

function writeWindowState(window) {
  if (window.isDestroyed()) return;
  try {
    const bounds = window.getBounds();
    fs.writeFileSync(
      windowStatePath(),
      JSON.stringify(
        {
          ...bounds,
          maximized: window.isMaximized(),
        },
        null,
        2,
      ),
    );
  } catch {
    // Layout persistence is a convenience; never block shutdown/startup for it.
  }
}

function createWindow() {
  const state = readWindowState();
  const window = new BrowserWindow({
    x: typeof state.x === "number" ? state.x : undefined,
    y: typeof state.y === "number" ? state.y : undefined,
    width: typeof state.width === "number" ? state.width : 1440,
    height: typeof state.height === "number" ? state.height : 1000,
    minWidth: 1180,
    minHeight: 760,
    title: "MOCKUPS",
    backgroundColor: "#0d0f14",
    webPreferences: {
      preload: path.join(__dirname, "preload.cjs"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
    },
  });

  if (state.maximized) {
    window.maximize();
  }

  window.on("close", () => writeWindowState(window));

  void window.loadURL(appUrl);
}

ipcMain.handle("mockups:pickFile", async () => {
  const result = await dialog.showOpenDialog({
    properties: ["openFile"],
  });
  return result.canceled ? [] : result.filePaths;
});

ipcMain.handle("mockups:listFonts", async () => {
  if (process.platform === "darwin") {
    return new Promise((resolve) => {
      execFile(
        "system_profiler",
        ["SPFontsDataType", "-json"],
        { timeout: 15000, maxBuffer: 24 * 1024 * 1024 },
        (error, stdout) => {
          if (error) {
            resolve([]);
            return;
          }
          try {
            const parsed = JSON.parse(stdout);
            const fonts = parsed.SPFontsDataType ?? [];
            const familyNames = new Set();
            const localFonts = [];
            for (const font of fonts) {
              const family =
                font.family ?? font._name ?? font.name ?? font.full_name;
              if (!family || familyNames.has(family)) continue;
              familyNames.add(family);
              localFonts.push({
                family,
                style:
                  font.style ??
                  font.typeface ??
                  font.face ??
                  font.font_face ??
                  font.font_style,
                fullName: font.full_name ?? font._name,
                postscriptName: font.postscript_name,
              });
            }
            resolve(localFonts);
          } catch {
            resolve([]);
          }
        },
      );
    });
  }
  return [];
});

app.whenReady().then(() => {
  session.defaultSession.setPermissionRequestHandler((_webContents, permission, callback) => {
    callback(permission === "local-fonts");
  });
  createWindow();

  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});
