import { cp, mkdir, rename, rm } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const repositoryRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
);

export const macDesktopApplicationName = "MOCKUPS Editor.app";

export function macDesktopInstallationPath(
  applicationsDirectory = "/Applications",
) {
  return path.join(applicationsDirectory, macDesktopApplicationName);
}

export async function installMacDesktopApp(
  root = repositoryRoot,
  applicationsDirectory = "/Applications",
) {
  const source = path.join(root, "out", "desktop", macDesktopApplicationName);
  const destination = macDesktopInstallationPath(applicationsDirectory);
  const staging = path.join(
    applicationsDirectory,
    `.MOCKUPS Editor.app.install-${process.pid}`,
  );
  const backup = path.join(
    applicationsDirectory,
    `.MOCKUPS Editor.app.backup-${process.pid}`,
  );
  await mkdir(applicationsDirectory, { recursive: true });
  await rm(staging, { recursive: true, force: true });
  await rm(backup, { recursive: true, force: true });
  await cp(source, staging, { recursive: true, force: false, errorOnExist: true });
  let replaced = false;
  try {
    await rename(destination, backup).then(
      () => { replaced = true; },
      (error) => {
        if (error?.code !== "ENOENT") throw error;
      },
    );
    await rename(staging, destination);
    await rm(backup, { recursive: true, force: true });
  } catch (error) {
    await rm(staging, { recursive: true, force: true });
    if (replaced) await rename(backup, destination);
    throw error;
  }
  console.log(`Installed ${destination}`);
  return destination;
}

const executedPath = process.argv[1]
  ? pathToFileURL(path.resolve(process.argv[1])).href
  : "";
if (executedPath === import.meta.url) {
  await installMacDesktopApp();
}
