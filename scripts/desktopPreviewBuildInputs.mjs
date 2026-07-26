import { createHash } from "node:crypto";
import { readFile, readdir, writeFile } from "node:fs/promises";
import { relative, resolve, sep } from "node:path";

const fixedInputs = [
  "package.json",
  "package-lock.json",
  "tsconfig.json",
  "scripts/buildDesktopPreview.mjs",
  "scripts/desktopPreviewBuildInputs.mjs",
];

function repositoryPath(repositoryRoot, fullPath) {
  return relative(repositoryRoot, fullPath)
    .split(sep)
    .join("/");
}

async function sourceFiles(repositoryRoot, directory) {
  const entries = await readdir(directory, {
    withFileTypes: true,
  });
  const files = [];
  for (const entry of entries) {
    if (entry.name === "bin" || entry.name === "obj") {
      continue;
    }
    const fullPath = resolve(directory, entry.name);
    if (entry.isDirectory()) {
      files.push(
        ...await sourceFiles(repositoryRoot, fullPath),
      );
      continue;
    }
    const relativePath = repositoryPath(
      repositoryRoot,
      fullPath,
    );
    if (relativePath.endsWith(".ts")
      || relativePath.endsWith(".tsx")
      || (relativePath.startsWith("src/desktop-preview/")
        && relativePath.endsWith(".json"))) {
      files.push(relativePath);
    }
  }
  return files;
}

export async function desktopPreviewSourceInputs(
  repositoryRoot,
) {
  return [
    ...fixedInputs,
    ...await sourceFiles(
      repositoryRoot,
      resolve(repositoryRoot, "src"),
    ),
  ].sort();
}

export async function hashSourceInputs(
  repositoryRoot,
  inputPaths,
) {
  const hash = createHash("sha256");
  for (const inputPath of [...inputPaths].sort()) {
    hash.update(inputPath);
    hash.update("\0");
    hash.update(await readFile(
      resolve(repositoryRoot, inputPath),
    ));
    hash.update("\0");
  }
  return hash.digest("hex");
}

export async function desktopPreviewSourceHash(
  repositoryRoot,
) {
  return hashSourceInputs(
    repositoryRoot,
    await desktopPreviewSourceInputs(repositoryRoot),
  );
}

export async function writeDesktopPreviewSourceStamp(
  stampPath,
  sourceHash,
) {
  await writeFile(
    stampPath,
    `${JSON.stringify(
      {
        schemaVersion: 1,
        sourceHash,
      },
      null,
      2,
    )}\n`,
    "utf8",
  );
}

export async function requireDesktopPreviewSourceStamp(
  stampPath,
  sourceHash,
) {
  let stamp;
  try {
    stamp = JSON.parse(await readFile(stampPath, "utf8"));
  } catch (error) {
    throw new Error(
      `Desktop Preview source stamp is missing or invalid: ${stampPath}`,
      { cause: error },
    );
  }
  if (stamp?.schemaVersion !== 1
    || stamp?.sourceHash !== sourceHash) {
    throw new Error(
      "Desktop Preview artifacts are stale for the current source inputs.",
    );
  }
}
