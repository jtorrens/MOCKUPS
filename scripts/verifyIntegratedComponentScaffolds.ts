import { readdirSync, readFileSync } from "node:fs";
import path from "node:path";

import {
  parseComponentScaffoldSpec,
} from "../src/development-scaffolding/componentScaffold.js";
import {
  verifyComponentScaffoldImplementation,
} from "../src/development-scaffolding/componentScaffoldWorkspace.js";
import {
  expectedIntegratedComponentScaffoldArtifacts,
  integratedComponentSpecRoot,
} from "../src/development-scaffolding/componentScaffoldArtifacts.js";

const repositoryRoot = process.cwd();
const specRoot = path.join(repositoryRoot, integratedComponentSpecRoot);
const databasePath = path.join(repositoryRoot, "data", "desktop-editor-spike.sqlite");
const results = readdirSync(specRoot, { withFileTypes: true })
  .filter((entry) => entry.isFile() && entry.name.endsWith(".json"))
  .sort((left, right) => left.name.localeCompare(right.name))
  .map((entry) => {
    const spec = parseComponentScaffoldSpec(
      JSON.parse(readFileSync(path.join(specRoot, entry.name), "utf8")) as unknown,
    );
    return verifyComponentScaffoldImplementation(
      spec,
      repositoryRoot,
      databasePath,
    );
  });

if (results.length === 0) {
  throw new Error("At least one integrated Component scaffold spec is required.");
}
const specs = readdirSync(specRoot, { withFileTypes: true })
  .filter((entry) => entry.isFile() && entry.name.endsWith(".json"))
  .sort((left, right) => left.name.localeCompare(right.name))
  .map((entry) => parseComponentScaffoldSpec(
    JSON.parse(readFileSync(path.join(specRoot, entry.name), "utf8")) as unknown,
  ));
for (const [relativePath, expected] of expectedIntegratedComponentScaffoldArtifacts(specs)) {
  const actual = readFileSync(path.join(repositoryRoot, relativePath), "utf8");
  if (actual !== expected) {
    throw new Error(
      `Generated Component scaffold artifact '${relativePath}' is stale. `
      + "Run npm run scaffold:generate.",
    );
  }
}
console.log(`Integrated Component scaffold contracts verified: ${results.length}.`);
