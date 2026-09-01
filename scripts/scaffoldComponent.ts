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
import {
  integrateComponentScaffold,
  materializeComponentScaffold,
  verifyComponentScaffoldImplementation,
} from "../src/development-scaffolding/componentScaffoldWorkspace.js";
import { workstationProjectPaths } from "./workstationProject.mjs";

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
  if (values.spec
      || values.database
      || values["dry-run"]
      || values.materialize
      || values.integrate
      || values.verify) {
    throw new Error(
      "--print-template cannot be combined with another Component scaffold option.",
    );
  }
  console.log(JSON.stringify(componentScaffoldTemplate(), null, 2));
  process.exit(0);
}

const modes = [
  values["dry-run"] ? "dry-run" : "",
  values.materialize ? "materialize" : "",
  values.integrate ? "integrate" : "",
  values.verify ? "verify" : "",
].filter(Boolean);
if (modes.length !== 1) {
  throw new Error(
    "Component scaffolding requires exactly one of --dry-run, --materialize, --integrate or --verify.",
  );
}
if (!values.spec) {
  throw new Error("Component scaffolding requires an explicit --spec JSON path.");
}

const repositoryRoot = process.cwd();
const databasePath = values.database
  ? path.resolve(values.database)
  : workstationProjectPaths(repositoryRoot).workstationDatabase;
const specPath = resolveComponentScaffoldSpecPath(repositoryRoot, values.spec!);
const spec = parseComponentScaffoldSpec(
  JSON.parse(readFileSync(specPath, "utf8")) as unknown,
);
if (values.integrate) {
  console.log(JSON.stringify(
    integrateComponentScaffold(spec, repositoryRoot, databasePath),
    null,
    2,
  ));
} else if (values.verify) {
  console.log(JSON.stringify(
    verifyComponentScaffoldImplementation(spec, repositoryRoot, databasePath),
    null,
    2,
  ));
} else {
  const inventory = loadComponentScaffoldInventory(repositoryRoot, databasePath);
  const plan = createComponentScaffoldPlan(spec, inventory, repositoryRoot);
  console.log(JSON.stringify(
    values.materialize
      ? materializeComponentScaffold(spec, plan, repositoryRoot)
      : plan,
    null,
    2,
  ));
}
