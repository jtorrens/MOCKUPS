import { spawn } from "node:child_process";
import { access } from "node:fs/promises";
import { setTimeout as sleep } from "node:timers/promises";

const appUrl = "http://127.0.0.1:4173";
const healthUrl = "http://127.0.0.1:4174/api/health";

async function waitForUrl(url: string) {
  const startedAt = Date.now();
  while (Date.now() - startedAt < 30_000) {
    try {
      const response = await fetch(url, { cache: "no-store" });
      if (response.ok) return;
    } catch {
      // Server not ready yet.
    }
    await sleep(250);
  }
  throw new Error(`Timed out waiting for ${url}`);
}

await waitForUrl(appUrl);
await waitForUrl(healthUrl);

const electronBinary =
  process.platform === "win32"
    ? "node_modules/.bin/electron.cmd"
    : "node_modules/.bin/electron";

await access(electronBinary);

const electronEnv = { ...process.env };
delete electronEnv.ELECTRON_RUN_AS_NODE;

const child = spawn(electronBinary, ["src/electron/main.cjs"], {
  stdio: "inherit",
  env: {
    ...electronEnv,
    MOCKUPS_ELECTRON_URL: appUrl,
  },
});

child.on("exit", (code) => {
  process.exit(code ?? 0);
});
