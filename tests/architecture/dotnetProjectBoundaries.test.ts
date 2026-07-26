import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { readdirSync } from "node:fs";
import path from "node:path";
import test from "node:test";

type EvaluatedItem = {
  Identity: string;
  FullPath?: string;
  PrivateAssets?: string;
};

type EvaluatedProject = {
  Properties: {
    DisableTransitiveProjectReferences: string;
  };
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
        "src/Mockups.Domain/Mockups.Domain.csproj",
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
      "-getProperty:DisableTransitiveProjectReferences",
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

function resolvedReferenceNames(projectPath: string): Set<string> {
  const output = execFileSync(
    "dotnet",
    [
      "msbuild",
      projectPath,
      "-target:ResolveReferences",
      "-getItem:ReferencePath",
      "-p:SkipDesktopPreviewBuild=true",
    ],
    {
      cwd: repositoryRoot,
      encoding: "utf8",
      maxBuffer: 2 * 1024 * 1024,
    },
  );
  const evaluated = JSON.parse(output) as {
    Items: {
      ReferencePath: Array<{ Filename?: string }>;
    };
  };
  return new Set(
    evaluated.Items.ReferencePath
      .map((item) => item.Filename)
      .filter((name): name is string => Boolean(name)),
  );
}

function assertCannotCompile(
  projectPath: string,
  expectedDiagnostic: RegExp,
) {
  const result = spawnSync(
    "dotnet",
    [
      "build",
      projectPath,
      "--nologo",
      "--verbosity:quiet",
    ],
    {
      cwd: repositoryRoot,
      encoding: "utf8",
    },
  );
  const output = `${result.stdout}\n${result.stderr}`;
  assert.notEqual(
    result.status,
    0,
    `${projectPath} unexpectedly compiled`,
  );
  assert.match(output, expectedDiagnostic);
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
    assert.equal(
      evaluated.Properties.DisableTransitiveProjectReferences,
      "true",
      `${projectPath} permits transitive project compilation`,
    );
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
    for (const packageReference of
      evaluated.Items.PackageReference) {
      assert.ok(
        packageReference.PrivateAssets
          ?.split(";")
          .includes("compile"),
        `${projectPath} exposes compile assets from ${packageReference.Identity}`,
      );
    }
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

test("Desktop explicitly sees UI, Application and Domain but cannot compile against persistence", () => {
  const desktop = evaluate(
    "src/Mockups.Desktop/Mockups.DesktopEditorShell.csproj",
  );
  assert.deepEqual(
    desktop.Items.ProjectReference
      .map((item) => repositoryPath(item.FullPath ?? item.Identity))
      .sort(),
    [
      "src/Mockups.Application/Mockups.Application.csproj",
      "src/Mockups.Domain/Mockups.Domain.csproj",
    ],
  );
  assert.equal(
    desktop.Items.PackageReference
      .some((item) => item.Identity.includes("Sqlite")),
    false,
  );
  const references = resolvedReferenceNames(
    "src/Mockups.Desktop/Mockups.DesktopEditorShell.csproj",
  );
  assert.equal(references.has("Mockups.Domain"), true);
  assert.equal(
    references.has("Mockups.Persistence.Sqlite"),
    false,
  );
  assert.equal(references.has("Microsoft.Data.Sqlite"), false);
});

test("the executable Host composes Desktop and Persistence without inheriting their compile capabilities", () => {
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
  const references = resolvedReferenceNames(
    "src/Mockups.Desktop.Host/Mockups.Desktop.Host.csproj",
  );
  assert.equal(references.has("Mockups.Domain"), false);
  assert.equal(references.has("Microsoft.Data.Sqlite"), false);
  assert.equal(references.has("SQLitePCLRaw.core"), false);
});

test("an Application-only consumer cannot compile against Domain", () => {
  assertCannotCompile(
    "tests/architecture/fixtures/TransitiveDomainLeak/TransitiveDomainLeak.csproj",
    /ForbiddenDependencyProbe\.cs.*(?:CS0246|CS0234)/su,
  );
});

test("a Persistence-only consumer cannot compile against SQLite packages", () => {
  assertCannotCompile(
    "tests/architecture/fixtures/TransitiveSqliteLeak/TransitiveSqliteLeak.csproj",
    /ForbiddenDependencyProbe\.cs.*(?:CS0246|CS0234)/su,
  );
});
