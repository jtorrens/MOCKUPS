import { execFileSync } from "node:child_process";
import { constants } from "node:fs";
import {
  access,
  chmod,
  cp,
  mkdir,
  open,
  readdir,
  readFile,
  rm,
  symlink,
  writeFile,
} from "node:fs/promises";
import { homedir } from "node:os";
import { basename, dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
export const macDesktopExecutableName = "Mockups.Desktop.Host";
export const macDesktopIconFileName = "mockups-app-icon.icns";

export function macDesktopPlaywrightLayout(
  root,
  appDir,
  revision,
  browserCache = resolve(homedir(), "Library", "Caches", "ms-playwright"),
) {
  const browserDirectoryName = `chromium_headless_shell-${revision}`;
  return {
    packageSources: [
      resolve(root, "node_modules", "playwright"),
      resolve(root, "node_modules", "playwright-core"),
    ],
    packageTarget: resolve(
      appDir,
      "Contents",
      "MacOS",
      "desktop-preview",
      "node_modules",
    ),
    browserSource: resolve(browserCache, browserDirectoryName),
    browserTarget: resolve(
      appDir,
      "Contents",
      "Resources",
      "playwright-browsers",
      browserDirectoryName,
    ),
    browserRoot: resolve(
      appDir,
      "Contents",
      "Resources",
      "playwright-browsers",
    ),
  };
}

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

export function parseMacAppleDevelopmentSigningIdentities(output) {
  return output
    .split(/\r?\n/u)
    .map((line) => line.match(
      /^\s*\d+\)\s+([0-9A-F]{40})\s+"(Apple Development:[^"]+)"\s*$/u,
    ))
    .filter((match) => match !== null)
    .map((match) => ({ fingerprint: match[1], name: match[2] }));
}

export function requireMacAppleDevelopmentSigningIdentity(output) {
  const identities = parseMacAppleDevelopmentSigningIdentities(output);
  if (identities.length !== 1) {
    throw new Error(
      "macOS packaging requires exactly one valid Apple Development "
      + `signing identity, but found ${identities.length}.`,
    );
  }
  return identities[0];
}

export function macDesktopCodeSignArgs(appDir, signingIdentity) {
  return [
    "--force",
    "--sign",
    signingIdentity,
    "--timestamp=none",
    appDir,
  ];
}

const macMachOMagics = new Set([
  0xfeedface,
  0xcefaedfe,
  0xfeedfacf,
  0xcffaedfe,
  0xcafebabe,
  0xbebafeca,
  0xcafebabf,
  0xbfbafeca,
]);

export function isMacMachOHeader(header) {
  return header.length >= 4 && macMachOMagics.has(header.readUInt32BE(0));
}

async function isMacMachOFile(filePath) {
  const handle = await open(filePath, "r");
  try {
    const header = Buffer.alloc(4);
    const { bytesRead } = await handle.read(header, 0, header.length, 0);
    return bytesRead === header.length && isMacMachOHeader(header);
  } finally {
    await handle.close();
  }
}

export async function findMacNestedCodePaths(directory) {
  const paths = [];
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const entryPath = resolve(directory, entry.name);
    if (entry.isDirectory()) {
      paths.push(...await findMacNestedCodePaths(entryPath));
    } else if (
      entry.isFile()
      && await isMacMachOFile(entryPath)
    ) {
      paths.push(entryPath);
    }
  }
  return paths.sort();
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
  const runtimeDir = resolve(resourcesDir, "runtime");
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
  await cp(publishDir, runtimeDir, { recursive: true });
  await cp(resolve(runtimeDir, macDesktopExecutableName), executablePath);
  await rm(resolve(runtimeDir, macDesktopExecutableName));
  for (const entry of await readdir(runtimeDir, { withFileTypes: true })) {
    await symlink(
      `../Resources/runtime/${entry.name}`,
      resolve(macOsDir, entry.name),
    );
  }
  await cp(iconSourcePath, iconBundlePath);

  const playwrightBrowsers = JSON.parse(await readFile(
    resolve(root, "node_modules", "playwright-core", "browsers.json"),
    "utf8",
  ));
  const headlessShell = playwrightBrowsers.browsers?.find(
    (browser) => browser.name === "chromium-headless-shell",
  );
  if (!headlessShell?.revision) {
    throw new Error(
      "The installed Playwright runtime does not declare Chromium Headless Shell.",
    );
  }
  const playwright = macDesktopPlaywrightLayout(
    root,
    appDir,
    headlessShell.revision,
  );
  await mkdir(playwright.packageTarget, { recursive: true });
  for (const source of playwright.packageSources) {
    await access(source, constants.R_OK);
    await cp(
      source,
      resolve(playwright.packageTarget, basename(source)),
      { recursive: true },
    );
  }
  await access(playwright.browserSource, constants.R_OK);
  await mkdir(dirname(playwright.browserTarget), { recursive: true });
  await cp(playwright.browserSource, playwright.browserTarget, {
    recursive: true,
  });

  execFileSync(
    process.execPath,
    [
      "-e",
      "const {chromium}=require('playwright'); chromium.launch({headless:true}).then(async browser=>{await browser.close()}).catch(error=>{console.error(error);process.exit(1)})",
    ],
    {
      cwd: resolve(macOsDir, "desktop-preview"),
      env: {
        ...process.env,
        PLAYWRIGHT_BROWSERS_PATH: playwright.browserRoot,
      },
      stdio: "inherit",
    },
  );

  await chmod(executablePath, 0o755);
  await access(executablePath, constants.X_OK);
  await access(iconBundlePath, constants.R_OK);
  await writeFile(infoPlistPath, macDesktopInfoPlist());

  execFileSync("plutil", ["-lint", infoPlistPath], {
    stdio: "inherit",
  });
  const signingIdentity = requireMacAppleDevelopmentSigningIdentity(
    execFileSync(
      "security",
      ["find-identity", "-v", "-p", "codesigning"],
      { encoding: "utf8" },
    ),
  );
  const nestedCodePaths = await findMacNestedCodePaths(
    resourcesDir,
  );
  for (const nestedCodePath of nestedCodePaths) {
    execFileSync(
      "codesign",
      macDesktopCodeSignArgs(nestedCodePath, signingIdentity.fingerprint),
      { stdio: "inherit" },
    );
  }
  execFileSync(
    "codesign",
    macDesktopCodeSignArgs(appDir, signingIdentity.fingerprint),
    { stdio: "inherit" },
  );
  for (const nestedCodePath of nestedCodePaths) {
    execFileSync(
      "codesign",
      ["--verify", "--strict", nestedCodePath],
      { stdio: "inherit" },
    );
  }
  execFileSync(
    "codesign",
    ["--verify", "--strict", appDir],
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
