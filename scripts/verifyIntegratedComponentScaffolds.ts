import { readdirSync, readFileSync } from "node:fs";
import path from "node:path";

import {
  parseComponentScaffoldSpec,
} from "../src/development-scaffolding/componentScaffold.js";
import {
  integratedComponentSpecRoot,
  verifyComponentScaffoldImplementation,
} from "../src/development-scaffolding/componentScaffoldWorkspace.js";

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
console.log(`Integrated Component scaffold contracts verified: ${results.length}.`);
