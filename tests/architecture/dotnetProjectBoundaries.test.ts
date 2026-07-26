import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { readFileSync, readdirSync } from "node:fs";
import path from "node:path";
import test from "node:test";

type EvaluatedItem = {
  Identity: string;
  FullPath?: string;
  PrivateAssets?: string;
  DefiningProjectFullPath?: string;
  IsImplicitlyDefined?: string;
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

type EvaluatedPhysicalChannels = {
  Items: {
    Reference: EvaluatedItem[];
    Analyzer: EvaluatedItem[];
    Compile: EvaluatedItem[];
    EmbeddedResource: EvaluatedItem[];
    Content: EvaluatedItem[];
    InternalsVisibleTo: EvaluatedItem[];
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

const expectedFriends = new Map([
  [
    "src/Mockups.Application/Mockups.Application.csproj",
    [
      "Mockups.Application.Tests",
      "Mockups.DesktopEditorShell.AnimationTests",
      "Mockups.Persistence.Sqlite",
    ],
  ],
  [
    "src/Mockups.Desktop/Mockups.DesktopEditorShell.csproj",
    ["Mockups.Desktop.Host"],
  ],
  [
    "src/Mockups.Desktop.Host/Mockups.Desktop.Host.csproj",
    ["Mockups.DesktopEditorShell.AnimationTests"],
  ],
  ["src/Mockups.Domain/Mockups.Domain.csproj", []],
  [
    "src/Mockups.Persistence.Sqlite/Mockups.Persistence.Sqlite.csproj",
    ["Mockups.DesktopEditorShell.AnimationTests"],
  ],
]);

const expectedExternalResources = new Map([
  [
    "src/Mockups.Application/Mockups.Application.csproj",
    ["src/desktop-preview/desktopPreviewManifest.json"],
  ],
  ["src/Mockups.Desktop/Mockups.DesktopEditorShell.csproj", []],
  ["src/Mockups.Desktop.Host/Mockups.Desktop.Host.csproj", []],
  ["src/Mockups.Domain/Mockups.Domain.csproj", []],
  ["src/Mockups.Persistence.Sqlite/Mockups.Persistence.Sqlite.csproj", []],
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

function evaluatePhysicalChannels(
  projectPath: string,
): EvaluatedPhysicalChannels {
  const output = execFileSync(
    "dotnet",
    [
      "msbuild",
      projectPath,
      "-getItem:Reference",
      "-getItem:Analyzer",
      "-getItem:Compile",
      "-getItem:EmbeddedResource",
      "-getItem:Content",
      "-getItem:InternalsVisibleTo",
      "-p:SkipDesktopPreviewBuild=true",
    ],
    {
      cwd: repositoryRoot,
      encoding: "utf8",
      maxBuffer: 4 * 1024 * 1024,
    },
  );
  return JSON.parse(output) as EvaluatedPhysicalChannels;
}

function normalizedFullPath(item: EvaluatedItem): string {
  return path.resolve(item.FullPath ?? item.Identity);
}

function isInside(parent: string, candidate: string): boolean {
  const relative = path.relative(parent, candidate);
  return relative === ""
    || (!relative.startsWith(`..${path.sep}`) && relative !== ".."
      && !path.isAbsolute(relative));
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

test("production projects expose only allowlisted physical compilation channels", () => {
  for (const projectPath of expectedProjects.keys()) {
    const absoluteProjectPath = path.resolve(projectPath);
    const projectDirectory = path.dirname(absoluteProjectPath);
    const source = readFileSync(absoluteProjectPath, "utf8");
    assert.match(
      source,
      /^\uFEFF?<Project Sdk="Microsoft\.NET\.Sdk">/,
      `${projectPath} uses an unapproved SDK`,
    );
    assert.doesNotMatch(
      source,
      /<(?:Import|UsingTask)\b/,
      `${projectPath} declares an unapproved MSBuild extension`,
    );

    const channels = evaluatePhysicalChannels(projectPath).Items;
    assert.deepEqual(
      channels.Reference,
      [],
      `${projectPath} declares an assembly Reference outside ProjectReference and PackageReference`,
    );
    for (const analyzer of channels.Analyzer) {
      assert.equal(
        analyzer.IsImplicitlyDefined,
        "true",
        `${projectPath} declares analyzer ${analyzer.Identity}`,
      );
    }
    for (const compile of channels.Compile) {
      assert.ok(
        isInside(projectDirectory, normalizedFullPath(compile)),
        `${projectPath} compiles external source ${compile.Identity}`,
      );
    }

    const externalResources = channels.EmbeddedResource
      .map(normalizedFullPath)
      .filter((candidate) => !isInside(projectDirectory, candidate))
      .map(repositoryPath)
      .sort();
    assert.deepEqual(
      externalResources,
      [...(expectedExternalResources.get(projectPath) ?? [])].sort(),
      `${projectPath} embeds an undeclared external resource`,
    );

    const friends = channels.InternalsVisibleTo
      .map((item) => item.Identity)
      .sort();
    assert.deepEqual(
      friends,
      [...(expectedFriends.get(projectPath) ?? [])].sort(),
      `${projectPath} exposes internals to an undeclared assembly`,
    );

    if (projectPath !== "src/Mockups.Desktop.Host/Mockups.Desktop.Host.csproj") {
      const externalContent = channels.Content
        .map(normalizedFullPath)
        .filter((candidate) => !isInside(projectDirectory, candidate));
      assert.deepEqual(
        externalContent,
        [],
        `${projectPath} links undeclared external Content`,
      );
      continue;
    }

    for (const content of channels.Content) {
      const candidate = normalizedFullPath(content);
      if (isInside(projectDirectory, candidate)) continue;
      const relative = repositoryPath(candidate);
      assert.ok(
        relative === "scripts/icon-themes/sync-icon-theme-token.cjs"
          || relative.startsWith("dist/desktop-preview/")
          || relative.startsWith("assets/system/system_icons/"),
        `Host links undeclared external Content ${relative}`,
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

test("a SQLite session cannot compile as a universal application store", () => {
  assertCannotCompile(
    "tests/architecture/fixtures/UniversalSqliteCapabilityLeak/UniversalSqliteCapabilityLeak.csproj",
    /ForbiddenDependencyProbe\.cs.*CS1061/su,
  );
});
