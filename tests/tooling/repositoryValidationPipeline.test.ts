import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import {
  existsSync,
  lstatSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
} from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";

import {
  repositoryValidationGates,
  runRepositoryGates,
} from "../../scripts/runRepositoryGates.js";
import {
  runStagedRepositoryValidation,
} from "../../scripts/runRepositoryValidation.js";

test("repository gates execute in declared order and stop at the first failure", () => {
  assert.deepEqual(repositoryValidationGates, [
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
  ]);
  const completed: string[] = [];
  const success = runRepositoryGates((gate) => {
    completed.push(gate);
    return 0;
  });
  assert.equal(success, 0);
  assert.deepEqual(completed, repositoryValidationGates);

  const failed: string[] = [];
  const failureGate = "test:unit";
  const failure = runRepositoryGates((gate) => {
    failed.push(gate);
    return gate === failureGate ? 37 : 0;
  });
  assert.equal(failure, 37);
  assert.deepEqual(
    failed,
    repositoryValidationGates.slice(
      0,
      repositoryValidationGates.indexOf(failureGate) + 1,
    ),
  );
});

test("desktop owner selection is executable and rejects unknown manifest ids", () => {
  const project =
    "tests/Mockups.Desktop.Tests/Mockups.DesktopEditorShell.AnimationTests.csproj";
  const selected = spawnSync(
    "dotnet",
    [
      "run",
      "--project",
      project,
      "--",
      "--group",
      "exhaustive",
      "--owner",
      "component:label",
      "--list",
    ],
    {
      cwd: process.cwd(),
      encoding: "utf8",
    },
  );
  assert.equal(selected.status, 0, selected.stderr);
  assert.match(
    selected.stdout,
    /manifest owners render their committed fixtures/u,
  );

  const rejected = spawnSync(
    "dotnet",
    [
      "run",
      "--project",
      project,
      "--",
      "--group",
      "exhaustive",
      "--owner",
      "component:not-a-real-owner",
      "--list",
    ],
    {
      cwd: process.cwd(),
      encoding: "utf8",
    },
  );
  assert.notEqual(rejected.status, 0);
  assert.match(
    `${rejected.stdout}\n${rejected.stderr}`,
    /Unknown Preview owner selector/u,
  );
});

test("Application selection is exact and rejects unknown test names", () => {
  const project =
    "tests/Mockups.Application.Tests/Mockups.Application.Tests.csproj";
  const selected = spawnSync(
    "dotnet",
    [
      "run",
      "--project",
      project,
      "--",
      "--exact",
      "SVG fill transforms keep direct reusable geometry",
      "--list",
    ],
    {
      cwd: process.cwd(),
      encoding: "utf8",
    },
  );
  assert.equal(selected.status, 0, selected.stderr);
  assert.match(
    selected.stdout,
    /SVG fill transforms keep direct reusable geometry/u,
  );
  assert.doesNotMatch(
    selected.stdout,
    /initial tree load resolves/u,
  );

  const rejected = spawnSync(
    "dotnet",
    [
      "run",
      "--project",
      project,
      "--",
      "--exact",
      "not a real Application test",
      "--list",
    ],
    {
      cwd: process.cwd(),
      encoding: "utf8",
    },
  );
  assert.notEqual(rejected.status, 0);
  assert.match(
    `${rejected.stdout}\n${rejected.stderr}`,
    /Unknown exact Application test/u,
  );
});

test("repository validation uses staged parity in a disposable isolated root", () => {
  const fixtureRoot = mkdtempSync(
    path.join(os.tmpdir(), "mockups-validation-owner-test-"),
  );
  const repositoryRoot = path.join(fixtureRoot, "repository");
  const temporaryParent = path.join(fixtureRoot, "temporary");
  mkdirSync(path.join(repositoryRoot, "assets"), { recursive: true });
  mkdirSync(temporaryParent);
  const stagedParity = Buffer.from("staged-parity-fixture");
  let validationDatabase = "";

  try {
    const status = runStagedRepositoryValidation({
      repositoryRoot,
      temporaryParent,
      readStagedParity: () => stagedParity,
      runRepositoryGate: (environment) => {
        validationDatabase =
          environment.MOCKUPS_VALIDATION_DATABASE ?? "";
        assert.deepEqual(
          readFileSync(validationDatabase),
          stagedParity,
        );
        const isolatedRoot = path.dirname(validationDatabase);
        assert.equal(
          lstatSync(path.join(isolatedRoot, "assets")).isSymbolicLink(),
          process.platform !== "win32",
        );
        return 23;
      },
    });

    assert.equal(status, 23);
    assert.notEqual(validationDatabase, "");
    assert.equal(existsSync(validationDatabase), false);
    assert.equal(existsSync(path.dirname(validationDatabase)), false);
  } finally {
    rmSync(fixtureRoot, { recursive: true, force: true });
  }
});

test("repository validation cleans its isolated root after gate exceptions", () => {
  const fixtureRoot = mkdtempSync(
    path.join(os.tmpdir(), "mockups-validation-cleanup-test-"),
  );
  const repositoryRoot = path.join(fixtureRoot, "repository");
  const temporaryParent = path.join(fixtureRoot, "temporary");
  mkdirSync(path.join(repositoryRoot, "assets"), { recursive: true });
  mkdirSync(temporaryParent);
  let validationDatabase = "";

  try {
    assert.throws(
      () => runStagedRepositoryValidation({
        repositoryRoot,
        temporaryParent,
        readStagedParity: () => Buffer.from("staged"),
        runRepositoryGate: (environment) => {
          validationDatabase =
            environment.MOCKUPS_VALIDATION_DATABASE ?? "";
          throw new Error("gate failed");
        },
      }),
      /gate failed/u,
    );
    assert.notEqual(validationDatabase, "");
    assert.equal(existsSync(path.dirname(validationDatabase)), false);
  } finally {
    rmSync(fixtureRoot, { recursive: true, force: true });
  }
});
