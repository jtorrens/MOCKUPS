import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import {
  constants,
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  renameSync,
  rmSync,
  statSync,
} from "node:fs";
import { homedir } from "node:os";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const repositoryRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
);

export function workstationProjectRoot(
  platform = process.platform,
  environment = process.env,
  userHome = homedir(),
) {
  if (platform === "darwin") {
    return path.join(userHome, "Library", "Application Support", "MOCKUPS");
  }
  if (platform === "win32") {
    const localAppData = environment.LOCALAPPDATA?.trim();
    return path.join(
      localAppData || path.join(userHome, "AppData", "Local"),
      "MOCKUPS",
    );
  }
  const dataHome = environment.XDG_DATA_HOME?.trim();
  return path.join(dataHome || path.join(userHome, ".local", "share"), "MOCKUPS");
}

export function workstationProjectPaths(
  root = repositoryRoot,
  workstationRoot = workstationProjectRoot(),
) {
  return {
    repositoryDatabase: path.join(root, "data", "mockups.sqlite"),
    workstationDatabase: path.join(workstationRoot, "mockups.sqlite"),
    workstationRoot,
  };
}

function digest(file) {
  return createHash("sha256").update(readFileSync(file)).digest("hex");
}

function requireFile(file, label) {
  if (!existsSync(file)) {
    throw new Error(`${label} does not exist: ${file}`);
  }
}

function validateCurrentDatabase(root, databasePath) {
  const validation = spawnSync(
    "dotnet",
    [
      "run",
      "--project",
      path.join(root, "src", "Mockups.Desktop.Host", "Mockups.Desktop.Host.csproj"),
      "--",
      "--validate-current-database",
      "--source",
      databasePath,
    ],
    { cwd: root, encoding: "utf8", stdio: "inherit" },
  );
  if (validation.status !== 0) {
    throw new Error(`Current database validation failed: ${databasePath}`);
  }
}

function copyDatabaseAtomically(source, destination) {
  const temporary = path.join(
    path.dirname(destination),
    `.${path.basename(destination)}.${process.pid}.tmp`,
  );
  rmSync(temporary, { force: true });
  copyFileSync(source, temporary, constants.COPYFILE_EXCL);
  renameSync(temporary, destination);
}

export function bootstrapWorkstationProject(
  root = repositoryRoot,
  workstationRoot = workstationProjectRoot(),
  validateDatabase = validateCurrentDatabase,
) {
  const paths = workstationProjectPaths(root, workstationRoot);
  requireFile(paths.repositoryDatabase, "Repository database snapshot");
  mkdirSync(paths.workstationRoot, { recursive: true });
  const createdDatabase = !existsSync(paths.workstationDatabase);
  if (createdDatabase) {
    copyFileSync(
      paths.repositoryDatabase,
      paths.workstationDatabase,
      constants.COPYFILE_EXCL,
    );
  }
  if (createdDatabase) {
    validateDatabase(root, paths.workstationDatabase);
    requireDatabaseParity(paths);
  }
  return paths;
}

function requireClosedDatabase(databasePath) {
  if (process.platform === "darwin") {
    const openFiles = spawnSync("lsof", ["-t", "--", databasePath], {
      encoding: "utf8",
    });
    if (openFiles.status === 0 && openFiles.stdout.trim()) {
      throw new Error(
        "The workstation database is open. Close MOCKUPS before creating the repository snapshot.",
      );
    }
    if (openFiles.status !== 0 && openFiles.status !== 1) {
      throw new Error(openFiles.stderr.trim() || "Could not verify that the workstation database is closed.");
    }
  }
  const walPath = `${databasePath}-wal`;
  if (existsSync(walPath) && statSync(walPath).size > 0) {
    throw new Error(
      "The workstation database has an uncheckpointed WAL. Open and close MOCKUPS cleanly before snapshotting.",
    );
  }
}

export function snapshotWorkstationProject(
  root = repositoryRoot,
  workstationRoot = workstationProjectRoot(),
  validateDatabase = validateCurrentDatabase,
) {
  const paths = workstationProjectPaths(root, workstationRoot);
  requireFile(paths.workstationDatabase, "Workstation database");
  requireClosedDatabase(paths.workstationDatabase);
  validateDatabase(root, paths.workstationDatabase);
  mkdirSync(path.dirname(paths.repositoryDatabase), { recursive: true });
  copyDatabaseAtomically(
    paths.workstationDatabase,
    paths.repositoryDatabase,
  );
  validateDatabase(root, paths.repositoryDatabase);
  requireDatabaseParity(paths);
  return paths;
}

export function requireDatabaseParity(
  paths = workstationProjectPaths(),
) {
  requireFile(paths.repositoryDatabase, "Repository database snapshot");
  requireFile(paths.workstationDatabase, "Workstation database");
  const repositoryDigest = digest(paths.repositoryDatabase);
  const workstationDigest = digest(paths.workstationDatabase);
  if (repositoryDigest !== workstationDigest) {
    throw new Error(
      "The workstation database and repository snapshot differ. "
      + "Close MOCKUPS and run 'npm run desktop:db:snapshot' before validation.",
    );
  }
  return workstationDigest;
}

export function checkDatabaseParityIfPresent(
  root = repositoryRoot,
  workstationRoot = workstationProjectRoot(),
) {
  const paths = workstationProjectPaths(root, workstationRoot);
  if (!existsSync(paths.workstationDatabase)) return undefined;
  return requireDatabaseParity(paths);
}

function run(command) {
  if (command === "bootstrap") {
    const paths = bootstrapWorkstationProject();
    console.log(`Workstation Project ready: ${paths.workstationDatabase}`);
    return;
  }
  if (command === "snapshot") {
    const paths = snapshotWorkstationProject();
    console.log(`Repository snapshot updated: ${paths.repositoryDatabase}`);
    return;
  }
  if (command === "check") {
    const digestValue = requireDatabaseParity();
    console.log(`Workstation database matches repository snapshot: ${digestValue.slice(0, 12)}`);
    return;
  }
  if (command === "check-if-present") {
    const digestValue = checkDatabaseParityIfPresent();
    console.log(digestValue
      ? `Workstation database matches repository snapshot: ${digestValue.slice(0, 12)}`
      : "No workstation database exists; parity check is not applicable.");
    return;
  }
  throw new Error(
    "Usage: node scripts/workstationProject.mjs bootstrap|snapshot|check|check-if-present",
  );
}

const executedPath = process.argv[1]
  ? pathToFileURL(path.resolve(process.argv[1])).href
  : "";
if (executedPath === import.meta.url) {
  try {
    run(process.argv[2] ?? "");
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  }
}
