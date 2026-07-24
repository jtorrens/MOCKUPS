import { readFileSync } from "node:fs";
import path from "node:path";

import {
  expectedIntegratedModuleScaffoldArtifacts,
  loadIntegratedModuleScaffoldSpecs,
} from "../src/development-scaffolding/moduleScaffoldArtifacts.js";
import { verifyModuleScaffoldImplementation } from
  "../src/development-scaffolding/moduleScaffoldWorkspace.js";

const repositoryRoot = process.cwd();
const databasePath = path.join(repositoryRoot, "data", "desktop-editor-spike.sqlite");
const specs = loadIntegratedModuleScaffoldSpecs(repositoryRoot);
for (const spec of specs) {
  verifyModuleScaffoldImplementation(spec, repositoryRoot, databasePath);
}
for (const [relativePath, expected] of expectedIntegratedModuleScaffoldArtifacts(specs)) {
  const actual = readFileSync(path.join(repositoryRoot, relativePath), "utf8");
  if (actual !== expected) {
    throw new Error(
      `Generated Module scaffold artifact '${relativePath}' is stale. `
      + "Run npm run scaffold:module:generate.",
    );
  }
}
console.log(`Integrated Module scaffold contracts verified: ${specs.length}.`);
