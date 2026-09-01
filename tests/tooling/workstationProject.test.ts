import assert from "node:assert/strict";
import {
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import test from "node:test";

import {
  bootstrapWorkstationProject,
  checkDatabaseParityIfPresent,
  requireDatabaseParity,
  snapshotWorkstationProject,
  workstationProjectRoot,
} from "../../scripts/workstationProject.mjs";

test("workstation Project roots follow each operating-system data owner", () => {
  assert.equal(
    workstationProjectRoot("darwin", {}, "/Users/editor"),
    "/Users/editor/Library/Application Support/MOCKUPS",
  );
  assert.equal(
    workstationProjectRoot("win32", { LOCALAPPDATA: "C:\\Local" }, "C:\\Users\\editor"),
    path.join("C:\\Local", "MOCKUPS"),
  );
  assert.equal(
    workstationProjectRoot("linux", { XDG_DATA_HOME: "/data" }, "/home/editor"),
    "/data/MOCKUPS",
  );
});

test("bootstrap and snapshot keep one operational database aligned", () => {
  const temporary = mkdtempSync(path.join(tmpdir(), "mockups-workstation-test-"));
  const repository = path.join(temporary, "repository");
  const workstation = path.join(temporary, "workstation");
  mkdirSync(path.join(repository, "data"), { recursive: true });
  mkdirSync(path.join(repository, "assets"), { recursive: true });
  writeFileSync(path.join(repository, "data", "mockups.sqlite"), "initial");
  writeFileSync(path.join(repository, "assets", "fixture.txt"), "asset");
  try {
    const paths = bootstrapWorkstationProject(repository, workstation, () => {});
    assert.equal(readFileSync(paths.workstationDatabase, "utf8"), "initial");
    assert.equal(
      readFileSync(path.join(paths.workstationAssets, "fixture.txt"), "utf8"),
      "asset",
    );
    assert.equal(requireDatabaseParity(paths).length, 64);

    writeFileSync(paths.workstationDatabase, "authored");
    writeFileSync(path.join(paths.workstationAssets, "fixture.txt"), "authored asset");
    writeFileSync(path.join(paths.repositoryAssets, "fixture.txt"), "stale asset");
    assert.throws(() => requireDatabaseParity(paths), /snapshot differ/u);
    bootstrapWorkstationProject(repository, workstation, () => {});
    assert.equal(
      readFileSync(path.join(paths.workstationAssets, "fixture.txt"), "utf8"),
      "authored asset",
    );
    snapshotWorkstationProject(repository, workstation, () => {});
    assert.equal(readFileSync(paths.repositoryDatabase, "utf8"), "authored");
    assert.equal(
      readFileSync(path.join(paths.repositoryAssets, "fixture.txt"), "utf8"),
      "authored asset",
    );
    assert.equal(requireDatabaseParity(paths).length, 64);
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
});

test("parity is not required before a workstation Project exists", () => {
  const temporary = mkdtempSync(path.join(tmpdir(), "mockups-workstation-empty-"));
  try {
    assert.equal(
      checkDatabaseParityIfPresent(
        path.join(temporary, "repository"),
        path.join(temporary, "workstation"),
      ),
      undefined,
    );
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
});
