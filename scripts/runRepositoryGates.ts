import { spawnSync } from "node:child_process";
import path from "node:path";
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
  for (const gate of repositoryValidationGates) {
    const status = runGate(gate);
    if (status !== 0) return status;
  }
  return 0;
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
