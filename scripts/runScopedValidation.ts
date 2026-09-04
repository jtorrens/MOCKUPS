import { spawnSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs } from "node:util";

import { isRetiredRepositoryPath } from "./validation/checkRetiredContracts.js";

export type ScopedValidationLevel = "changed" | "revision";

export type ValidationStep = {
  id: string;
  command: string;
  args: readonly string[];
  reason: string;
};

type PreviewManifest = {
  components: Record<
    string,
    { contract: string; resolver: string; renderable: string }
  >;
  modules: Record<
    string,
    { contract: string; resolver: string; renderable: string }
  >;
};

function npmStep(
  id: string,
  script: string,
  reason: string,
  trailingArgs: readonly string[] = [],
): ValidationStep {
  return {
    id,
    command: process.platform === "win32" ? "npm.cmd" : "npm",
    args: ["run", script, ...trailingArgs],
    reason,
  };
}

function gitDiffCheckStep(): ValidationStep {
  return {
    id: "diff-check",
    command: "git",
    args: ["diff", "--check"],
    reason: "all revisions must remain free of whitespace errors",
  };
}

function normalizeRepositoryPath(value: string): string {
  return value.replaceAll("\\", "/").replace(/^\.\/+/u, "");
}

function loadManifest(repositoryRoot: string): PreviewManifest {
  return JSON.parse(
    readFileSync(
      path.join(
        repositoryRoot,
        "src/desktop-preview/desktopPreviewManifest.json",
      ),
      "utf8",
    ),
  ) as PreviewManifest;
}

function previewOwnerForFile(
  file: string,
  manifest: PreviewManifest,
): string | undefined {
  if (!file.startsWith("src/desktop-preview/")) return undefined;
  const relative = `./${path.posix.basename(file).replace(/\.(?:ts|tsx)$/u, "")}`;
  for (const [owner, routes] of Object.entries(manifest.components)) {
    if (Object.values(routes).includes(relative)) {
      return `component:${owner}`;
    }
  }
  for (const [owner, routes] of Object.entries(manifest.modules)) {
    if (Object.values(routes).includes(relative)) {
      return `module:${owner}`;
    }
  }
  return undefined;
}

function previewTestsForOwner(
  repositoryRoot: string,
  ownerSelector: string,
  manifest: PreviewManifest,
): string[] {
  const owner = ownerSelector.slice(ownerSelector.indexOf(":") + 1);
  const routes = ownerSelector.startsWith("component:")
    ? manifest.components[owner]
    : manifest.modules[owner];
  const routeTerms = routes
    ? Object.values(routes)
      .filter((value): value is string => typeof value === "string")
      .map((value) =>
        path.posix.basename(value)
          .replace(/(?:Component|Module)(?:Contract|Resolver|Renderable)$/u, ""))
    : [];
  const stableTerms = new Set(
    [
      owner,
      owner.replace(/^module\./u, ""),
      owner.replaceAll("_", ""),
      owner.replaceAll(".", ""),
      ...routeTerms,
    ].map((value) => value.toLowerCase()),
  );
  const candidates = spawnSync(
    "git",
    ["ls-files", "tests/animation/*.test.ts"],
    { cwd: repositoryRoot, encoding: "utf8" },
  );
  if (candidates.status !== 0) return [];
  return candidates.stdout
    .split(/\r?\n/u)
    .filter(Boolean)
    .filter((file) => {
      const basename = path.basename(file).toLowerCase();
      if ([...stableTerms].some((term) => basename.includes(term))) {
        return true;
      }
      const source = readFileSync(path.join(repositoryRoot, file), "utf8")
        .toLowerCase();
      return [...stableTerms].some((term) =>
        source.includes(`${term}component`)
        || source.includes(`${term}module`));
    })
    .map(normalizeRepositoryPath);
}

