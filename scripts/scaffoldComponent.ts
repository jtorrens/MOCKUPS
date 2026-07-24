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
  materializeComponentScaffold,
  verifyComponentScaffoldImplementation,
} from "../src/development-scaffolding/componentScaffoldWorkspace.js";
import {
  adoptExistingComponentScaffold,
  type ComponentScaffoldIntent,
} from "../src/development-scaffolding/componentScaffoldAdoption.js";

const { values } = parseArgs({
  options: {
    spec: { type: "string" },
    database: { type: "string" },
    "component-type": { type: "string" },
    intent: { type: "string" },
    "dry-run": { type: "boolean", default: false },
    materialize: { type: "boolean", default: false },
    verify: { type: "boolean", default: false },
    "adopt-existing": { type: "boolean", default: false },
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
      || values.verify
      || values["adopt-existing"]
      || values["component-type"]
      || values.intent) {
    throw new Error(
      "--print-template cannot be combined with --spec, --database, --dry-run, --materialize or --verify.",
    );
  }
  console.log(JSON.stringify(componentScaffoldTemplate(), null, 2));
  process.exit(0);
}

const modes = [
  values["dry-run"] ? "dry-run" : "",
  values.materialize ? "materialize" : "",
  values.verify ? "verify" : "",
  values["adopt-existing"] ? "adopt-existing" : "",
].filter(Boolean);
if (modes.length !== 1) {
  throw new Error(
    "Component scaffolding requires exactly one of --dry-run, --materialize, --verify or --adopt-existing.",
  );
}
if (!values["adopt-existing"] && !values.spec) {
  throw new Error("Component scaffolding requires an explicit --spec JSON path.");
}

const repositoryRoot = process.cwd();
const databasePath = values.database
  ? path.resolve(values.database)
  : path.join(repositoryRoot, "data", "desktop-editor-spike.sqlite");
if (values["adopt-existing"]) {
  if (!values["component-type"] || !values.intent || values.spec) {
    throw new Error(
      "--adopt-existing requires --component-type and --intent and cannot use --spec.",
    );
  }
  const intentPath = resolveComponentScaffoldSpecPath(repositoryRoot, values.intent);
  const intent = JSON.parse(readFileSync(intentPath, "utf8")) as ComponentScaffoldIntent;
  console.log(JSON.stringify(
    adoptExistingComponentScaffold(
      values["component-type"],
      intent,
      repositoryRoot,
      databasePath,
    ),
    null,
    2,
  ));
  process.exit(0);
}

const specPath = resolveComponentScaffoldSpecPath(repositoryRoot, values.spec!);
const spec = parseComponentScaffoldSpec(
  JSON.parse(readFileSync(specPath, "utf8")) as unknown,
);
if (values.verify) {
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
