import type { ArchitectureValidationContext } from "./validationContext.js";

type WorkflowStep = {
  uses?: string;
  run?: string;
};

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
  assertContains,
}: ArchitectureValidationContext) {
  const packageScripts = (JSON.parse(readText("package.json")) as {
    scripts?: Record<string, string>;
  }).scripts ?? {};
  const repositoryTestScript = packageScripts["test:repository"] ?? "";
  for (const [scriptName, expectedOwner] of [
    ["validate:contracts", "runValidationModule.ts contracts"],
    ["validate:generated", "runValidationModule.ts generated"],
    ["validate:pipeline", "runValidationModule.ts pipeline"],
    ["validate:retired", "runValidationModule.ts retired"],
    ["validate:architecture", "npm run test:architecture"],
  ] as const) {
    if (!(packageScripts[scriptName] ?? "").includes(expectedOwner)) {
      addViolation(
        "package.json",
        `${scriptName} must execute its focused validation owner`,
      );
    }
  }
  if (!(packageScripts["check:architecture"] ?? "")
    .includes("npm run validate:contracts")
      || !(packageScripts["check:architecture"] ?? "")
        .includes("npm run validate:generated")
      || !(packageScripts["check:architecture"] ?? "")
        .includes("npm run validate:pipeline")
      || !(packageScripts["check:architecture"] ?? "")
        .includes("npm run validate:retired")
      || !(packageScripts["check:architecture"] ?? "")
        .includes("npm run validate:architecture")) {
    addViolation(
      "package.json",
      "the architecture aggregate must execute every focused validation owner",
    );
  }
  const desktopBuildIndex = repositoryTestScript.indexOf("npm run desktop:compile");
  const unusedAnalysisIndex = repositoryTestScript.indexOf(
    "npm run check:unused:desktop",
  );
  if (desktopBuildIndex < 0
    || unusedAnalysisIndex < 0
    || desktopBuildIndex > unusedAnalysisIndex) {
    addViolation(
      "package.json",
      "the full test gate must build the desktop project before unused-parameter analysis",
    );
  }
  if (packageScripts.test !== "tsx scripts/runRepositoryValidation.ts") {
    addViolation(
      "package.json",
      "the public repository gate must isolate the staged parity database through its validation owner",
    );
  }
  for (const requiredTerm of [
    "git",
    `["show", \`:\${parityPath}\`]`,
    "MOCKUPS_VALIDATION_DATABASE",
    "symlinkSync",
    "repositoryAssets",
    `["run", "test:repository"]`,
    "finally",
    "rmSync",
  ]) {
    assertContains(
      "scripts/runRepositoryValidation.ts",
      requiredTerm,
      "the validation owner must use a disposable staged parity database and always clean it",
    );
  }
  for (const group of ["core", "ui", "exhaustive"]) {
    if (!(packageScripts[`animation:test:desktop:${group}`] ?? "")
      .includes(`--group ${group}`)
      || !repositoryTestScript.includes("npm run animation:test")) {
      addViolation(
        "package.json",
        `the complete repository gate must execute the isolated desktop ${group} group`,
      );
    }
  }
  if (packageScripts["test:focus:preview"] !== "tsx --test"
      || !(packageScripts["test:focus:desktop"] ?? "").endsWith(" --")
      || !(packageScripts["test:focus:desktop"] ?? "").startsWith("dotnet run ")
      || !(packageScripts["animation:test:desktop:owner"] ?? "")
        .endsWith("-- --group exhaustive")
      || !(packageScripts["test:guard"] ?? "").includes("npm run check:architecture")) {
    addViolation(
      "package.json",
      "focused Preview, desktop and manifest-owner selectors plus the shared architecture guard must remain available",
    );
  }
  for (const requiredTerm of [
    `SingleArgumentValue(args, "--group")`,
    `ArgumentValues(args, "--exact")`,
    `ArgumentValues(args, "--filter")`,
    `ArgumentValues(args, "--owner")`,
    "Unknown exact desktop test",
    "Unknown Preview owner selector",
    "Desktop test selection matched no tests.",
    `"core" => !isolatedUiTests.Contains(test.Name)`,
    `"ui" => isolatedUiTests.Contains(test.Name)`,
    `"exhaustive" => exhaustiveTests.Contains(test.Name)`,
    "MOCKUPS_VALIDATION_DATABASE",
  ]) {
    assertContains(
      "tests/Mockups.Desktop.Tests/Program.cs",
      requiredTerm,
      "the desktop test owner must keep fail-closed focused selection, isolated groups and parity-path injection",
    );
  }
  if (!(packageScripts["test:cold"] ?? "").includes("dotnet clean")
      || !(packageScripts["test:cold"] ?? "").includes("npm test")) {
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
