import { rm } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { build } from "esbuild";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const outdir = resolve(repoRoot, "dist", "desktop-preview");
const outfile = resolve(outdir, "renderDesignPreviewHtml.cjs");
const serverOutfile = resolve(outdir, "renderDesignPreviewHtmlServer.cjs");

await rm(outdir, { force: true, recursive: true });

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
