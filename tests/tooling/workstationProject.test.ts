import assert from "node:assert/strict";
import {
  existsSync,
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
  beginWorkstationUpdate,
  bootstrapWorkstationProject,
  checkDatabaseParityIfPresent,
  checkpointWorkstationUpdate,
  endWorkstationUpdate,
  requireDatabaseParity,
  requireWorkstationUpdate,
  snapshotWorkstationProject,
  workstationProjectPaths,
  workstationProjectRoot,
  workstationUpdateStatus,
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

test("one maintenance cycle snapshots only the canonical workstation database", () => {
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
    assert.equal(existsSync(path.join(workstation, "assets")), false);
    assert.equal(requireDatabaseParity(paths).length, 64);

    writeFileSync(paths.workstationDatabase, "authored");
    writeFileSync(path.join(repository, "assets", "fixture.txt"), "external asset");
    assert.throws(() => requireDatabaseParity(paths), /snapshot differ/u);
    assert.throws(
      () => snapshotWorkstationProject(repository, workstation, () => {}),
      /No MOCKUPS update maintenance is active/u,
    );

    beginWorkstationUpdate(
      repository,
      workstation,
      () => {},
      () => {},
    );
    assert.equal(existsSync(paths.workstationUpdateLock), true);
    assert.equal(requireWorkstationUpdate(paths), paths.workstationUpdateLock);
    assert.equal(readFileSync(paths.repositoryDatabase, "utf8"), "authored");
    assert.throws(
      () => bootstrapWorkstationProject(repository, workstation, () => {}),
      /update maintenance is active/u,
    );

    writeFileSync(paths.workstationDatabase, "authored-final");
    checkpointWorkstationUpdate(repository, workstation, () => {});
    assert.equal(
      readFileSync(paths.repositoryDatabase, "utf8"),
      "authored-final",
    );
    assert.equal(
      readFileSync(path.join(repository, "assets", "fixture.txt"), "utf8"),
      "external asset",
    );
    assert.equal(requireDatabaseParity(paths).length, 64);
    assert.deepEqual(
      workstationUpdateStatus(paths),
      {
        updateActive: true,
        repositoryExists: true,
        workstationExists: true,
        databasesAligned: true,
        paths,
      },
    );

    endWorkstationUpdate(
      repository,
      workstation,
      () => {},
      () => {},
    );
    assert.equal(existsSync(paths.workstationUpdateLock), false);
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
});

test("failed maintenance begin releases its startup block", () => {
  const temporary = mkdtempSync(path.join(tmpdir(), "mockups-update-failure-"));
  const repository = path.join(temporary, "repository");
  const workstation = path.join(temporary, "workstation");
  mkdirSync(path.join(repository, "data"), { recursive: true });
  mkdirSync(workstation, { recursive: true });
  writeFileSync(path.join(repository, "data", "mockups.sqlite"), "repository");
  writeFileSync(path.join(workstation, "mockups.sqlite"), "workstation");
  const paths = workstationProjectPaths(repository, workstation);
  try {
    assert.throws(
      () => beginWorkstationUpdate(
        repository,
        workstation,
        () => { throw new Error("invalid canonical database"); },
        () => {},
      ),
      /invalid canonical database/u,
    );
    assert.equal(existsSync(paths.workstationUpdateLock), false);
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
});

test("maintenance cannot begin from a dirty repository", () => {
  const temporary = mkdtempSync(path.join(tmpdir(), "mockups-dirty-update-"));
  const repository = path.join(temporary, "repository");
  const workstation = path.join(temporary, "workstation");
  mkdirSync(path.join(repository, "data"), { recursive: true });
  mkdirSync(workstation, { recursive: true });
  writeFileSync(path.join(repository, "data", "mockups.sqlite"), "database");
  writeFileSync(path.join(workstation, "mockups.sqlite"), "database");
  const paths = workstationProjectPaths(repository, workstation);
  try {
    assert.throws(
      () => beginWorkstationUpdate(
        repository,
        workstation,
        () => {},
        () => { throw new Error("repository is dirty"); },
      ),
      /repository is dirty/u,
    );
    assert.equal(existsSync(paths.workstationUpdateLock), false);
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
});

test("an active application prevents maintenance from acquiring the database", () => {
  const temporary = mkdtempSync(path.join(tmpdir(), "mockups-active-app-"));
  const repository = path.join(temporary, "repository");
  const workstation = path.join(temporary, "workstation");
  mkdirSync(path.join(repository, "data"), { recursive: true });
  mkdirSync(workstation, { recursive: true });
  writeFileSync(path.join(repository, "data", "mockups.sqlite"), "database");
  writeFileSync(path.join(workstation, "mockups.sqlite"), "database");
  const paths = workstationProjectPaths(repository, workstation);
  writeFileSync(paths.workstationApplicationLock, "application active");
  try {
    assert.throws(
      () => beginWorkstationUpdate(
        repository,
        workstation,
        () => {},
        () => {},
      ),
      /MOCKUPS is open/u,
    );
    assert.equal(existsSync(paths.workstationUpdateLock), false);
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
