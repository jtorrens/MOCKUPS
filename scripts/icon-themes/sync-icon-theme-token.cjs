#!/usr/bin/env node

/**
 * Search and generate one semantic MOCKUPS icon token across all icon sets.
 *
 * This script is intentionally provider-specific and external to the app UI.
 * MOCKUPS passes the full target set context; the script only searches provider
 * packages, normalizes SVGs, writes staged files, and copies them into the
 * provided final set directories once every target is ready.
 */

const fs = require("fs");
const os = require("os");
const path = require("path");
const { execFileSync } = require("child_process");

const LUCIDE_PACKAGE = "lucide-static";
const MATERIAL_DEFAULT_WEIGHT = 400;
const MATERIAL_STYLES = ["rounded", "outlined", "sharp"];

function parseArgs(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i++) {
    const token = argv[i];
    if (token === "--mode") args.mode = argv[++i];
    else if (token.startsWith("--mode=")) args.mode = token.slice("--mode=".length);
    else if (token === "--query") args.query = argv[++i];
    else if (token.startsWith("--query=")) args.query = token.slice("--query=".length);
    else if (token === "--request") args.request = argv[++i];
    else if (token.startsWith("--request=")) args.request = token.slice("--request=".length);
    else if (token === "--help" || token === "-h") printHelpAndExit();
    else throw new Error(`Unknown argument: ${token}`);
  }
  return args;
}

function printHelpAndExit() {
  console.log(`
Usage:
  node scripts/icon-themes/sync-icon-theme-token.cjs --mode search --query telephone
  node scripts/icon-themes/sync-icon-theme-token.cjs --mode generate --request /tmp/request.json

The generate request is produced by MOCKUPS and includes all target sets.
`);
  process.exit(0);
}

function walk(dir, out = []) {
  if (!fs.existsSync(dir)) return out;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, out);
    else out.push(full);
  }
  return out;
}

function normalizePath(value) {
  return value.replace(/\\/g, "/").toLowerCase();
}

function packageCacheRoot() {
  const dir = path.join(os.tmpdir(), "mockups-icon-provider-cache");
  fs.mkdirSync(dir, { recursive: true });
  return dir;
}

function safeCacheName(packageName) {
  return packageName.replace(/[^a-z0-9._-]+/gi, "_");
}

function ensurePackage(packageName) {
  const cacheRoot = packageCacheRoot();
  const extractedRoot = path.join(cacheRoot, safeCacheName(packageName));
  if (fs.existsSync(extractedRoot)) return extractedRoot;

  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), "mockups-icon-package-"));
  execFileSync(
    "npm",
    ["pack", `${packageName}@latest`, "--pack-destination", tmpDir],
    { stdio: "ignore" },
  );
  const tgz = fs.readdirSync(tmpDir).find((name) => name.endsWith(".tgz"));
  if (!tgz) throw new Error(`No tgz produced for ${packageName}`);
  fs.mkdirSync(extractedRoot, { recursive: true });
  execFileSync("tar", ["-xzf", path.join(tmpDir, tgz), "-C", extractedRoot], {
    stdio: "ignore",
  });
  const packageRoot = path.join(extractedRoot, "package");
  if (!fs.existsSync(packageRoot)) {
    throw new Error(`Extracted package root not found for ${packageName}`);
  }
  return packageRoot;
}

function svgFilesForPackage(packageName) {
  return walk(ensurePackage(packageName)).filter((file) =>
    file.toLowerCase().endsWith(".svg"),
  );
}

function basenameWithoutSvg(file) {
  return path.basename(file).replace(/\.svg$/i, "");
}

function materialBaseSourceName(sourceName) {
  return String(sourceName ?? "")
    .replace(/[_-](round|rounded|outline|outlined|sharp)$/i, "")
    .replace(/[_-](24px|20px|40px|48px)$/i, "");
}

function materialSourceNamesForStyle(sourceName, style) {
  const base = materialBaseSourceName(sourceName);
  const styleAliases =
    style === "rounded"
      ? ["round", "rounded"]
      : style === "outlined"
        ? ["outline", "outlined"]
        : ["sharp"];
  return [
    base,
    ...styleAliases.flatMap((alias) => [
      `${base}_${alias}`,
      `${base}-${alias}`,
    ]),
  ];
}

function compact(value) {
  return String(value ?? "")
    .trim()
    .toLowerCase()
    .replace(/[_\s]+/g, "-");
}

