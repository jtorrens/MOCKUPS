import { readFileSync } from "node:fs";
import path from "node:path";
import { parseArgs } from "node:util";

import {
  createModuleScaffoldPlan,
  loadModuleScaffoldInventory,
  moduleScaffoldTemplate,
  parseModuleScaffoldSpec,
  resolveModuleScaffoldSpecPath,
} from "../src/development-scaffolding/moduleScaffold.js";
import {
  integrateModuleScaffold,
  materializeModuleScaffold,
  verifyModuleScaffoldImplementation,
} from "../src/development-scaffolding/moduleScaffoldWorkspace.js";

const { values } = parseArgs({
  options: {
    spec: { type: "string" },
    database: { type: "string" },
    "dry-run": { type: "boolean", default: false },
    materialize: { type: "boolean", default: false },
    integrate: { type: "boolean", default: false },
    verify: { type: "boolean", default: false },
    "print-template": { type: "boolean", default: false },
  },
  strict: true,
  allowPositionals: false,
});

if (values["print-template"]) {
  if (values.spec || values.database || values["dry-run"]
      || values.materialize || values.integrate || values.verify) {
    throw new Error("--print-template cannot be combined with another option.");
  }
  console.log(JSON.stringify(moduleScaffoldTemplate(), null, 2));
  process.exit(0);
}
const modes = [
  values["dry-run"] ? "dry-run" : "",
  values.materialize ? "materialize" : "",
  values.integrate ? "integrate" : "",
  values.verify ? "verify" : "",
].filter(Boolean);
if (modes.length !== 1 || !values.spec) {
  throw new Error(
    "Module scaffolding requires --spec and exactly one of "
    + "--dry-run, --materialize, --integrate or --verify.",
  );
}
const repositoryRoot = process.cwd();
const databasePath = values.database
  ? path.resolve(values.database)
  : path.join(repositoryRoot, "data", "desktop-editor-spike.sqlite");
const specPath = resolveModuleScaffoldSpecPath(repositoryRoot, values.spec);
const spec = parseModuleScaffoldSpec(
  JSON.parse(readFileSync(specPath, "utf8")) as unknown,
);
if (values.integrate) {
  console.log(JSON.stringify(
    integrateModuleScaffold(spec, repositoryRoot, databasePath),
    null,
    2,
  ));
} else if (values.verify) {
  console.log(JSON.stringify(
    verifyModuleScaffoldImplementation(spec, repositoryRoot, databasePath),
    null,
    2,
  ));
} else {
  const inventory = loadModuleScaffoldInventory(repositoryRoot, databasePath);
  const plan = createModuleScaffoldPlan(spec, inventory, repositoryRoot);
  console.log(JSON.stringify(
    values.materialize
      ? materializeModuleScaffold(spec, plan, repositoryRoot)
      : plan,
    null,
    2,
  ));
}
