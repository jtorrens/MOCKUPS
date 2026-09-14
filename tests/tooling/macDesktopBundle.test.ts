import assert from "node:assert/strict";
import path from "node:path";
import test from "node:test";

import {
  findMacNestedCodePaths,
  isMacMachOHeader,
  macDesktopCodeSignArgs,
  macDesktopExecutableName,
  macDesktopIconFileName,
  macDesktopInfoPlist,
  macDesktopPlaywrightLayout,
  parseMacAppleDevelopmentSigningIdentities,
  requireMacAppleDevelopmentSigningIdentity,
  verifyMacDesktopBuildIdentity,
} from "../../scripts/packageMacApp.mjs";
import { macDesktopPublishArgs } from "../../scripts/publishMacDesktop.mjs";
import {
  installMacDesktopApp,
  macDesktopInstallationPath,
} from "../../scripts/installMacApp.mjs";
import {
  mkdtemp,
  mkdir,
  readFile,
  readlink,
  rm,
  symlink,
  writeFile,
} from "node:fs/promises";
import { tmpdir } from "node:os";

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

test("macOS bundle receives one final Apple Development signature", () => {
  const appPath = path.join("out", "desktop", "MOCKUPS Editor.app");
  const identity = "CB32BFC6C5EF8722983A3324A05ACF119951FC51";

  assert.deepEqual(macDesktopCodeSignArgs(appPath, identity), [
    "--force",
    "--sign",
    identity,
    "--timestamp=none",
    appPath,
  ]);
});

test("macOS packaging detects only nested Mach-O code", async () => {
  const temporary = await mkdtemp(path.join(tmpdir(), "mockups-signing-test-"));
  const executable = path.join(temporary, "Mockups.Desktop.Host");
  const library = path.join(temporary, "runtime", "libRuntime.dylib");
  const resource = path.join(temporary, "runtime", "icon.svg");
  try {
    await mkdir(path.dirname(library), { recursive: true });
    await writeFile(executable, Buffer.from([0xcf, 0xfa, 0xed, 0xfe]));
    await writeFile(library, Buffer.from([0xca, 0xfe, 0xba, 0xbe]));
    await writeFile(resource, "<svg/>");

    assert.equal(
      isMacMachOHeader(Buffer.from([0xcf, 0xfa, 0xed, 0xfe])),
      true,
    );
    assert.deepEqual(
      await findMacNestedCodePaths(temporary),
      [executable, library].sort(),
    );
  } finally {
    await rm(temporary, { recursive: true, force: true });
  }
});

test("macOS packaging requires one exact Apple Development identity", () => {
  const output = `
  1) CB32BFC6C5EF8722983A3324A05ACF119951FC51 "Apple Development: Developer One (TEAM123456)"
     1 valid identities found
`;

  assert.deepEqual(parseMacAppleDevelopmentSigningIdentities(output), [{
    fingerprint: "CB32BFC6C5EF8722983A3324A05ACF119951FC51",
    name: "Apple Development: Developer One (TEAM123456)",
  }]);
  assert.deepEqual(requireMacAppleDevelopmentSigningIdentity(output), {
    fingerprint: "CB32BFC6C5EF8722983A3324A05ACF119951FC51",
    name: "Apple Development: Developer One (TEAM123456)",
  });
  assert.throws(
    () => requireMacAppleDevelopmentSigningIdentity("0 valid identities found"),
    /exactly one valid Apple Development signing identity/u,
  );
});

test("macOS bundle carries its Playwright runtime and Chromium browser", () => {
  const appPath = path.join(repositoryRoot, "out", "desktop", "MOCKUPS Editor.app");
  const layout = macDesktopPlaywrightLayout(
    repositoryRoot,
    appPath,
    "1228",
    path.join(repositoryRoot, "browser-cache"),
  );

  assert.deepEqual(layout.packageSources, [
    path.join(repositoryRoot, "node_modules", "playwright"),
    path.join(repositoryRoot, "node_modules", "playwright-core"),
  ]);
  assert.equal(
    layout.packageTarget,
    path.join(appPath, "Contents", "MacOS", "desktop-preview", "node_modules"),
  );
  assert.equal(
    layout.browserSource,
    path.join(repositoryRoot, "browser-cache", "chromium_headless_shell-1228"),
  );
  assert.equal(
    layout.browserTarget,
    path.join(
      appPath,
      "Contents",
      "Resources",
      "playwright-browsers",
      "chromium_headless_shell-1228",
    ),
  );
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

test("macOS installation atomically replaces the Applications bundle", async () => {
  const temporary = await mkdtemp(path.join(tmpdir(), "mockups-install-test-"));
  const applications = path.join(temporary, "Applications");
  const source = path.join(
    temporary,
    "out",
    "desktop",
    "MOCKUPS Editor.app",
    "Contents",
    "MacOS",
  );
  const runtime = path.join(
    temporary,
    "out",
    "desktop",
    "MOCKUPS Editor.app",
    "Contents",
    "Resources",
    "runtime",
  );
  const destination = macDesktopInstallationPath(applications);
  try {
    await mkdir(source, { recursive: true });
    await mkdir(runtime, { recursive: true });
    await mkdir(destination, { recursive: true });
    await writeFile(path.join(source, "Mockups.Desktop.Host"), "current");
    await writeFile(path.join(runtime, "Runtime.dll"), "runtime");
    await symlink(
      "../Resources/runtime/Runtime.dll",
      path.join(source, "Runtime.dll"),
    );
    await writeFile(path.join(destination, "obsolete"), "obsolete");

    assert.equal(
      await installMacDesktopApp(temporary, applications),
      destination,
    );
    assert.equal(
      await readFile(
        path.join(destination, "Contents", "MacOS", "Mockups.Desktop.Host"),
        "utf8",
      ),
      "current",
    );
    await assert.rejects(readFile(path.join(destination, "obsolete")));
    assert.equal(
      await readlink(path.join(destination, "Contents", "MacOS", "Runtime.dll")),
      "../Resources/runtime/Runtime.dll",
    );
  } finally {
    await rm(temporary, { recursive: true, force: true });
  }
});
