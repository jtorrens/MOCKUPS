import { execFileSync } from "node:child_process";
import { constants } from "node:fs";
import { access, chmod, cp, mkdir, rm, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
export const macDesktopExecutableName = "Mockups.Desktop.Host";
export const macDesktopIconFileName = "mockups-app-icon.icns";

export function macDesktopInfoPlist(
  executableName = macDesktopExecutableName,
  iconFileName = macDesktopIconFileName,
) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleDisplayName</key>
  <string>MOCKUPS Editor</string>
  <key>CFBundleExecutable</key>
  <string>${executableName}</string>
  <key>CFBundleIdentifier</key>
  <string>com.mockups.desktop-editor</string>
  <key>CFBundleIconFile</key>
  <string>${iconFileName}</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>MOCKUPS Editor</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>0.1.0</string>
  <key>CFBundleVersion</key>
  <string>0.1.0</string>
  <key>LSMinimumSystemVersion</key>
  <string>13.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
`;
}

export function macDesktopCodeSignArgs(appDir) {
  return [
    "--force",
    "--deep",
    "--sign",
    "-",
    "--timestamp=none",
    appDir,
  ];
}

export function verifyMacDesktopBuildIdentity(expected, actual) {
  const expectedCommit = expected.trim();
  const actualCommit = actual.trim();
  if (!/^[0-9a-f]{8}$/u.test(expectedCommit)) {
    throw new Error(`Expected build commit '${expectedCommit}' is invalid.`);
  }
  if (actualCommit !== expectedCommit) {
    throw new Error(
      `Published Desktop build '${actualCommit}' does not match HEAD '${expectedCommit}'.`,
    );
  }
  return actualCommit;
}

export async function packageMacDesktopApp(root = repoRoot) {
  const publishDir = resolve(root, "out", "desktop", "osx-arm64");
  const appDir = resolve(root, "out", "desktop", "MOCKUPS Editor.app");
  const contentsDir = resolve(appDir, "Contents");
  const macOsDir = resolve(contentsDir, "MacOS");
  const resourcesDir = resolve(contentsDir, "Resources");
  const infoPlistPath = resolve(contentsDir, "Info.plist");
  const executablePath = resolve(macOsDir, macDesktopExecutableName);
  const publishedExecutablePath = resolve(
    publishDir,
    macDesktopExecutableName,
  );
  const iconSourcePath = resolve(
    root,
    "assets",
    "system",
    "application",
    macDesktopIconFileName,
  );
  const iconBundlePath = resolve(resourcesDir, macDesktopIconFileName);

  const expectedCommit = execFileSync(
    "git",
    ["rev-parse", "--short=8", "HEAD"],
    { cwd: root, encoding: "utf8" },
  );
  const actualCommit = execFileSync(
    publishedExecutablePath,
    ["--build-identity"],
    { cwd: root, encoding: "utf8" },
  );
  const verifiedCommit = verifyMacDesktopBuildIdentity(
    expectedCommit,
    actualCommit,
  );

  await rm(appDir, { force: true, recursive: true });
  await mkdir(macOsDir, { recursive: true });
  await mkdir(resourcesDir, { recursive: true });
  await cp(publishDir, macOsDir, { recursive: true });
  await cp(iconSourcePath, iconBundlePath);
  await chmod(executablePath, 0o755);
  await access(executablePath, constants.X_OK);
  await access(iconBundlePath, constants.R_OK);
  await writeFile(infoPlistPath, macDesktopInfoPlist());

  execFileSync("plutil", ["-lint", infoPlistPath], {
    stdio: "inherit",
  });
  execFileSync(
    "codesign",
    macDesktopCodeSignArgs(appDir),
    { stdio: "inherit" },
  );
  execFileSync(
    "codesign",
    ["--verify", "--deep", "--strict", appDir],
    { stdio: "inherit" },
  );

  console.log(`Created and verified ${appDir} at ${verifiedCommit}`);
  return appDir;
}

const executedPath = process.argv[1]
  ? pathToFileURL(resolve(process.argv[1])).href
  : "";
if (executedPath === import.meta.url) {
  await packageMacDesktopApp();
}
