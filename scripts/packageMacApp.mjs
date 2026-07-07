import { chmod, cp, mkdir, rm, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const publishDir = resolve(repoRoot, "out", "desktop", "osx-arm64");
const appDir = resolve(repoRoot, "out", "desktop", "MOCKUPS Editor.app");
const contentsDir = resolve(appDir, "Contents");
const macOsDir = resolve(contentsDir, "MacOS");
const executableName = "Mockups.DesktopEditorShell";

await rm(appDir, { force: true, recursive: true });
await mkdir(macOsDir, { recursive: true });
await cp(publishDir, macOsDir, { recursive: true });
await chmod(resolve(macOsDir, executableName), 0o755);

await writeFile(
  resolve(contentsDir, "Info.plist"),
  `<?xml version="1.0" encoding="UTF-8"?>
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
`,
);

console.log(`Created ${appDir}`);
