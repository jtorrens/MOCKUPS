import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import path from "node:path";
import test from "node:test";

import { macDesktopLaunchSpec } from "../../scripts/launchDesktopMac.js";

const repositoryRoot = process.cwd();

test("macOS development launch wakes the display only for startup", () => {
  const spec = macDesktopLaunchSpec("development", repositoryRoot);

  assert.equal(spec.command, "caffeinate");
  assert.deepEqual(spec.args.slice(0, 3), ["-du", "-t", "10"]);
  assert.deepEqual(spec.args.slice(3), ["npm", "run", "desktop"]);
  assert.equal(spec.artifactPath, undefined);
});

test("macOS packaged launch waits on the exact app bundle", () => {
  const spec = macDesktopLaunchSpec("packaged", repositoryRoot);
  const appPath = path.join(
    repositoryRoot,
    "out",
    "desktop",
    "MOCKUPS Editor.app",
  );

  assert.deepEqual(spec.args, [
    "-du",
    "-t",
    "10",
    "open",
    "-W",
    appPath,
  ]);
  assert.equal(spec.artifactPath, appPath);
});

test("display wake policy stays outside the Avalonia application", () => {
  const program = readFileSync(
    path.join(
      repositoryRoot,
      "spikes",
      "desktop-editor-shell",
      "Program.cs",
    ),
    "utf8",
  );

  assert.doesNotMatch(program, /caffeinate|PreventUserIdleDisplaySleep|IOPMAssertion/u);
});