function isBroadPreviewPath(file: string): boolean {
  if (!file.startsWith("src/desktop-preview/")) return false;
  const basename = path.posix.basename(file).toLowerCase();
  return file.endsWith("desktopPreviewManifest.json")
    || basename.includes("registry")
    || basename.includes("common")
    || basename.includes("boundary")
    || basename.includes("adapter")
    || basename.startsWith("generated")
    || basename === "desktoppreviewcomponents.ts"
    || basename === "desktoppreviewmodules.ts";
}

export function planScopedValidation(
  repositoryRoot: string,
  changedFiles: readonly string[],
): ValidationStep[] {
  const files = [...new Set(
    changedFiles.map(normalizeRepositoryPath).filter(Boolean),
  )].sort();
  if (files.length === 0) return [gitDiffCheckStep()];

  const manifest = loadManifest(repositoryRoot);
  const steps = new Map<string, ValidationStep>();
  const add = (step: ValidationStep) => steps.set(step.id, step);
  const owners = new Set<string>();
  const previewTests = new Set<string>();
  const unclassified: string[] = [];
  let broadPreview = false;
  let application = false;
  let desktopCore = false;
  let desktopUi = false;
  let desktopCompile = false;
  let typecheck = false;
  let architectureContracts = false;
  let architectureGenerated = false;
  let architecturePipeline = false;
  let architectureRetired = false;
  let architectureBoundaries = false;
  let scaffoldingComponent = false;
  let scaffoldingModule = false;
  let tooling = false;
  let database = false;

  for (const file of files) {
    if (isRetiredRepositoryPath(file)) {
      architectureRetired = true;
      continue;
    }
    if (file === "scripts/validation/checkDocumentationContracts.ts") {
      architectureContracts = true;
      architecturePipeline = true;
      tooling = true;
      typecheck = true;
      continue;
    }
    if (file === "scripts/validation/checkRetiredContracts.ts") {
      architectureRetired = true;
      architecturePipeline = true;
      tooling = true;
      typecheck = true;
      continue;
    }
    if (file === "src/Mockups.Desktop/Common/SvgReplacementService.cs") {
      desktopCompile = true;
      add(npmStep(
        "application:svg-fill",
        "test:focus:application",
        "the SVG transformation service has one focused Application contract",
        ["--", "--exact", "SVG fill transforms keep direct reusable geometry"],
      ));
      add(npmStep(
        "desktop:svg-fill-preview",
        "test:focus:desktop",
        "the generated SVG preview has one focused persistence and rendering regression",
        ["--", "--exact", "generated fill SVG previews preserve their filled geometry"],
      ));
      continue;
    }
    if (file === "data/mockups.sqlite") {
      database = true;
      desktopCore = true;
      broadPreview = true;
      continue;
    }
    if (file === "AGENTS.md"
      || file.startsWith("docs/architecture/")
      || file === "docs/README.md") {
      architectureContracts = true;
      continue;
    }
    if (file.startsWith("archive/")) {
      architectureRetired = true;
      continue;
    }
    if (file.startsWith("docs/")
      || file === "README.md"
      || file === ".gitignore"
      || file === ".editorconfig") {
      continue;
    }
    if (file.startsWith("tests/animation/")
      && file.endsWith(".test.ts")) {
      previewTests.add(file);
      typecheck = true;
      continue;
    }
    if (file.startsWith("tests/animation/")
      && file.endsWith(".ts")) {
      broadPreview = true;
      typecheck = true;
      continue;
    }
    if (file.startsWith("tests/architecture/")) {
      architectureBoundaries = true;
      continue;
    }
    if (file.startsWith("tests/scaffolding/")) {
      if (file.includes("component")) scaffoldingComponent = true;
      if (file.includes("module")) scaffoldingModule = true;
      continue;
    }
    if (file.startsWith("tests/tooling/")) {
      tooling = true;
      continue;
    }
    if (file.startsWith("tests/Mockups.Application.Tests/")) {
      application = true;
      desktopCompile = true;
      continue;
    }
    if (file.startsWith("tests/Mockups.Desktop.Tests/")) {
      desktopCompile = true;
      desktopCore = true;
      continue;
    }
    if (file.startsWith("src/desktop-preview/")) {
      typecheck = true;
      const owner = previewOwnerForFile(file, manifest);
      if (owner && !isBroadPreviewPath(file)) {
        owners.add(owner);
        for (const test of previewTestsForOwner(
          repositoryRoot,
          owner,
          manifest,
        )) {
          previewTests.add(test);
        }
      } else {
        broadPreview = true;
      }
      continue;
    }
    if (file.startsWith("src/Mockups.Application/")
      || file.startsWith("src/Mockups.Application.PersistencePorts/")
      || file.startsWith("src/Mockups.Domain/")) {
      application = true;
      desktopCompile = true;
      if (file.startsWith("src/Mockups.Application.PersistencePorts/")) {
        architectureBoundaries = true;
      }
      continue;
    }
    if (file.startsWith("src/Mockups.Desktop/")
      || file.startsWith("src/Mockups.Desktop.Host/")) {
      desktopCompile = true;
      desktopCore = true;
      if (file.endsWith(".axaml")
        || file.includes("/MainWindow.")
        || file.includes("/Common/")) {
        desktopUi = true;
      }
      continue;
    }
    if (file.startsWith("src/Mockups.Persistence.")) {
      desktopCompile = true;
      desktopCore = true;
      database = true;
      continue;
    }
    if (file.startsWith("assets/")) {
      database = true;
      desktopCompile = true;
      if (file.endsWith(".svg")) {
        add(npmStep(
          "desktop:icon-theme-svg",
          "test:focus:desktop",
          "SVG assets must remain valid through the strict Icon Theme owner",
          [
            "--",
            "--exact",
            "Icon Theme repository preserves rows and strict token files",
          ],
        ));
        if (path.posix.basename(file).includes("_fill")) {
          add(npmStep(
            "desktop:svg-fill-preview",
            "test:focus:desktop",
            "generated fill SVG assets must preserve visible geometry",
            [
              "--",
              "--exact",
              "generated fill SVG previews preserve their filled geometry",
            ],
          ));
        }
      }
      continue;
    }
    if (file.startsWith("scaffolding/components/")) {
      scaffoldingComponent = true;
      architectureGenerated = true;
      database = true;
      continue;
    }
    if (file.startsWith("scaffolding/modules/")) {
      scaffoldingModule = true;
      architectureGenerated = true;
      database = true;
      continue;
    }
    if (file.startsWith("src/development-scaffolding/")) {
      typecheck = true;
      if (file.toLowerCase().includes("component")) {
        scaffoldingComponent = true;
      } else if (file.toLowerCase().includes("module")) {
        scaffoldingModule = true;
      } else {
        scaffoldingComponent = true;
        scaffoldingModule = true;
      }
      continue;
    }
    if (file.startsWith("src/shared/")
      || file.startsWith("src/visual/")) {
      typecheck = true;
      broadPreview = true;
      continue;
    }
    if (file.startsWith("scripts/validation/")
      || file === "scripts/runRepositoryGates.ts"
      || file === "scripts/runRepositoryValidation.ts"
      || file === "scripts/runScopedValidation.ts"
      || file === "package.json"
      || file === "package-lock.json"
      || file === "tsconfig.json"
      || file.includes(".github/workflows/")) {
      tooling = true;
      architecturePipeline = true;
      typecheck = true;
      continue;
    }
    if (file.endsWith(".csproj")
      || file === "Directory.Build.props"
      || file === "Directory.Build.targets"
      || file === "Directory.Packages.props"
      || file.endsWith(".props")
      || file.endsWith(".targets")) {
      desktopCompile = true;
      architectureBoundaries = true;
      continue;
    }
    if (file.startsWith("scripts/")) {
      tooling = true;
      typecheck = true;
      continue;
    }
    unclassified.push(file);
  }

  if (unclassified.length > 0) {
    throw new Error(
      "Scoped validation stopped because these paths have no declared "
      + "validation owner:\n"
      + unclassified.map((file) => `  - ${file}`).join("\n")
      + "\nClassify each path and its focused checks before validating it. "
      + "The complete repository suite is never selected implicitly.",
    );
  }

  if (typecheck) {
    add(npmStep("typecheck", "typecheck", "TypeScript or validation code changed"));
  }
  if (desktopCompile) {
    add(npmStep(
      "desktop-compile",
      "desktop:compile",
      "compiled Desktop code, tests or resources changed",
    ));
  }
  if (application) {
    add(npmStep(
      "application",
      "test:unit",
      "Application or Domain behavior changed",
    ));
  }
  if (broadPreview) {
    add(npmStep(
      "preview-all",
      "animation:test:preview",
      "a shared Preview, fixture or persistence change can affect many owners",
    ));
    add(npmStep(
      "desktop-exhaustive",
      "animation:test:desktop:exhaustive",
      "shared Preview output requires every manifest fixture",
    ));
  } else {
    for (const test of previewTests) {
      add(npmStep(
        `preview:${test}`,
        "test:focus:preview",
        "focused Preview coverage imports or names the changed owner",
        ["--", test],
      ));
    }
    for (const owner of owners) {
      add(npmStep(
        `owner:${owner}`,
        "animation:test:desktop:owner",
        `only the ${owner} Preview owner changed`,
        ["--", "--owner", owner],
      ));
    }
  }
  if (desktopCore) {
    add(npmStep(
      "desktop-core",
      "animation:test:desktop:core",
      "Desktop or persistence behavior changed without an exact declared test",
    ));
  }
  if (desktopUi) {
    add(npmStep(
      "desktop-ui",
      "animation:test:desktop:ui",
      "shared or native visual-tree code changed",
    ));
  }
  if (database) {
    add(npmStep(
      "database",
      "desktop:db:validate",
      "persisted data or a referenced Project asset changed",
    ));
  }
  if (scaffoldingComponent) {
    add(npmStep(
      "component-scaffolding-tests",
      "test:focus:preview",
      "Component scaffolding changed",
      ["--", "tests/scaffolding/componentScaffold.test.ts"],
    ));
    add(npmStep(
      "component-scaffolding-verify",
      "scaffold:verify",
      "integrated Component artifacts must remain deterministic",
    ));
  }
  if (scaffoldingModule) {
    add(npmStep(
      "module-scaffolding-tests",
      "test:focus:preview",
      "Module scaffolding changed",
      ["--", "tests/scaffolding/moduleScaffold.test.ts"],
    ));
    add(npmStep(
      "module-scaffolding-verify",
      "scaffold:module:verify",
      "integrated Module artifacts must remain deterministic",
    ));
  }
  if (tooling) {
    add(npmStep(
      "tooling",
      "test:tooling",
      "repository tooling or its executable tests changed",
    ));
  }
  if (architectureContracts) {
    add(npmStep(
      "contracts",
      "validate:contracts",
      "normative active documentation changed",
    ));
  }
  if (architectureGenerated) {
    add(npmStep(
      "generated",
      "validate:generated",
      "generated integration contracts changed",
    ));
  }
  if (architecturePipeline) {
    add(npmStep(
      "pipeline",
      "validate:pipeline",
      "validation orchestration changed",
    ));
  }
  if (architectureRetired) {
    add(npmStep(
      "retired",
      "validate:retired",
      "retired implementation paths must remain absent",
    ));
  }
  if (architectureBoundaries) {
    add(npmStep(
      "architecture",
      "validate:architecture",
      "compiled or Preview dependency boundaries changed",
    ));
  }
  add(gitDiffCheckStep());
  return [...steps.values()];
}

