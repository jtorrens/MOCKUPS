import { readFileSync } from "node:fs";
import path from "node:path";
import { parseArgs } from "node:util";

import {
  componentScaffoldTemplate,
  createComponentScaffoldPlan,
  loadComponentScaffoldInventory,
  parseComponentScaffoldSpec,
  resolveComponentScaffoldSpecPath,
} from "../src/development-scaffolding/componentScaffold.js";

const { values } = parseArgs({
  options: {
    spec: { type: "string" },
    database: { type: "string" },
    "dry-run": { type: "boolean", default: false },
    "print-template": { type: "boolean", default: false },
  },
  strict: true,
  allowPositionals: false,
});

if (values["print-template"]) {
  if (values.spec || values.database || values["dry-run"]) {
    throw new Error("--print-template cannot be combined with --spec, --database or --dry-run.");
  }
  console.log(JSON.stringify(componentScaffoldTemplate(), null, 2));
  process.exit(0);
}

if (!values["dry-run"]) {
  throw new Error(
    "Component scaffolding phase 1 is read-only. Pass --dry-run; --apply is intentionally unavailable.",
  );
}
if (!values.spec) {
  throw new Error("Component scaffolding requires an explicit --spec JSON path.");
}

const repositoryRoot = process.cwd();
const specPath = resolveComponentScaffoldSpecPath(repositoryRoot, values.spec);
const databasePath = values.database
  ? path.resolve(values.database)
  : path.join(repositoryRoot, "data", "desktop-editor-spike.sqlite");
const spec = parseComponentScaffoldSpec(
  JSON.parse(readFileSync(specPath, "utf8")) as unknown,
);
const inventory = loadComponentScaffoldInventory(repositoryRoot, databasePath);
const plan = createComponentScaffoldPlan(spec, inventory, repositoryRoot);

console.log(JSON.stringify(plan, null, 2));
