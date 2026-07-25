import { mkdtempSync, rmSync, symlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";

const parityPath = "data/desktop-editor-spike.sqlite";
const temporaryDirectory = mkdtempSync(
  path.join(tmpdir(), "mockups-validation-"),
);
const validationDatabase = path.join(
  temporaryDirectory,
  "desktop-editor-spike.sqlite",
);
const repositoryAssets = path.join(process.cwd(), "assets");
const validationAssets = path.join(temporaryDirectory, "assets");

try {
  symlinkSync(
    repositoryAssets,
    validationAssets,
    process.platform === "win32" ? "junction" : "dir",
  );
  const stagedParity = spawnSync(
    "git",
    ["show", `:${parityPath}`],
    {
      cwd: process.cwd(),
      encoding: "buffer",
      maxBuffer: 32 * 1024 * 1024,
    },
  );
  if (stagedParity.status !== 0 || !stagedParity.stdout?.length) {
    const detail = stagedParity.stderr?.toString("utf8").trim();
    throw new Error(
      `Unable to read staged parity database${detail ? `: ${detail}` : ""}`,
    );
  }
  writeFileSync(validationDatabase, stagedParity.stdout);
  const digest = createHash("sha256")
    .update(stagedParity.stdout)
    .digest("hex");
  process.stdout.write(
    `Validating staged parity database ${digest.slice(0, 12)} without replacing the local workspace database.\n`,
  );

  const result = spawnSync(
    "npm",
    ["run", "test:repository"],
    {
      cwd: process.cwd(),
      env: {
        ...process.env,
        MOCKUPS_VALIDATION_DATABASE: validationDatabase,
      },
      stdio: "inherit",
    },
  );
  if (result.error) throw result.error;
  process.exitCode = result.status ?? 1;
} finally {
  rmSync(temporaryDirectory, { recursive: true, force: true });
}