function gitLines(
  repositoryRoot: string,
  args: readonly string[],
): string[] {
  const result = spawnSync("git", args, {
    cwd: repositoryRoot,
    encoding: "utf8",
  });
  if (result.status !== 0) {
    throw new Error(result.stderr.trim() || `git ${args.join(" ")} failed`);
  }
  return result.stdout.split(/\r?\n/u).filter(Boolean);
}

export function discoverChangedFiles(
  repositoryRoot: string,
  level: ScopedValidationLevel,
  base?: string,
): string[] {
  const staged = new Set(gitLines(
    repositoryRoot,
    ["diff", "--cached", "--name-only"],
  ));
  const working = [
    ...gitLines(repositoryRoot, ["diff", "--name-only", "HEAD"]),
    ...gitLines(repositoryRoot, [
      "ls-files",
      "--others",
      "--exclude-standard",
    ]),
  ];
  const scopedWorking = excludeUnstagedWorkstationDatabase(
    working,
    staged,
  );
  if (level === "changed") return [...new Set(scopedWorking)].sort();
  const revisionBase = base
    ?? (scopedWorking.length > 0 ? "HEAD" : "HEAD^");
  const committed = revisionBase === "HEAD"
    ? []
    : gitLines(repositoryRoot, [
      "diff",
      "--name-only",
      `${revisionBase}...HEAD`,
    ]);
  return [...new Set([...committed, ...scopedWorking])].sort();
}

