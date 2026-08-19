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
      document.replace("Schema version `12`", "Schema version `11`"),
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
