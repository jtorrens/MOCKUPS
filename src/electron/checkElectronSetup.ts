import { existsSync } from "node:fs";
import { readFile } from "node:fs/promises";

function assert(condition: unknown, message: string): asserts condition {
  if (!condition) {
    throw new Error(message);
  }
}

assert(existsSync("src/electron/main.cjs"), "Electron main process is missing");
assert(existsSync("src/electron/preload.cjs"), "Electron preload is missing");

const mainSource = await readFile("src/electron/main.cjs", "utf8");
const preloadSource = await readFile("src/electron/preload.cjs", "utf8");

assert(
  mainSource.includes("contextIsolation: true"),
  "Electron window must use contextIsolation",
);
assert(
  mainSource.includes("nodeIntegration: false"),
  "Electron renderer must not enable broad Node integration",
);
assert(
  preloadSource.includes("contextBridge.exposeInMainWorld"),
  "Preload must expose a narrow context bridge",
);
assert(
  preloadSource.includes("mockupsNative"),
  "Preload must expose the mockupsNative API boundary",
);

console.log("✓ Electron main/preload files exist");
console.log("✓ Electron renderer uses context isolation and no Node integration");
console.log("✓ Electron preload exposes a narrow mockupsNative boundary");