function candidateScore(name, query) {
  const normalizedName = compact(name);
  const normalizedQuery = compact(query);
  if (!normalizedQuery) return 999;
  if (normalizedName === normalizedQuery) return 0;
  if (normalizedName.startsWith(normalizedQuery)) return 1;
  if (normalizedName.includes(normalizedQuery)) return 2;
  const parts = normalizedQuery.split("-").filter(Boolean);
  if (parts.length && parts.every((part) => normalizedName.includes(part))) return 3;
  return 999;
}

function svgDataUrl(svg) {
  return `data:image/svg+xml;base64,${Buffer.from(svg).toString("base64")}`;
}

function previewSvgForCandidate({ provider, file }) {
  try {
    const raw = fs.readFileSync(file, "utf8");
    const normalized =
      provider === "lucide"
        ? makeLucideSvgTintable(raw, 2)
        : makeMaterialSvgTintable(raw);
    assertValidSvg(normalized, path.basename(file));
    return svgDataUrl(normalized);
  } catch {
    return "";
  }
}

function searchPackage({ packageName, provider, query, limit = 40 }) {
  const files = svgFilesForPackage(packageName);
  const byName = new Map();
  for (const file of files) {
    const rawSourceName = basenameWithoutSvg(file);
    const sourceName =
      provider === "material"
        ? materialBaseSourceName(rawSourceName)
        : rawSourceName;
    const score = candidateScore(sourceName, query);
    if (score >= 999) continue;
    const existing = byName.get(sourceName);
    const canonicalBonus =
      provider === "material" && rawSourceName === sourceName ? -0.25 : 0;
    const finalScore = score + canonicalBonus;
    if (!existing || finalScore < existing.score) {
      byName.set(sourceName, {
        provider,
        sourceName,
        file,
        score: finalScore,
      });
    }
  }
  return [...byName.values()]
    .sort((left, right) => left.score - right.score || left.sourceName.localeCompare(right.sourceName))
    .slice(0, limit)
    .map(({ provider: candidateProvider, sourceName, file }) => ({
      provider: candidateProvider,
      sourceName,
      previewUrl: previewSvgForCandidate({
        provider: candidateProvider,
        file,
      }),
    }));
}

function runSearch(query) {
  const lucide = searchPackage({
    packageName: LUCIDE_PACKAGE,
    provider: "lucide",
    query,
  });
  const material = searchPackage({
    packageName: `@material-symbols/svg-${MATERIAL_DEFAULT_WEIGHT}`,
    provider: "material",
    query,
  });
  return { lucide, material };
}

function makeLucideSvgTintable(svg, strokeWidth) {
  let out = svg
    .replace(/stroke="#000000"/gi, 'stroke="currentColor"')
    .replace(/stroke="#000"/gi, 'stroke="currentColor"')
    .replace(/stroke="black"/gi, 'stroke="currentColor"')
    .replace(/fill="#000000"/gi, 'fill="none"')
    .replace(/fill="#000"/gi, 'fill="none"')
    .replace(/fill="black"/gi, 'fill="none"');
  if (/stroke-width="[^"]+"/i.test(out)) {
    out = out.replace(/stroke-width="[^"]+"/gi, `stroke-width="${strokeWidth}"`);
  } else {
    out = out.replace("<svg ", `<svg stroke-width="${strokeWidth}" `);
  }
  return out;
}

function makeMaterialSvgTintable(svg) {
  return svg
    .replace(/fill="#000000"/gi, 'fill="currentColor"')
    .replace(/fill="#000"/gi, 'fill="currentColor"')
    .replace(/fill="black"/gi, 'fill="currentColor"')
    .replace(/stroke="#000000"/gi, 'stroke="currentColor"')
    .replace(/stroke="#000"/gi, 'stroke="currentColor"')
    .replace(/stroke="black"/gi, 'stroke="currentColor"');
}

function findLucideSvg(sourceName) {
  const target = `${sourceName}.svg`.toLowerCase();
  return svgFilesForPackage(LUCIDE_PACKAGE).find(
    (file) => path.basename(file).toLowerCase() === target,
  );
}

