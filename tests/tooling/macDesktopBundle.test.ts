import assert from "node:assert/strict";
import path from "node:path";
import test from "node:test";

import {
  macDesktopCodeSignArgs,
  macDesktopExecutableName,
  macDesktopIconFileName,
  macDesktopInfoPlist,
} from "../../scripts/packageMacApp.mjs";

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
