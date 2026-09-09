import assert from "node:assert/strict";
import {
  copyFileSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import test from "node:test";

import { checkDocumentationContracts } from "../../scripts/validation/checkDocumentationContracts.js";
import { checkRetiredContracts } from "../../scripts/validation/checkRetiredContracts.js";
import { createArchitectureValidationContext } from "../../scripts/validation/validationContext.js";

const repositoryRoot = path.resolve(".");

function copyRelativeFile(root: string, relativePath: string) {
  const target = path.join(root, relativePath);
  mkdirSync(path.dirname(target), { recursive: true });
  copyFileSync(path.join(repositoryRoot, relativePath), target);
}

function documentationFixture() {
  const root = mkdtempSync(path.join(tmpdir(), "mockups-documentation-contract-"));
  copyRelativeFile(root, "AGENTS.md");
  copyRelativeFile(root, "docs/README.md");
  copyRelativeFile(
    root,
    "src/Mockups.Persistence.Sqlite.Core/CurrentSqliteSchema.cs",
  );
  for (const entry of readdirSync(path.join(repositoryRoot, "docs/architecture"))) {
    if (entry === ".DS_Store") continue;
    copyRelativeFile(root, `docs/architecture/${entry}`);
  }
  return root;
}

function documentationViolations(root: string) {
  const context = createArchitectureValidationContext(root);
  checkDocumentationContracts(context);
  return context.violations;
}

test("documentation validation binds the normative schema version to executable schema", () => {
  const root = documentationFixture();
  try {
    assert.deepEqual(documentationViolations(root), []);
    const documentPath = path.join(root, "docs/architecture/data_persistence.md");
    const document = readFileSync(documentPath, "utf8");
    writeFileSync(
      documentPath,
      document.replace("Schema version `16`", "Schema version `15`"),
      "utf8",
    );
    assert.equal(
      documentationViolations(root).some((violation) =>
        violation.includes("must match the executable SQLite schema version")),
      true,
    );
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("documentation validation rejects unindexed active files without entering the archive", () => {
  const root = documentationFixture();
  try {
    writeFileSync(path.join(root, "docs/unindexed-handoff.md"), "stale\n", "utf8");
    assert.equal(
      documentationViolations(root).some((violation) =>
        violation.includes("docs/unindexed-handoff.md")),
      true,
    );
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("retired validation rejects completed maintenance scripts", () => {
  const root = mkdtempSync(path.join(tmpdir(), "mockups-retired-contract-"));
  try {
    writeFileSync(path.join(root, "package.json"), "{\"scripts\":{}}\n", "utf8");
    const retiredScript = "scripts/migratePaletteColorReferencesToIds.mjs";
    mkdirSync(path.dirname(path.join(root, retiredScript)), { recursive: true });
    writeFileSync(path.join(root, retiredScript), "// retired\n", "utf8");
    const context = createArchitectureValidationContext(root);
    checkRetiredContracts(context);
    assert.equal(
      context.violations.some((violation) => violation.includes(retiredScript)),
      true,
    );
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("retired validation rejects every parallel SQLite database under data", () => {
  const root = mkdtempSync(path.join(tmpdir(), "mockups-retired-database-contract-"));
  try {
    writeFileSync(path.join(root, "package.json"), "{\"scripts\":{}}\n", "utf8");
    mkdirSync(path.join(root, "data", "nested"), { recursive: true });
    writeFileSync(path.join(root, "data", "mockups.sqlite"), "current", "utf8");
    writeFileSync(
      path.join(root, "data", "nested", "historical.sqlite"),
      "historical",
      "utf8",
    );
    const context = createArchitectureValidationContext(root);
    checkRetiredContracts(context);
    assert.equal(
      context.violations.some((violation) =>
        violation.includes("data/nested/historical.sqlite")
        && violation.includes("only repository SQLite database")),
      true,
    );
    assert.equal(
      context.violations.some((violation) =>
        violation.startsWith("data/mockups.sqlite:")),
      false,
    );
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("retired validation rejects historical Restore and shell session persistence", () => {
  const root = mkdtempSync(path.join(tmpdir(), "mockups-retired-state-contract-"));
  try {
    writeFileSync(path.join(root, "package.json"), "{\"scripts\":{}}\n", "utf8");
    const restore = "src/Mockups.Desktop.Host/BackupHubRestoreService.cs";
    const shell = "src/Mockups.Desktop/EditorShell/EditorShellStateService.cs";
    mkdirSync(path.dirname(path.join(root, restore)), { recursive: true });
    mkdirSync(path.dirname(path.join(root, shell)), { recursive: true });
    writeFileSync(
      path.join(root, restore),
      "const string Retired = \"restore-retired-v1-processing\";\n",
      "utf8",
    );
    writeFileSync(
      path.join(root, shell),
      "public object SessionHistory { get; init; }\n",
      "utf8",
    );
    const context = createArchitectureValidationContext(root);
    checkRetiredContracts(context);
    assert.equal(
      context.violations.some((violation) =>
        violation.includes("restore-retired-v1-processing")),
      true,
    );
    assert.equal(
      context.violations.some((violation) => violation.includes("SessionHistory")),
      true,
    );
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
