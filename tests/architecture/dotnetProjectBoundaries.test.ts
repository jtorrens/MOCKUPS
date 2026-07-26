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
    "src/Mockups.Application/Mockups.Application.csproj",
    {
      projectReferences: ["src/Mockups.Domain/Mockups.Domain.csproj"],
      packageReferences: [],
    },
  ],
  [
    "src/Mockups.Desktop/Mockups.DesktopEditorShell.csproj",
    {
      projectReferences: [
        "src/Mockups.Application/Mockups.Application.csproj",
      ],
      packageReferences: [
        "Avalonia",
        "Avalonia.Controls.ColorPicker",
        "Avalonia.Controls.WebView",
        "Avalonia.Desktop",
        "Avalonia.Fonts.Inter",
        "Avalonia.Themes.Fluent",
        "AvaloniaUI.DiagnosticsSupport",
        "SukiUI",
      ],
    },
  ],
  [
    "src/Mockups.Desktop.Host/Mockups.Desktop.Host.csproj",
    {
      projectReferences: [
        "src/Mockups.Application/Mockups.Application.csproj",
        "src/Mockups.Desktop/Mockups.DesktopEditorShell.csproj",
        "src/Mockups.Persistence.Sqlite/Mockups.Persistence.Sqlite.csproj",
      ],
      packageReferences: [
        "Avalonia",
        "Avalonia.Desktop",
        "Avalonia.Fonts.Inter",
        "AvaloniaUI.DiagnosticsSupport",
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
  [
    "src/Mockups.Persistence.Sqlite/Mockups.Persistence.Sqlite.csproj",
    {
      projectReferences: [
        "src/Mockups.Application/Mockups.Application.csproj",
        "src/Mockups.Domain/Mockups.Domain.csproj",
      ],
      packageReferences: [
        "Microsoft.Data.Sqlite",
        "SQLitePCLRaw.bundle_e_sqlite3",
      ],
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

test("Application can see Domain but has no UI or persistence package capabilities", () => {
  const application = evaluate("src/Mockups.Application/Mockups.Application.csproj");
  assert.deepEqual(
    application.Items.ProjectReference.map((item) => repositoryPath(item.FullPath ?? item.Identity)),
    ["src/Mockups.Domain/Mockups.Domain.csproj"],
  );
  assert.deepEqual(application.Items.PackageReference, []);
});

test("workspace coordinator tests compile against Application alone", () => {
  const tests = evaluate(
    "tests/Mockups.Application.Tests/Mockups.Application.Tests.csproj",
  );
  assert.deepEqual(
    tests.Items.ProjectReference.map((item) =>
      repositoryPath(item.FullPath ?? item.Identity)),
    ["src/Mockups.Application/Mockups.Application.csproj"],
  );
  assert.deepEqual(tests.Items.PackageReference, []);
});

test("Persistence can see Application and Domain but has no UI package capabilities", () => {
  const persistence = evaluate(
    "src/Mockups.Persistence.Sqlite/Mockups.Persistence.Sqlite.csproj",
  );
  assert.deepEqual(
    persistence.Items.ProjectReference
      .map((item) => repositoryPath(item.FullPath ?? item.Identity))
      .sort(),
    [
      "src/Mockups.Application/Mockups.Application.csproj",
      "src/Mockups.Domain/Mockups.Domain.csproj",
    ],
  );
  assert.deepEqual(
    persistence.Items.PackageReference.map((item) => item.Identity).sort(),
    ["Microsoft.Data.Sqlite", "SQLitePCLRaw.bundle_e_sqlite3"],
  );
});

test("Desktop can see UI and Application but cannot compile against SQLite", () => {
  const desktop = evaluate(
    "src/Mockups.Desktop/Mockups.DesktopEditorShell.csproj",
  );
  assert.deepEqual(
    desktop.Items.ProjectReference
      .map((item) => repositoryPath(item.FullPath ?? item.Identity))
      .sort(),
    [
      "src/Mockups.Application/Mockups.Application.csproj",
    ],
  );
  assert.equal(
    desktop.Items.PackageReference
      .some((item) => item.Identity.includes("Sqlite")),
    false,
  );
});

test("the executable Host is the only composition project that sees Desktop and Persistence", () => {
  const host = evaluate(
    "src/Mockups.Desktop.Host/Mockups.Desktop.Host.csproj",
  );
  assert.deepEqual(
    host.Items.ProjectReference
      .map((item) => repositoryPath(item.FullPath ?? item.Identity))
      .sort(),
    [
      "src/Mockups.Application/Mockups.Application.csproj",
      "src/Mockups.Desktop/Mockups.DesktopEditorShell.csproj",
      "src/Mockups.Persistence.Sqlite/Mockups.Persistence.Sqlite.csproj",
    ],
  );
});