export function excludeUnstagedWorkstationDatabase(
  files: readonly string[],
  stagedFiles: ReadonlySet<string>,
): string[] {
  return files.filter((file) =>
    file !== "data/mockups.sqlite" || stagedFiles.has(file));
}

function run(): void {
  const { values } = parseArgs({
    options: {
      level: { type: "string" },
      base: { type: "string" },
      file: { type: "string", multiple: true },
      list: { type: "boolean", default: false },
    },
    strict: true,
  });
  const level = values.level ?? "changed";
  if (level !== "changed" && level !== "revision") {
    throw new Error(
      `Unknown scoped validation level '${level}'. Expected changed or revision.`,
    );
  }
  const repositoryRoot = process.cwd();
  if (!existsSync(path.join(repositoryRoot, "package.json"))) {
    throw new Error("Scoped validation must run from the repository root.");
  }
  if (level === "revision") {
    process.stdout.write(
      "\n[workstation-update-maintenance] "
      + "node scripts/workstationProject.mjs require-update\n"
      + "  MOCKUPS must remain closed for the complete repository update\n",
    );
    if (!values.list) {
      const maintenance = spawnSync(
        process.execPath,
        ["scripts/workstationProject.mjs", "require-update"],
        {
          cwd: repositoryRoot,
          env: process.env,
          stdio: "inherit",
        },
      );
      if (maintenance.error) throw maintenance.error;
      if (maintenance.status !== 0) {
        process.exit(maintenance.status ?? 1);
      }
    }
    process.stdout.write(
      "\n[workstation-database-parity] "
      + "node scripts/workstationProject.mjs check-if-present\n"
      + "  a local operational database must match its repository snapshot\n",
    );
    if (!values.list) {
      const parity = spawnSync(
        process.execPath,
        ["scripts/workstationProject.mjs", "check-if-present"],
        {
          cwd: repositoryRoot,
          env: process.env,
          stdio: "inherit",
        },
      );
      if (parity.error) throw parity.error;
      if (parity.status !== 0) process.exit(parity.status ?? 1);
    }
  }
  const files = values.file?.length
    ? values.file
    : discoverChangedFiles(repositoryRoot, level, values.base);
  const plan = planScopedValidation(repositoryRoot, files);
  process.stdout.write(
    `Scoped ${level} validation for ${files.length} changed file(s):\n`,
  );
  for (const file of files) process.stdout.write(`  ${file}\n`);
  for (const step of plan) {
    process.stdout.write(
      `\n[${step.id}] ${step.command} ${step.args.join(" ")}\n`
      + `  ${step.reason}\n`,
    );
    if (values.list) continue;
    const result = spawnSync(step.command, step.args, {
      cwd: repositoryRoot,
      env: process.env,
      stdio: "inherit",
    });
    if (result.error) throw result.error;
    if (result.status !== 0) process.exit(result.status ?? 1);
  }
}

const executedPath = process.argv[1] ? path.resolve(process.argv[1]) : "";
if (executedPath === fileURLToPath(import.meta.url)) run();
