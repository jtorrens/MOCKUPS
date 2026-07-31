import { execFileSync } from "node:child_process";
import { rm } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

export function macDesktopPublishArgs(root = repoRoot) {
  return [
    "publish",
    resolve(root, "src", "Mockups.Desktop.Host", "Mockups.Desktop.Host.csproj"),
    "-c",
    "Release",
    "-r",
    "osx-arm64",
    "--self-contained",
    "true",
    "-o",
    resolve(root, "out", "desktop", "osx-arm64"),
  ];
}

export async function publishMacDesktop(root = repoRoot) {
  const publishDirectory = resolve(root, "out", "desktop", "osx-arm64");
  await rm(publishDirectory, { force: true, recursive: true });
  execFileSync("dotnet", macDesktopPublishArgs(root), {
    cwd: root,
    stdio: "inherit",
  });
  return publishDirectory;
}

const executedPath = process.argv[1]
  ? pathToFileURL(resolve(process.argv[1])).href
  : "";
if (executedPath === import.meta.url) {
  await publishMacDesktop();
}
