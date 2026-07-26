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

export function checkDocumentationContracts({
  root,
  readText,
  addViolation,
  assertContains,
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
  for (const document of canonicalArchitectureDocuments) {
    const relativePath = `docs/architecture/${document}`;
    if (!existsSync(path.join(root, relativePath))) {
      addViolation(relativePath, "canonical architecture document is missing");
      continue;
    }
    assertContains(
      relativePath,
      "Status: normative.",
      "canonical architecture document must be normative",
    );
    assertContains("AGENTS.md", relativePath, `AGENTS must require ${relativePath}`);
    assertContains(
      "docs/architecture/README.md",
      document,
      `the architecture index must include ${document}`,
    );
  }
  for (const archiveRuleOwner of ["AGENTS.md", "docs/README.md"]) {
    assertContains(
      archiveRuleOwner,
      "open, search, read, quote, summarize, cite",
      `${archiveRuleOwner} must prohibit historical archive consultation`,
    );
  }
  for (const activeMarkdownPath of [
    "AGENTS.md",
    "docs/README.md",
    "docs/architecture/README.md",
    ...canonicalArchitectureDocuments.map(
      (document) => `docs/architecture/${document}`,
    ),
  ]) {
    const source = readText(activeMarkdownPath);
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
