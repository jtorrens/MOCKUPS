import assert from "node:assert/strict";
import path from "node:path";
import test from "node:test";

import {
  excludeUnstagedWorkstationDatabase,
  planScopedValidation,
} from "../../scripts/runScopedValidation.js";

const repositoryRoot = path.resolve(".");

test("build graph changes cannot be consumed by a source or test directory first", () => {
  for (const file of [
    "src/Mockups.Application/Mockups.Application.csproj",
    "src/Mockups.Desktop/Mockups.DesktopEditorShell.csproj",
    "src/Mockups.Persistence.Sqlite.Core/Mockups.Persistence.Sqlite.Core.csproj",
    "tests/Mockups.Desktop.Tests/Mockups.DesktopEditorShell.AnimationTests.csproj",
    "Directory.Build.props",
    "Directory.Build.targets",
  ]) {
    const ids = planScopedValidation(repositoryRoot, [file]).map(step => step.id);
    assert.ok(ids.includes("desktop-compile"), file);
    assert.ok(ids.includes("architecture"), file);
  }
});

test("shared Application contracts select their real Desktop consumers", () => {
  for (const file of [
    "src/Mockups.Application/RuntimePreviewDocumentContract.cs",
    "src/Mockups.Application/StructuredCollectionMutation.cs",
    "src/Mockups.Application/EditorWorkspace.cs",
  ]) {
    const ids = planScopedValidation(repositoryRoot, [file]).map(step => step.id);
    for (const id of ["application", "desktop-core", "desktop-ui"]) {
      assert.ok(ids.includes(id), `${file}: missing ${id}`);
    }
  }
});

test("shared editor controllers and changed Desktop tests retain isolated UI coverage", () => {
  for (const file of [
    "src/Mockups.Desktop/EditorShell/EditorContentPreparationService.cs",
    "src/Mockups.Desktop/EditorShell/ProductionPreviewSessionDataSource.cs",
    "tests/Mockups.Desktop.Tests/Program.cs",
    "tests/Mockups.Desktop.Tests/webPreviewBrowserChecks.mjs",
  ]) {
    const ids = planScopedValidation(repositoryRoot, [file]).map(step => step.id);
    assert.ok(ids.includes("desktop-core"), file);
    assert.ok(ids.includes("desktop-ui"), file);
  }
});

test("an unclassified path stops instead of selecting the full repository suite", () => {
  assert.throws(
    () => planScopedValidation(
      repositoryRoot,
      ["new-owner/not-classified.behavior"],
    ),
    /no declared validation owner[\s\S]*new-owner\/not-classified\.behavior[\s\S]*never selected implicitly/u,
  );
});

test("retired archive changes select only their absence contract", () => {
  const plan = planScopedValidation(
    repositoryRoot,
    ["archive/react-legacy/src/retired.ts"],
  );
  assert.deepEqual(
    plan.map((step) => step.id),
    ["retired", "diff-check"],
  );
});

test("every retired cleanup artifact selects only its absence contract", () => {
  for (const file of [
    "scripts/migratePaletteColorReferencesToIds.mjs",
    "scripts/icon-themes/download-lucide-theme.cjs",
    "scripts/icon-themes/material-rounded-200/editor_audio.svg",
    "scripts/icon-themes/_licenses/material-symbols-svg-200-apache-2.0.txt",
    "assets/icons/components/Render Presets.svg",
    "docs/WINDOWS_PC_TEST_HANDOFF.md",
  ]) {
    const plan = planScopedValidation(repositoryRoot, [file]);
    assert.deepEqual(
      plan.map((step) => step.id),
      ["retired", "diff-check"],
      file,
    );
  }
});

test("a focused SVG service change selects only its direct regressions", () => {
  const plan = planScopedValidation(
    repositoryRoot,
    ["src/Mockups.Desktop/Common/SvgReplacementService.cs"],
  );
  const ids = plan.map((step) => step.id);
  assert.deepEqual(ids, [
    "application:svg-fill",
    "desktop:svg-fill-preview",
    "desktop-compile",
    "diff-check",
  ]);
  assert.equal(
    plan.some((step) =>
      step.id === "desktop-core"
      || step.id === "desktop-exhaustive"
      || step.id === "preview-all"),
    false,
  );
});

test("a concrete Preview owner stays focused on that manifest owner", () => {
  const plan = planScopedValidation(
    repositoryRoot,
    ["src/desktop-preview/labelComponentResolver.ts"],
  );
  const ids = plan.map((step) => step.id);
  assert.equal(ids.includes("owner:component:label"), true);
  assert.equal(ids.includes("desktop-exhaustive"), false);
  assert.equal(ids.includes("preview-all"), false);
});

test("a shared Preview renderer selects exhaustive manifest coverage", () => {
  const plan = planScopedValidation(
    repositoryRoot,
    ["src/desktop-preview/componentRenderableCommon.ts"],
  );
  const ids = plan.map((step) => step.id);
  assert.equal(ids.includes("preview-all"), true);
  assert.equal(ids.includes("desktop-exhaustive"), true);
});

test("a generated fill asset selects SVG checks without unrelated owners", () => {
  const plan = planScopedValidation(
    repositoryRoot,
    ["assets/system/system_icons/media_play_fill.svg"],
  );
  const ids = plan.map((step) => step.id);
  assert.equal(ids.includes("desktop:icon-theme-svg"), true);
  assert.equal(ids.includes("desktop:svg-fill-preview"), true);
  assert.equal(ids.includes("database"), true);
  assert.equal(ids.includes("desktop-exhaustive"), false);
  assert.equal(ids.includes("preview-all"), false);
});

test("broad Preview coverage subsumes changed focused Preview tests", () => {
  const plan = planScopedValidation(
    repositoryRoot,
    [
      "data/mockups.sqlite",
      "tests/animation/listItemComponent.test.ts",
    ],
  );
  const ids = plan.map((step) => step.id);
  assert.equal(ids.includes("preview-all"), true);
  assert.equal(
    ids.some((id) => id.startsWith("preview:")),
    false,
  );
});

test("a shared Preview test fixture selects broad Preview coverage", () => {
  const plan = planScopedValidation(
    repositoryRoot,
    ["tests/animation/committedComponentFixture.ts"],
  );
  const ids = plan.map((step) => step.id);
  assert.equal(ids.includes("preview-all"), true);
  assert.equal(ids.includes("desktop-exhaustive"), true);
  assert.equal(ids.includes("typecheck"), true);
});

test("automatic discovery ignores only an unstaged workstation database", () => {
  const files = [
    "data/mockups.sqlite",
    "src/Mockups.Desktop/MainWindow.axaml.cs",
  ];
  assert.deepEqual(
    excludeUnstagedWorkstationDatabase(files, new Set()),
    ["src/Mockups.Desktop/MainWindow.axaml.cs"],
  );
  assert.deepEqual(
    excludeUnstagedWorkstationDatabase(
      files,
      new Set(["data/mockups.sqlite"]),
    ),
    files,
  );
});
