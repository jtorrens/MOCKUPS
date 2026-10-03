import { spawn } from "node:child_process";
import { cpus } from "node:os";
import path from "node:path";
import { performance } from "node:perf_hooks";
import { fileURLToPath } from "node:url";

import Database from "better-sqlite3";
import manifest from "../src/desktop-preview/desktopPreviewManifest.json" with { type: "json" };

type Shard = {
  id: number;
  owners: string[];
  weight: number;
};

type ShardResult = {
  shard: Shard;
  durationMs: number;
  exitCode: number;
};

const project = "tests/Mockups.Desktop.Tests/Mockups.DesktopEditorShell.AnimationTests.csproj";
const owners = [
  ...Object.keys(manifest.components).map((owner) => `component:${owner}`),
  ...Object.keys(manifest.modules).map((owner) => `module:${owner}`),
].sort();
const requestedWorkers = Number.parseInt(
  process.env.MOCKUPS_DESKTOP_EXHAUSTIVE_WORKERS ?? "",
  10,
);
const workerCount = Math.max(
  1,
  Math.min(
    owners.length,
    Number.isFinite(requestedWorkers) && requestedWorkers > 0
      ? requestedWorkers
      : Math.min(4, cpus().length),
  ),
);
const databasePath = process.env.MOCKUPS_VALIDATION_DATABASE
  ?? path.join(process.cwd(), "data", "mockups.sqlite");

function createShards(): Shard[] {
  const database = new Database(databasePath, {
    readonly: true,
    fileMustExist: true,
  });
  try {
    const weights = new Map<string, number>(
      database.prepare(`
        SELECT
          'component:' || component_type AS owner,
          SUM(json_array_length(json_extract(metadata_json, '$.variants'))) * 2 AS weight
        FROM component_classes
        GROUP BY component_type
        UNION ALL
        SELECT
          'module:' || record_class_id AS owner,
          SUM(json_array_length(json_extract(metadata_json, '$.variants'))) * 2 AS weight
        FROM modules
        GROUP BY record_class_id
      `).all().map((row) => {
        const value = row as { owner: string; weight: number };
        return [value.owner, value.weight];
      }),
    );
    const shards = Array.from(
      { length: workerCount },
      (_, id): Shard => ({ id: id + 1, owners: [], weight: 0 }),
    );
    for (const owner of [...owners].sort((left, right) =>
      (weights.get(right) ?? 2) - (weights.get(left) ?? 2))) {
      const shard = shards.reduce((lightest, candidate) =>
        candidate.weight < lightest.weight ? candidate : lightest);
      const weight = weights.get(owner) ?? 2;
      shard.owners.push(owner);
      shard.weight += weight;
    }
    return shards;
  } finally {
    database.close();
  }
}

async function runShard(shard: Shard): Promise<ShardResult> {
  const startedAt = performance.now();
  const exitCode = await new Promise<number>((resolve, reject) => {
    const ownerArguments = shard.owners.flatMap((owner) => ["--owner", owner]);
    const child = spawn(
      "dotnet",
      [
        "run",
        "--project",
        project,
        "--no-build",
        "--no-restore",
        "--",
        "--group",
        "exhaustive",
        ...ownerArguments,
      ],
      {
        cwd: process.cwd(),
        env: process.env,
        stdio: "inherit",
      },
    );
    child.once("error", reject);
    child.once("exit", (code, signal) => {
      if (signal) {
        reject(new Error(
          `Desktop exhaustive shard ${shard.id} ended with signal ${signal}.`,
        ));
        return;
      }
      resolve(code ?? 1);
    });
  });
  return {
    shard,
    durationMs: performance.now() - startedAt,
    exitCode,
  };
}

async function run(): Promise<void> {
  const shards = createShards();
  process.stdout.write(
    `Running ${owners.length} exhaustive Desktop owners in ${shards.length} weighted shards.\n`,
  );
  const startedAt = performance.now();
  const results = await Promise.all(shards.map(runShard));
  for (const result of results) {
    process.stdout.write(
      `${result.exitCode === 0 ? "PASS" : "FAIL"} SHARD ${result.shard.id} `
      + `(${result.shard.owners.length} owners, weight ${result.shard.weight}, `
      + `${(result.durationMs / 1_000).toFixed(1)} s)\n`,
    );
  }
  const failures = results.filter((result) => result.exitCode !== 0);
  const passedOwnerCount = results
    .filter((result) => result.exitCode === 0)
    .reduce((total, result) => total + result.shard.owners.length, 0);
  const durationMs = performance.now() - startedAt;
  process.stdout.write(
    `Exhaustive Desktop owners: ${passedOwnerCount}/${owners.length} passed `
    + `in ${(durationMs / 1_000).toFixed(1)} s.\n`,
  );
  if (failures.length > 0) process.exitCode = 1;
}

const executedPath = process.argv[1] ? path.resolve(process.argv[1]) : "";
if (executedPath === fileURLToPath(import.meta.url)) {
  await run();
}
