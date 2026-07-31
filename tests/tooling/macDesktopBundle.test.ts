import assert from "node:assert/strict";
import path from "node:path";
import test from "node:test";

import {
  macDesktopCodeSignArgs,
  macDesktopExecutableName,
  macDesktopIconFileName,
  macDesktopInfoPlist,
  verifyMacDesktopBuildIdentity,
} from "../../scripts/packageMacApp.mjs";
import { macDesktopPublishArgs } from "../../scripts/publishMacDesktop.mjs";

const repositoryRoot = process.cwd();

test("macOS bundle launches the executable Desktop Host", () => {
  const plist = macDesktopInfoPlist();

  assert.equal(macDesktopExecutableName, "Mockups.Desktop.Host");
  assert.match(
    plist,
    /<key>CFBundleExecutable<\/key>\s*<string>Mockups\.Desktop\.Host<\/string>/u,
  );
  assert.doesNotMatch(plist, /Mockups\.DesktopEditorShell/u);
});

test("macOS bundle declares the provisional MOCKUPS application icon", () => {
  const plist = macDesktopInfoPlist();

  assert.equal(macDesktopIconFileName, "mockups-app-icon.icns");
  assert.match(
    plist,
    /<key>CFBundleIconFile<\/key>\s*<string>mockups-app-icon\.icns<\/string>/u,
  );
});

test("macOS bundle receives one final deep ad-hoc signature", () => {
  const appPath = path.join("out", "desktop", "MOCKUPS Editor.app");

  assert.deepEqual(macDesktopCodeSignArgs(appPath), [
    "--force",
    "--deep",
    "--sign",
    "-",
    "--timestamp=none",
    appPath,
  ]);
});

test("macOS packaging rejects a published build from another commit", () => {
  assert.equal(
    verifyMacDesktopBuildIdentity("d9b96d97\n", "d9b96d97\n"),
    "d9b96d97",
  );
  assert.throws(
    () => verifyMacDesktopBuildIdentity("d9b96d97", "41d04021"),
    /does not match HEAD/u,
  );
});

test("macOS publication rebuilds the exact self-contained Release target", () => {
  assert.deepEqual(macDesktopPublishArgs(repositoryRoot), [
    "publish",
    path.join(
      repositoryRoot,
      "src",
      "Mockups.Desktop.Host",
      "Mockups.Desktop.Host.csproj",
    ),
    "-c",
    "Release",
    "-r",
    "osx-arm64",
    "--self-contained",
    "true",
    "-o",
    path.join(repositoryRoot, "out", "desktop", "osx-arm64"),
  ]);
});
