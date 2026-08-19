import assert from "node:assert/strict";
import path from "node:path";
import test from "node:test";

import {
  excludeUnstagedWorkstationDatabase,
  planScopedValidation,
} from "../../scripts/runScopedValidation.js";

const repositoryRoot = path.resolve(".");

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
