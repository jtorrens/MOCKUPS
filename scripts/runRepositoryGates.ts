import { spawnSync } from "node:child_process";
import path from "node:path";
import { performance } from "node:perf_hooks";
import { pathToFileURL } from "node:url";

export const repositoryValidationGates = [
  "typecheck",
  "desktop:compile",
  "check:unused:desktop",
  "test:unit",
  "test:scaffolding",
  "scaffold:verify",
  "scaffold:module:verify",
  "test:tooling",
  "animation:test",
  "check:architecture",
] as const;

export type RepositoryValidationGate =
  (typeof repositoryValidationGates)[number];

export type RepositoryGateRunner = (
  gate: RepositoryValidationGate,
) => number;

export function runRepositoryGates(
  runGate: RepositoryGateRunner = runNpmGate,
): number {
  const timings: Array<{ gate: RepositoryValidationGate; durationMs: number }> = [];
  for (const gate of repositoryValidationGates) {
    const startedAt = performance.now();
    const status = runGate(gate);
    const durationMs = performance.now() - startedAt;
    timings.push({ gate, durationMs });
    process.stdout.write(
      `Repository gate '${gate}' ${status === 0 ? "passed" : "failed"} `
      + `in ${formatDuration(durationMs)}.\n`,
    );
    if (status !== 0) {
      printTimingSummary(timings);
      return status;
    }
  }
  printTimingSummary(timings);
  return 0;
}

function formatDuration(durationMs: number): string {
  if (durationMs < 1_000) return `${Math.round(durationMs)} ms`;
  const seconds = durationMs / 1_000;
  if (seconds < 60) return `${seconds.toFixed(1)} s`;
  return `${Math.floor(seconds / 60)}m ${(seconds % 60).toFixed(1)}s`;
}

function printTimingSummary(
  timings: ReadonlyArray<{
    gate: RepositoryValidationGate;
    durationMs: number;
  }>,
): void {
  const totalMs = timings.reduce((total, timing) => total + timing.durationMs, 0);
  process.stdout.write("\nRepository gate timings:\n");
  for (const timing of [...timings].sort((left, right) =>
    right.durationMs - left.durationMs)) {
    process.stdout.write(
      `  ${timing.gate}: ${formatDuration(timing.durationMs)}\n`,
    );
  }
  process.stdout.write(`  total: ${formatDuration(totalMs)}\n`);
}

function runNpmGate(gate: RepositoryValidationGate): number {
  const result = spawnSync(
    process.platform === "win32" ? "npm.cmd" : "npm",
    ["run", gate],
    {
      cwd: process.cwd(),
      env: process.env,
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
  process.exitCode = runRepositoryGates();
}
