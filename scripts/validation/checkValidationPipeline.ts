import type { ArchitectureValidationContext } from "./validationContext.js";

type WorkflowStep = {
  uses?: string;
  run?: string;
};

function commandStages(command: string): string[] {
  return command
    .split(/\s*&&\s*/)
    .map((stage) => stage.trim())
    .filter(Boolean);
}

function sameStages(
  actualCommand: string,
  expectedStages: readonly string[],
): boolean {
  const actualStages = commandStages(actualCommand);
  return actualStages.length === expectedStages.length
    && actualStages.every(
      (stage, index) => stage === expectedStages[index],
    );
}

function workflowSteps(
  readText: ArchitectureValidationContext["readText"],
  relativePath: string,
): WorkflowStep[] {
  const lines = readText(relativePath).split(/\r?\n/);
  const stepsLine = lines.findIndex((line) => /^\s*steps:\s*$/.test(line));
  if (stepsLine < 0) return [];
  const stepsIndent = lines[stepsLine]!.match(/^\s*/)?.[0].length ?? 0;
  const steps: WorkflowStep[] = [];
  let current: WorkflowStep | undefined;
  for (const line of lines.slice(stepsLine + 1)) {
    if (!line.trim() || line.trimStart().startsWith("#")) continue;
    const indent = line.match(/^\s*/)?.[0].length ?? 0;
    if (indent <= stepsIndent) break;
    const item = line.match(/^\s*-\s+([A-Za-z][A-Za-z0-9_-]*):\s*(.*)$/);
    if (item) {
      if (current) steps.push(current);
      current = {};
      const key = item[1] ?? "";
      const value = (item[2] ?? "").trim().replace(/^["']|["']$/g, "");
      if (key === "uses" || key === "run") current[key] = value;
      continue;
    }
    const property = line.match(/^\s+([A-Za-z][A-Za-z0-9_-]*):\s*(.*)$/);
    if (!current || !property) continue;
    const key = property[1] ?? "";
    const value = (property[2] ?? "").trim().replace(/^["']|["']$/g, "");
    if (key === "uses" || key === "run") current[key] = value;
  }
  if (current) steps.push(current);
  return steps;
}

export function checkValidationPipeline({
  readText,
  addViolation,
}: ArchitectureValidationContext) {
  const packageScripts = (JSON.parse(readText("package.json")) as {
    scripts?: Record<string, string>;
  }).scripts ?? {};
  const repositoryTestScript = packageScripts["test:repository"] ?? "";
  for (const [scriptName, expectedCommand] of [
    ["validate:contracts", "tsx scripts/validation/runValidationModule.ts contracts"],
    ["validate:generated", "tsx scripts/validation/runValidationModule.ts generated"],
    ["validate:pipeline", "tsx scripts/validation/runValidationModule.ts pipeline"],
    ["validate:retired", "tsx scripts/validation/runValidationModule.ts retired"],
    ["validate:architecture", "npm run test:architecture"],
  ] as const) {
    if ((packageScripts[scriptName] ?? "") !== expectedCommand) {
      addViolation(
        "package.json",
        `${scriptName} must execute its focused validation owner`,
      );
    }
  }
  if (!sameStages(
    packageScripts["check:architecture"] ?? "",
    [
      "npm run validate:contracts",
      "npm run validate:generated",
      "npm run validate:pipeline",
      "npm run validate:retired",
      "npm run validate:architecture",
    ],
  )) {
    addViolation(
      "package.json",
      "the architecture aggregate must execute every focused validation owner",
    );
  }
  if (repositoryTestScript !== "tsx scripts/runRepositoryGates.ts") {
    addViolation(
      "package.json",
      "the full test gate must use its executable ordered gate owner",
    );
  }
  if (packageScripts.test !== "tsx scripts/runRepositoryValidation.ts") {
    addViolation(
      "package.json",
      "the public repository gate must isolate the staged parity database through its validation owner",
    );
  }
  const desktopTestCommand =
    "dotnet run --project tests/Mockups.Desktop.Tests/Mockups.DesktopEditorShell.AnimationTests.csproj --";
  for (const group of ["core", "ui", "exhaustive"]) {
    if ((packageScripts[`animation:test:desktop:${group}`] ?? "")
      !== `${desktopTestCommand} --group ${group}`) {
      addViolation(
        "package.json",
        `the desktop suite must expose the isolated ${group} group`,
      );
    }
  }
  if (packageScripts["test:focus:preview"] !== "tsx --test"
      || packageScripts["test:focus:desktop"] !== desktopTestCommand
      || packageScripts["animation:test:desktop:owner"]
        !== `${desktopTestCommand} --group exhaustive`
      || !sameStages(
        packageScripts["test:guard"] ?? "",
        [
          "npm run typecheck",
          "npm run check:architecture",
          "git diff --check",
        ],
      )) {
    addViolation(
      "package.json",
      "focused Preview, desktop and manifest-owner selectors plus the shared architecture guard must remain available",
    );
  }
  if (!sameStages(
    packageScripts["test:cold"] ?? "",
    [
      "dotnet clean src/Mockups.Desktop.Host/Mockups.Desktop.Host.csproj",
      "npm test",
    ],
  )) {
    addViolation(
      "package.json",
      "test:cold must clear desktop build outputs before running the complete repository gate",
    );
  }

  const workflowPath = ".github/workflows/validate.yml";
  const steps = workflowSteps(readText, workflowPath);
  const supportedCheckoutActions = new Set([
    "actions/checkout@v6",
    "actions/checkout@v7",
  ]);
  const checkoutIndexes = steps
    .map((step, index) => step.uses?.startsWith("actions/checkout@") ? index : -1)
    .filter((index) => index >= 0);
  const checkoutIndex = checkoutIndexes[0] ?? -1;
  const checkoutAction = checkoutIndex >= 0 ? steps[checkoutIndex]?.uses ?? "" : "";
  const setupNodeIndex = steps.findIndex(
    (step) => step.uses === "actions/setup-node@v6",
  );
  const setupDotnetIndex = steps.findIndex(
    (step) => step.uses === "actions/setup-dotnet@v5",
  );
  const linuxUiDependenciesCommand =
    "sudo apt-get update && sudo apt-get install --yes xvfb libwebkit2gtk-4.1-dev";
  const linuxUiDependenciesIndex = steps.findIndex(
    (step) => step.run === linuxUiDependenciesCommand,
  );
  const npmCiIndex = steps.findIndex((step) => step.run === "npm ci");
  const coldGateCommand = "xvfb-run -a npm run test:cold";
  const coldGateIndex = steps.findIndex((step) => step.run === coldGateCommand);
  if (checkoutIndexes.length !== 1 || !supportedCheckoutActions.has(checkoutAction)) {
    addViolation(
      workflowPath,
      "repository CI must contain one checkout step using an admitted actions/checkout major",
    );
  }
  if (setupNodeIndex < 0 || setupDotnetIndex < 0) {
    addViolation(
      workflowPath,
      "repository CI must configure the admitted Node and .NET setup actions",
    );
  }
  if (linuxUiDependenciesIndex < 0 || npmCiIndex < 0 || coldGateIndex < 0) {
    addViolation(
      workflowPath,
      "repository CI must install Linux UI dependencies and the npm lockfile before running test:cold under a virtual display",
    );
  }
  const setupCompleteIndex = Math.max(setupNodeIndex, setupDotnetIndex);
  if (checkoutIndex < 0
      || setupNodeIndex <= checkoutIndex
      || setupDotnetIndex <= checkoutIndex
      || linuxUiDependenciesIndex <= setupCompleteIndex
      || npmCiIndex <= linuxUiDependenciesIndex
      || coldGateIndex <= npmCiIndex) {
    addViolation(
      workflowPath,
      `repository CI steps must be ordered checkout, setup, ${linuxUiDependenciesCommand}, npm ci, then ${coldGateCommand}`,
    );
  }
}
