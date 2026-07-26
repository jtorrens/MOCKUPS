import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { readdirSync } from "node:fs";
import path from "node:path";
import test from "node:test";

type EvaluatedItem = {
  Identity: string;
  FullPath?: string;
};

type EvaluatedProject = {
  Items: {
    ProjectReference: EvaluatedItem[];
    PackageReference: EvaluatedItem[];
  };
};

const repositoryRoot = process.cwd();

const expectedProjects = new Map([
  [
    "src/Mockups.Desktop/Mockups.DesktopEditorShell.csproj",
    {
      projectReferences: ["src/Mockups.Domain/Mockups.Domain.csproj"],
      packageReferences: [
        "Avalonia",
        "Avalonia.Controls.ColorPicker",
        "Avalonia.Controls.WebView",
        "Avalonia.Desktop",
        "Avalonia.Fonts.Inter",
        "Avalonia.Themes.Fluent",
        "AvaloniaUI.DiagnosticsSupport",
        "Microsoft.Data.Sqlite",
        "SQLitePCLRaw.bundle_e_sqlite3",
        "SukiUI",
      ],
    },
  ],
  [
    "src/Mockups.Domain/Mockups.Domain.csproj",
    {
      projectReferences: [],
      packageReferences: [],
    },
  ],
]);

function projectFiles(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === "bin" || entry.name === "obj") return [];
      return projectFiles(fullPath);
    }
    return entry.name.endsWith(".csproj") ? [fullPath] : [];
  });
}

function repositoryPath(fullPath: string): string {
  return path.relative(repositoryRoot, fullPath).split(path.sep).join("/");
}

function evaluate(projectPath: string): EvaluatedProject {
  const output = execFileSync(
    "dotnet",
    [
      "msbuild",
      projectPath,
      "-getItem:ProjectReference",
      "-getItem:PackageReference",
    ],
    {
      cwd: repositoryRoot,
      encoding: "utf8",
    },
  );
  return JSON.parse(output) as EvaluatedProject;
}

test("the evaluated .NET project graph exposes only declared dependencies", () => {
  const actualProjects = projectFiles(path.join(repositoryRoot, "src"))
    .map(repositoryPath)
    .sort();
  assert.deepEqual(actualProjects, [...expectedProjects.keys()].sort());

  for (const projectPath of actualProjects) {
    const expected = expectedProjects.get(projectPath);
    assert.ok(expected, `Missing expected dependency declaration for ${projectPath}`);
    const evaluated = evaluate(projectPath);
    const projectReferences = evaluated.Items.ProjectReference
      .map((item) => repositoryPath(item.FullPath ?? item.Identity))
      .sort();
    const packageReferences = evaluated.Items.PackageReference
      .map((item) => item.Identity)
      .sort();
    assert.deepEqual(
      projectReferences,
      [...expected.projectReferences].sort(),
      `${projectPath} has an undeclared project dependency`,
    );
    assert.deepEqual(
      packageReferences,
      [...expected.packageReferences].sort(),
      `${projectPath} has an undeclared package dependency`,
    );
  }
});

test("Domain compiles without project or package capabilities", () => {
  const domain = evaluate("src/Mockups.Domain/Mockups.Domain.csproj");
  assert.deepEqual(domain.Items.ProjectReference, []);
  assert.deepEqual(domain.Items.PackageReference, []);
});
