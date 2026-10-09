import { existsSync, readdirSync } from "node:fs";
import path from "node:path";

import type { ArchitectureValidationContext } from "./validationContext.js";

const canonicalArchitectureDocuments = [
  "system_overview.md",
  "data_persistence.md",
  "design_system.md",
  "production.md",
  "editor_dictionary.md",
  "composition_runtime.md",
  "animation.md",
  "preview_rendering.md",
  "resources_assets.md",
  "ux_ui.md",
  "development_workflow.md",
  "validation.md",
] as const;

const activeDocumentationFiles = new Set<string>([
  "docs/README.md",
  "docs/architecture/README.md",
  ...canonicalArchitectureDocuments.map(
    (document) => `docs/architecture/${document}`,
  ),
]);

function collectActiveDocumentationFiles(
  root: string,
  relativeDirectory = "docs",
): string[] {
  const files: string[] = [];
  for (const entry of readdirSync(path.join(root, relativeDirectory), {
    withFileTypes: true,
  })) {
    if (entry.name === ".DS_Store") continue;
    const relativePath = `${relativeDirectory}/${entry.name}`;
    if (relativePath === "docs/old") continue;
    if (entry.isDirectory()) {
      files.push(...collectActiveDocumentationFiles(root, relativePath));
    } else {
      files.push(relativePath);
    }
  }
  return files;
}

export function checkDocumentationContracts({
  root,
  readText,
  addViolation,
  assertDocumentContains,
}: ArchitectureValidationContext) {
  const canonicalArchitectureEntries = new Set<string>([
    "README.md",
    ...canonicalArchitectureDocuments,
  ]);
  const architectureDirectory = path.join(root, "docs", "architecture");
  for (const entry of readdirSync(architectureDirectory)) {
    if (entry === ".DS_Store") continue;
    if (!canonicalArchitectureEntries.has(entry)) {
      addViolation(
        `docs/architecture/${entry}`,
        "active architecture contains a file or directory outside the canonical set",
      );
    }
  }
  for (const relativePath of collectActiveDocumentationFiles(root)) {
    if (!activeDocumentationFiles.has(relativePath)) {
      addViolation(
        relativePath,
        "active documentation contains a file outside the canonical index",
      );
    }
  }
  for (const document of canonicalArchitectureDocuments) {
    const relativePath = `docs/architecture/${document}`;
    if (!existsSync(path.join(root, relativePath))) {
      addViolation(relativePath, "canonical architecture document is missing");
      continue;
    }
    assertDocumentContains(
      relativePath,
      "Status: normative.",
      "canonical architecture document must be normative",
    );
    assertDocumentContains("AGENTS.md", relativePath, `AGENTS must require ${relativePath}`);
    assertDocumentContains(
      "docs/architecture/README.md",
      document,
      `the architecture index must include ${document}`,
    );
  }
  for (const archiveRuleOwner of ["AGENTS.md", "docs/README.md"]) {
    assertDocumentContains(
      archiveRuleOwner,
      "open, search, read, quote, summarize, cite",
      `${archiveRuleOwner} must prohibit historical archive consultation`,
    );
  }
  const schemaSource = readText(
    "src/Mockups.Persistence.Sqlite.Core/CurrentSqliteSchema.cs",
  );
  const schemaVersionMatches = [
    ...schemaSource.matchAll(/\bPRAGMA\s+user_version\s*=\s*(\d+)\s*;/gu),
  ];
  if (schemaVersionMatches.length !== 1) {
    addViolation(
      "src/Mockups.Persistence.Sqlite.Core/CurrentSqliteSchema.cs",
      "current SQLite schema must declare exactly one numeric user_version",
    );
  } else {
    const schemaVersion = schemaVersionMatches[0]?.[1] ?? "";
    assertDocumentContains(
      "docs/architecture/data_persistence.md",
      `Schema version \`${schemaVersion}\` is the only current schema.`,
      "normative persistence documentation must match the executable SQLite schema version",
    );
  }
  for (const requiredTerm of [
    "materialization never edits the manifest, registry or database",
    "Integration rejects missing assets",
    "Module contract planning",
  ]) {
    assertDocumentContains(
      "docs/architecture/development_workflow.md",
      requiredTerm,
      "the normative development workflow must retain its scaffolding boundaries",
    );
  }
  for (const [document, requiredTerm] of [
    ["AGENTS.md", "Do not run test suites during iteration or final handoff unless the user"],
    ["docs/architecture/validation.md", "Tests require explicit user"],
    ["docs/architecture/development_workflow.md", "Local agent iteration is compile-only unless the user explicitly authorizes"],
  ] as const) {
    assertDocumentContains(
      document,
      requiredTerm,
      `${document} must keep validation proportional to revision scope`,
    );
  }
  // Bounded prose regressions, not a substitute for executable behavior guards.
  // These exact retired claims previously coexisted with the opposite contract.
  const retiredClaims = [
    ["snapshot-at-enqueue", "The visible editor creates a complete immutable snapshot"],
    ["Design media substitution", "Design `defaultValue` for an empty or unavailable authored media value"],
    ["Actor-derived Screen Theme", "Screen → Shot → Shot owner Actor → Actor default Theme"],
    ["automatic complete gate", "and requires `npm test` before handoff"],
    ["protected frame-zero keyframe", "protected frame-zero keyframe"],
  ] as const;
  for (const activeMarkdownPath of [
    "AGENTS.md",
    "docs/README.md",
    "docs/architecture/README.md",
    ...canonicalArchitectureDocuments.map(
      (document) => `docs/architecture/${document}`,
    ),
  ]) {
    const source = readText(activeMarkdownPath);
    const normalized = source.replace(/\s+/gu, " ");
    for (const [contract, retiredClaim] of retiredClaims) {
      if (normalized.includes(retiredClaim)) {
        addViolation(activeMarkdownPath, `retired documentation contract: ${contract}`);
      }
    }
    for (const match of source.matchAll(/\]\(([^)]+)\)/g)) {
      const target = match[1] ?? "";
      if (target.includes("docs/old") || target.includes("../old")) {
        addViolation(
          activeMarkdownPath,
          "active documentation must not link to the historical archive",
        );
      }
    }
  }
}