function materialCandidateScore(file, sourceName, style) {
  const normalized = normalizePath(file);
  const base = path.basename(file, ".svg").toLowerCase();
  const wantedNames = materialSourceNamesForStyle(sourceName, style)
    .map((name) => name.toLowerCase());
  if (!wantedNames.includes(base)) return -1;
  let score = 0;
  if (base === materialBaseSourceName(sourceName).toLowerCase()) score += 15;
  if (base.endsWith(`_${style}`) || base.endsWith(`-${style}`)) score += 10;
  if (style === "rounded" && (base.endsWith("_round") || base.endsWith("-round"))) score += 10;
  if (style === "outlined" && (base.endsWith("_outline") || base.endsWith("-outline"))) score += 10;
  if (
    normalized.includes(`/${style}/`) ||
    normalized.includes(`-${style}`) ||
    normalized.includes(`_${style}`) ||
    normalized.includes(`/materialsymbols${style}/`)
  ) {
    score += 100;
  }
  for (const otherStyle of MATERIAL_STYLES) {
    if (otherStyle !== style && normalized.includes(otherStyle)) score -= 40;
  }
  return score;
}

function findMaterialSvg({ sourceName, style, weight }) {
  const files = svgFilesForPackage(`@material-symbols/svg-${weight}`);
  const scored = files
    .map((file) => ({
      file,
      score: materialCandidateScore(file, sourceName, style),
    }))
    .filter((entry) => entry.score >= 0)
    .sort((left, right) => right.score - left.score);
  return scored[0]?.file;
}

function assertValidSvg(svg, label) {
  if (!/<svg[\s>]/i.test(svg)) throw new Error(`${label} is not an SVG`);
  if (!/viewBox=/i.test(svg)) throw new Error(`${label} has no viewBox`);
}

function providerForSet(set) {
  const provider = String(set.iconSet?.provider ?? "").toLowerCase();
  if (provider === "lucide" || provider === "material") return provider;
  throw new Error(`Unsupported icon provider for set ${set.name}: ${provider || "(empty)"}`);
}

function runGenerate(requestPath) {
  const request = JSON.parse(fs.readFileSync(requestPath, "utf8"));
  const token = String(request.token ?? "").trim();
  if (!/^[a-z][a-z0-9_]*(?:\.[a-z0-9_]+)*$/.test(token)) {
    throw new Error(`Invalid token: ${token}`);
  }
  const sets = Array.isArray(request.sets) ? request.sets : [];
  if (!sets.length) throw new Error("Generate request has no sets.");
  const selectedSources = request.selectedSources ?? {};
  const stagedRoot = fs.mkdtempSync(path.join(os.tmpdir(), "mockups-icon-token-"));
  const stagedFiles = [];

  for (const set of sets) {
    const provider = providerForSet(set);
    const sourceName = String(selectedSources[provider] ?? "").trim();
    if (!sourceName) {
      throw new Error(`Missing selected ${provider} source for set ${set.name}`);
    }

    let sourceFile = "";
    let svg = "";
    if (provider === "lucide") {
      sourceFile = findLucideSvg(sourceName);
      if (!sourceFile) throw new Error(`Lucide icon not found: ${sourceName}`);
      svg = makeLucideSvgTintable(
        fs.readFileSync(sourceFile, "utf8"),
        Number(set.iconSet?.stroke ?? 2),
      );
    } else {
      const style = String(set.iconSet?.style ?? "rounded");
      const weight = Number(set.iconSet?.weight ?? MATERIAL_DEFAULT_WEIGHT);
      sourceFile = findMaterialSvg({ sourceName, style, weight });
      if (!sourceFile) {
        throw new Error(`Material icon not found: ${sourceName} (${style}, ${weight})`);
      }
      svg = makeMaterialSvgTintable(fs.readFileSync(sourceFile, "utf8"));
    }

    assertValidSvg(svg, `${set.name}/${token}.svg`);
    const stagedDir = path.join(stagedRoot, set.name);
    fs.mkdirSync(stagedDir, { recursive: true });
    const stagedPath = path.join(stagedDir, `${token}.svg`);
    fs.writeFileSync(stagedPath, svg, "utf8");
    stagedFiles.push({
      provider,
      setName: set.name,
      sourceName,
      sourceFile,
      stagedPath,
      targetPath: path.join(set.path, `${token}.svg`),
    });
  }

  for (const file of stagedFiles) {
    fs.mkdirSync(path.dirname(file.targetPath), { recursive: true });
    fs.copyFileSync(file.stagedPath, file.targetPath);
  }

  return {
    token,
    writtenFileCount: stagedFiles.length,
    sets: stagedFiles.map(({ setName, provider, sourceName }) => ({
      setName,
      provider,
      sourceName,
    })),
  };
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  let result;
  if (args.mode === "search") {
    result = runSearch(String(args.query ?? ""));
  } else if (args.mode === "generate") {
    result = runGenerate(String(args.request ?? ""));
  } else {
    throw new Error("Expected --mode search or --mode generate");
  }
  process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
}

try {
  main();
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
}
