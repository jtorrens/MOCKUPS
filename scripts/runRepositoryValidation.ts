import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import {
  mkdtempSync,
  rmSync,
  symlinkSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { pathToFileURL } from "node:url";

export const parityPath = "data/desktop-editor-spike.sqlite";

export type StagedRepositoryValidationOptions = {
  repositoryRoot?: string;
  temporaryParent?: string;
  readStagedParity?: () => Buffer;
  runRepositoryGate?: (environment: NodeJS.ProcessEnv) => number;
};

export function runStagedRepositoryValidation(
  options: StagedRepositoryValidationOptions = {},
): number {
  const repositoryRoot = options.repositoryRoot ?? process.cwd();
  const temporaryDirectory = mkdtempSync(
    path.join(options.temporaryParent ?? tmpdir(), "mockups-validation-"),
  );
  const validationDatabase = path.join(
    temporaryDirectory,
    "desktop-editor-spike.sqlite",
  );
  const repositoryAssets = path.join(repositoryRoot, "assets");
  const validationAssets = path.join(temporaryDirectory, "assets");

  try {
    symlinkSync(
      repositoryAssets,
      validationAssets,
      process.platform === "win32" ? "junction" : "dir",
    );
    const stagedParity = options.readStagedParity?.()
      ?? readStagedFile(repositoryRoot);
    if (stagedParity.length === 0) {
      throw new Error("The staged parity database is empty.");
    }
    writeFileSync(validationDatabase, stagedParity);
    const digest = createHash("sha256")
      .update(stagedParity)
      .digest("hex");
    process.stdout.write(
      `Validating staged parity database ${digest.slice(0, 12)} without replacing the local workspace database.\n`,
    );

    const environment = {
      ...process.env,
      MOCKUPS_VALIDATION_DATABASE: validationDatabase,
    };
    return options.runRepositoryGate?.(environment)
      ?? runRepositoryGate(repositoryRoot, environment);
  } finally {
    rmSync(temporaryDirectory, { recursive: true, force: true });
  }
}

function readStagedFile(repositoryRoot: string): Buffer {
  const stagedParity = spawnSync(
    "git",
    ["show", `:${parityPath}`],
    {
      cwd: repositoryRoot,
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
  return stagedParity.stdout;
}

function runRepositoryGate(
  repositoryRoot: string,
  environment: NodeJS.ProcessEnv,
): number {
  const result = spawnSync(
    process.platform === "win32" ? "npm.cmd" : "npm",
    ["run", "test:repository"],
    {
      cwd: repositoryRoot,
      env: environment,
      stdio: "inherit",
    },
  );
  if (result.error) throw result.error;
  return result.status ?? 1;
}

const executedPath = process.argv[1]
  ? pathToFileURL(path.resolve(process.argv[1])).href
  : "";
if (executedPath === import.meta.url) {
  process.exitCode = runStagedRepositoryValidation();
}
