import { createHash } from "node:crypto";
import { spawnSync } from "node:child_process";
import { readFile, readdir, rm, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { build } from "esbuild";
import {
  desktopPreviewSourceHash,
  requireDesktopPreviewSourceStamp,
  writeDesktopPreviewSourceStamp,
} from "./desktopPreviewBuildInputs.mjs";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const outdir = resolve(repoRoot, "dist", "desktop-preview");
const sourceStampPath = resolve(
  repoRoot,
  "dist",
  "desktop-preview.source.json",
);
const outfile = resolve(outdir, "renderDesignPreviewHtml.cjs");
const serverOutfile = resolve(outdir, "renderDesignPreviewHtmlServer.cjs");
const rasterServerOutfile = resolve(outdir, "renderPreviewRasterServer.cjs");
const artifactsOnly = process.argv.includes("--artifacts-only");
const manifestOnly = process.argv.includes("--manifest-only");

if (artifactsOnly && manifestOnly) {
  throw new Error("Desktop Preview build modes are mutually exclusive.");
}

const sourceHash = await desktopPreviewSourceHash(repoRoot);

if (!manifestOnly) {
  await rm(outdir, { force: true, recursive: true });
  await rm(sourceStampPath, { force: true });

  await build({
    entryPoints: [resolve(repoRoot, "src", "desktop-preview", "renderDesignPreviewHtml.tsx")],
    outfile,
    bundle: true,
    platform: "node",
    format: "cjs",
    target: "node20",
    sourcemap: true,
    legalComments: "none",
    logLevel: "info",
  });

  await build({
    entryPoints: [resolve(repoRoot, "src", "desktop-preview", "renderPreviewRasterServer.ts")],
    outfile: rasterServerOutfile,
    bundle: true,
    packages: "external",
    platform: "node",
    format: "cjs",
    target: "node20",
    sourcemap: true,
    legalComments: "none",
    logLevel: "info",
  });

  await build({
    entryPoints: [resolve(repoRoot, "src", "desktop-preview", "renderDesignPreviewHtmlServer.ts")],
    outfile: serverOutfile,
    bundle: true,
    platform: "node",
    format: "cjs",
    target: "node20",
    sourcemap: true,
    legalComments: "none",
    logLevel: "info",
  });

  const sourceHashAfterBuild =
    await desktopPreviewSourceHash(repoRoot);
  if (sourceHashAfterBuild !== sourceHash) {
    throw new Error(
      "Desktop Preview source inputs changed while the artifacts were being built.",
    );
  }
  await writeDesktopPreviewSourceStamp(
    sourceStampPath,
    sourceHash,
  );
} else {
  await requireDesktopPreviewSourceStamp(
    sourceStampPath,
    sourceHash,
  );
}

if (!artifactsOnly) {
  const artifactNames = (await readdir(outdir))
    .filter((name) => name !== "manifest.json")
    .sort();
  const artifacts = {};
  for (const artifactName of artifactNames) {
    const contents = await readFile(resolve(outdir, artifactName));
    artifacts[artifactName] = createHash("sha256").update(contents).digest("hex");
  }
  const bundleHash = createHash("sha256")
    .update(
      Object.entries(artifacts)
        .map(([name, hash]) => `${name}:${hash}\n`)
        .join(""),
    )
    .digest("hex");
  const commitResult = spawnSync("git", ["rev-parse", "HEAD"], {
    cwd: repoRoot,
    encoding: "utf8",
  });
  if (commitResult.status !== 0) {
    throw new Error(
      `Could not identify the source commit for the Desktop Preview bundle: ${
        commitResult.stderr || commitResult.stdout
      }`,
    );
  }
  await writeFile(
    resolve(outdir, "manifest.json"),
    `${JSON.stringify(
      {
        schemaVersion: 2,
        commit: commitResult.stdout.trim(),
        builtAt: new Date().toISOString(),
        sourceHash,
        bundleHash,
        artifacts,
      },
      null,
      2,
    )}\n`,
    "utf8",
  );
}
