#!/usr/bin/env node

/**
 * add-editor-material-icons-prompt-weight.cjs
 *
 * Añade los iconos de editor a UN set Material dentro del directorio actual.
 *
 * Regla de ruta:
 *   - El icon themes root es SIEMPRE process.cwd().
 *   - El script crea el set dentro de esa carpeta.
 *   - No acepta rutas como argumento para evitar confusiones.
 *
 * Uso interactivo:
 *   cd /ruta/a/icon-themes
 *   node add-editor-material-icons-prompt-weight.cjs
 *
 * Uso con argumentos:
 *   node add-editor-material-icons-prompt-weight.cjs --style rounded --weight 600
 *   node add-editor-material-icons-prompt-weight.cjs --style outlined --weight 400
 *   node add-editor-material-icons-prompt-weight.cjs --style rounded --weight 500 --out material-rounded-500
 *   node add-editor-material-icons-prompt-weight.cjs --dry-run
 *   node add-editor-material-icons-prompt-weight.cjs --overwrite
 */

const fs = require("fs");
const path = require("path");
const os = require("os");
const readline = require("readline");
const { execFileSync } = require("child_process");

const VERSION = "add-editor-material-icons-prompt-weight.cjs 2026-07-01.prompt-weight.1";

const VALID_STYLES = new Set(["rounded", "outlined", "sharp"]);
const VALID_WEIGHTS = new Set(["100", "200", "300", "400", "500", "600", "700"]);

const ICONS = [
  { token: "system_duplicate", candidates: ["content_copy", "file_copy", "copy_all"] },

  { token: "editor_general", candidates: ["dashboard", "category", "apps"] },
  { token: "editor_style", candidates: ["style", "palette", "format_paint"] },
  { token: "editor_behavior", candidates: ["settings", "tune", "manufacturing"] },
  { token: "editor_content", candidates: ["article", "notes", "subject"] },
  { token: "editor_design", candidates: ["design_services", "draw", "architecture"] },
  { token: "editor_layout", candidates: ["view_quilt", "dashboard_customize", "grid_view"] },
  { token: "editor_header", candidates: ["vertical_align_top", "table_rows", "web_asset"] },
  { token: "editor_messages", candidates: ["forum", "chat", "sms"] },
  { token: "editor_bubble", candidates: ["chat_bubble", "mode_comment"] },
  { token: "editor_avatar", candidates: ["account_circle", "person"] },
  { token: "editor_label", candidates: ["label", "sell", "title"] },
  { token: "editor_media", candidates: ["perm_media", "collections", "photo_library"] },
  { token: "editor_image", candidates: ["image"] },
  { token: "editor_video", candidates: ["videocam", "movie"] },
  { token: "editor_audio", candidates: ["graphic_eq", "audio_file", "mic"] },
  { token: "editor_tail", candidates: ["call_made", "subdirectory_arrow_left"] },
  { token: "editor_keyboard", candidates: ["keyboard"] },
  { token: "editor_text_input", candidates: ["input", "text_fields", "short_text"] },
  { token: "editor_button_icon", candidates: ["smart_button", "buttons_alt", "radio_button_checked"] },
  { token: "editor_relief", candidates: ["texture", "gradient", "blur_on"] },
  { token: "editor_shadow", candidates: ["layers", "filter_none", "shadow"] },
  { token: "editor_border", candidates: ["border_style", "border_outer"] }
];

function parseArgs(argv) {
  const args = {
    dryRun: false,
    overwrite: false,
    style: null,
    weight: null,
    out: null,
    yes: false
  };

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];

    if (arg === "--dry-run") {
      args.dryRun = true;
    } else if (arg === "--overwrite") {
      args.overwrite = true;
    } else if (arg === "--yes" || arg === "-y") {
      args.yes = true;
    } else if (arg === "--style") {
      args.style = readRequiredValue(argv, ++i, "--style");
    } else if (arg.startsWith("--style=")) {
      args.style = arg.slice("--style=".length);
    } else if (arg === "--weight") {
      args.weight = readRequiredValue(argv, ++i, "--weight");
    } else if (arg.startsWith("--weight=")) {
      args.weight = arg.slice("--weight=".length);
    } else if (arg === "--out") {
      args.out = readRequiredValue(argv, ++i, "--out");
    } else if (arg.startsWith("--out=")) {
      args.out = arg.slice("--out=".length);
    } else if (arg === "--help" || arg === "-h") {
      printHelpAndExit();
    } else if (arg.startsWith("--")) {
      throw new Error(`Argumento no reconocido: ${arg}`);
    } else {
      throw new Error(
        `Este script no acepta rutas como argumento: ${arg}\n` +
        `Colócate primero en el directorio icon-themes y ejecuta node desde ahí.`
      );
    }
  }

  if (args.style != null) args.style = normalizeStyle(args.style);
  if (args.weight != null) args.weight = normalizeWeight(args.weight);
  if (args.out != null) args.out = normalizeOutFolder(args.out);

  return args;
}

function readRequiredValue(argv, index, flag) {
  const value = argv[index];
  if (!value || value.startsWith("--")) {
    throw new Error(`Falta valor para ${flag}`);
  }
  return value;
}

function printHelpAndExit() {
  console.log(`${VERSION}

Uso:
  node add-editor-material-icons-prompt-weight.cjs [--dry-run] [--overwrite]
  node add-editor-material-icons-prompt-weight.cjs --style rounded --weight 600
  node add-editor-material-icons-prompt-weight.cjs --style outlined --weight 400 --out material-outlined-basic

Opciones:
  --style rounded|outlined|sharp
  --weight 100|200|300|400|500|600|700
  --out <folderName>
  --dry-run
  --overwrite
  --yes, -y       No pregunta confirmación final cuando faltan argumentos interactivos.

Regla de ruta:
  - El icon themes root es SIEMPRE el directorio actual desde donde ejecutas node.
  - El set se crea dentro de ese directorio.
  - No se aceptan rutas como argumento.

Ejemplo:
  cd /Volumes/SD_02/PROYECTOS/MOCKUPS/scripts/icon-themes
  node add-editor-material-icons-prompt-weight.cjs
`);
  process.exit(0);
}

function normalizeStyle(value) {
  const style = String(value || "").trim().toLowerCase();
  if (!VALID_STYLES.has(style)) {
    throw new Error(`Estilo inválido: ${value}. Usa rounded, outlined o sharp.`);
  }
  return style;
}

function normalizeWeight(value) {
  const weight = String(value || "").trim();
  if (!VALID_WEIGHTS.has(weight)) {
    throw new Error(`Peso inválido: ${value}. Usa 100, 200, 300, 400, 500, 600 o 700.`);
  }
  return weight;
}

function normalizeOutFolder(value) {
  const out = String(value || "").trim();
  if (!out) throw new Error("El nombre del set no puede estar vacío.");
  if (out.includes("/") || out.includes("\\")) {
    throw new Error(`El nombre del set no debe incluir rutas ni separadores: ${out}`);
  }
  if (!/^[a-z0-9][a-z0-9._-]*$/i.test(out)) {
    throw new Error(`Nombre de set inválido: ${out}`);
  }
  return out;
}

function defaultOutFolder(style, weight) {
  // Convención actual del proyecto: 400 se llama basic.
  const suffix = weight === "400" ? "basic" : weight;
  return `material-${style}-${suffix}`;
}

function question(rl, text) {
  return new Promise((resolve) => rl.question(text, resolve));
}

async function completeInteractiveArgs(args) {
  const canAsk = process.stdin.isTTY && process.stdout.isTTY && !args.yes;

  if (!canAsk) {
    const style = args.style || "rounded";
    const weight = args.weight || "600";
    const out = args.out || defaultOutFolder(style, weight);
    return { ...args, style, weight, out };
  }

  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });

  try {
    let style = args.style;
    let weight = args.weight;
    let out = args.out;

    while (!style) {
      const answer = (await question(rl, "Estilo Material [rounded/outlined/sharp] (rounded): ")).trim();
      try {
        style = normalizeStyle(answer || "rounded");
      } catch (error) {
        console.log(error.message);
      }
    }

    while (!weight) {
      const answer = (await question(rl, "Peso Material [100/200/300/400/500/600/700] (600): ")).trim();
      try {
        weight = normalizeWeight(answer || "600");
      } catch (error) {
        console.log(error.message);
      }
    }

    const suggestedOut = defaultOutFolder(style, weight);
    while (!out) {
      const answer = (await question(rl, `Nombre del set (${suggestedOut}): `)).trim();
      try {
        out = normalizeOutFolder(answer || suggestedOut);
      } catch (error) {
        console.log(error.message);
      }
    }

    return { ...args, style, weight, out };
  } finally {
    rl.close();
  }
}

function ensureDir(dir, dryRun, label) {
  if (fs.existsSync(dir)) {
    if (!fs.statSync(dir).isDirectory()) {
      throw new Error(`${label} existe pero no es un directorio: ${dir}`);
    }
    return false;
  }

  if (dryRun) {
    console.log(`[dry-run] Crearía ${label}: ${dir}`);
    return true;
  }

  fs.mkdirSync(dir, { recursive: true });

  if (!fs.existsSync(dir) || !fs.statSync(dir).isDirectory()) {
    throw new Error(`No se pudo crear ${label}: ${dir}`);
  }

  console.log(`Creado ${label}: ${dir}`);
  return true;
}

function removeDirIfExists(dir) {
  if (fs.existsSync(dir)) {
    fs.rmSync(dir, { recursive: true, force: true });
  }
}

function walk(dir, out = []) {
  if (!fs.existsSync(dir)) return out;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      walk(full, out);
    } else {
      out.push(full);
    }
  }

  return out;
}

function normalizePath(p) {
  return p.replace(/\\/g, "/").toLowerCase();
}

function scoreCandidate(file, materialName, style) {
  const p = normalizePath(file);
  const base = path.basename(file, ".svg").toLowerCase();

  let score = 0;

  if (!p.endsWith(".svg")) return -9999;

  if (
    p.includes(`/${style}/`) ||
    p.includes(`-${style}`) ||
    p.includes(`_${style}`) ||
    p.includes(`/materialsymbols${style}/`)
  ) {
    score += 100;
  } else {
    score -= 100;
  }

  for (const otherStyle of ["rounded", "outlined", "sharp"]) {
    if (otherStyle === style) continue;

    if (
      p.includes(`/${otherStyle}/`) ||
      p.includes(`-${otherStyle}`) ||
      p.includes(`_${otherStyle}`) ||
      p.includes(`/materialsymbols${otherStyle}/`)
    ) {
      score -= 150;
    }
  }

  if (base === materialName) score += 160;
  if (base.startsWith(materialName + "_")) score += 120;
  if (base.includes(materialName)) score += 30;

  if (p.includes("fill0") || p.includes("fill_0") || p.includes("/0/")) score += 20;
  if (p.includes("fill1") || p.includes("fill_1") || p.includes("/1/")) score -= 20;

  if (p.includes("24px") || p.includes("/24/")) score += 15;
  if (p.includes("20px") || p.includes("/20/")) score += 5;
  if (p.includes("48px") || p.includes("/48/")) score -= 5;

  return score;
}

function findBestSvg(allSvgFiles, materialName, style) {
  const candidates = allSvgFiles
    .map((file) => ({ file, score: scoreCandidate(file, materialName, style) }))
    .filter((item) => item.score > 0)
    .sort((a, b) => b.score - a.score);

  return candidates[0]?.file ?? null;
}

function findTokenSource(allSvgFiles, icon, style) {
  for (const materialName of icon.candidates) {
    const source = findBestSvg(allSvgFiles, materialName, style);
    if (source) return { source, materialName };
  }

  return null;
}

function makeSvgTintable(svg) {
  let out = svg;

  out = out
    .replace(/\sfill="(?!none\b|currentColor\b)[^"]*"/gi, ' fill="currentColor"')
    .replace(/\sstroke="(?!none\b|currentColor\b)[^"]*"/gi, ' stroke="currentColor"');

  if (!/\sfill=/i.test(out) && !/\sstroke=/i.test(out)) {
    out = out.replace(/<svg\b/i, '<svg fill="currentColor"');
  }

  if (!/\bviewBox=/i.test(out)) {
    out = out.replace(/<svg\b/i, '<svg viewBox="0 0 24 24"');
  }

  if (!/currentColor/i.test(out)) {
    out = out.replace(/<svg\b/i, '<svg fill="currentColor"');
  }

  return out;
}

function validateSvg(svg, target) {
  const errors = [];

  if (!/<svg\b/i.test(svg)) errors.push("no contiene <svg>");
  if (!/\bviewBox=/i.test(svg)) errors.push("no contiene viewBox");
  if (!/currentColor/i.test(svg)) errors.push("no contiene currentColor");

  if (errors.length > 0) {
    throw new Error(`${target}: SVG inválido: ${errors.join(", ")}`);
  }
}

function downloadPackage(weight) {
  const packageName = `@material-symbols/svg-${weight}`;
  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), `material-symbols-${weight}-`));

  console.log(`Descargando ${packageName}@latest...`);

  execFileSync(
    "npm",
    ["pack", `${packageName}@latest`, "--pack-destination", tmpDir],
    { stdio: "inherit" }
  );

  const tgz = fs.readdirSync(tmpDir).find((name) => name.endsWith(".tgz"));
  if (!tgz) throw new Error(`No se encontró el .tgz descargado para ${packageName}.`);

  execFileSync("tar", ["-xzf", path.join(tmpDir, tgz), "-C", tmpDir], { stdio: "inherit" });

  const extractedRoot = path.join(tmpDir, "package");
  if (!fs.existsSync(extractedRoot)) {
    throw new Error(`No se encontró la carpeta extraída para ${packageName}.`);
  }

  const allSvgFiles = walk(extractedRoot).filter((file) => file.toLowerCase().endsWith(".svg"));
  if (allSvgFiles.length === 0) throw new Error(`No se encontraron SVG dentro de ${packageName}.`);

  console.log(`Encontrados ${allSvgFiles.length} SVG en ${packageName}.`);
  return { packageName, tmpDir, extractedRoot, allSvgFiles };
}

function copyLicense(download, iconThemesRoot, dryRun) {
  const licenseDir = path.join(iconThemesRoot, "_licenses");
  ensureDir(licenseDir, dryRun, "directorio de licencias");

  if (dryRun) return;

  const files = walk(download.extractedRoot);
  const license = files.find((file) => path.basename(file).toLowerCase() === "license");
  const target = path.join(licenseDir, `material-symbols-svg-${download.weight}-apache-2.0.txt`);

  if (license) {
    fs.copyFileSync(license, target);
  } else {
    fs.writeFileSync(
      target,
      [
        `Material Symbols package: ${download.packageName}`,
        "Expected license: Apache License 2.0.",
        "Please verify the license from the installed npm package or official repository."
      ].join("\n"),
      "utf8"
    );
  }

  if (!fs.existsSync(target) || fs.statSync(target).size === 0) {
    throw new Error(`No se pudo verificar la licencia escrita: ${target}`);
  }
}

function writeVerifiedFile(target, content) {
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, content, "utf8");

  if (!fs.existsSync(target)) {
    throw new Error(`Escritura fallida: el archivo no existe después de escribir: ${target}`);
  }

  const size = fs.statSync(target).size;
  if (size === 0) {
    throw new Error(`Escritura fallida: el archivo queda vacío: ${target}`);
  }

  return size;
}

function verifyExpectedFiles(setDir) {
  const missing = [];

  for (const icon of ICONS) {
    const target = path.join(setDir, `${icon.token}.svg`);
    if (!fs.existsSync(target) || fs.statSync(target).size === 0) {
      missing.push(target);
    }
  }

  if (missing.length > 0) {
    throw new Error(`Verificación final fallida. Faltan ${missing.length} SVG:\n${missing.slice(0, 20).join("\n")}${missing.length > 20 ? "\n..." : ""}`);
  }
}

async function main() {
  const parsedArgs = parseArgs(process.argv.slice(2));
  const args = await completeInteractiveArgs(parsedArgs);

  const iconThemesRoot = process.cwd();
  const setDir = path.join(iconThemesRoot, args.out);

  console.log("");
  console.log(VERSION);
  console.log(`Icon themes root = directorio actual: ${iconThemesRoot}`);
  console.log(`Set objetivo: ${args.out}`);
  console.log(`Material style: ${args.style}`);
  console.log(`Material weight: ${args.weight}`);
  console.log(`Modo: ${args.dryRun ? "dry-run" : "write"}${args.overwrite ? " + overwrite" : ""}`);
  console.log("");

  let download = null;

  try {
    ensureDir(iconThemesRoot, args.dryRun, "icon themes root");
    ensureDir(setDir, args.dryRun, `set ${args.out}`);

    download = downloadPackage(args.weight);
    download.weight = args.weight;

    console.log("");
    console.log("Validando candidatos antes de escribir...");

    const plan = [];
    const missing = [];

    for (const icon of ICONS) {
      const found = findTokenSource(download.allSvgFiles, icon, args.style);

      if (!found) {
        missing.push({ token: icon.token, candidates: icon.candidates });
        continue;
      }

      plan.push({
        token: icon.token,
        candidates: icon.candidates,
        materialName: found.materialName,
        source: found.source,
        target: path.join(setDir, `${icon.token}.svg`)
      });
    }

    if (missing.length > 0) {
      console.error("");
      console.error("Faltan candidatos. No se escribirá nada.");
      for (const item of missing) {
        console.error(`- ${item.token}: ${item.candidates.join(" / ")}`);
      }
      process.exitCode = 1;
      return;
    }

    copyLicense(download, iconThemesRoot, args.dryRun);

    const added = [];
    const skipped = [];
    const overwritten = [];
    const wouldAdd = [];
    const wouldOverwrite = [];

    for (const item of plan) {
      const exists = fs.existsSync(item.target);

      if (exists && !args.overwrite) {
        skipped.push(item);
        continue;
      }

      const rawSvg = fs.readFileSync(item.source, "utf8");
      const svg = makeSvgTintable(rawSvg);
      validateSvg(svg, item.target);

      if (args.dryRun) {
        if (exists) wouldOverwrite.push(item);
        else wouldAdd.push(item);
        continue;
      }

      writeVerifiedFile(item.target, svg);

      if (exists) overwritten.push(item);
      else added.push(item);
    }

    if (!args.dryRun) {
      verifyExpectedFiles(setDir);
    }

    console.log("");
    console.log("Resumen");
    console.log(`- Icon themes root: ${iconThemesRoot}`);
    console.log(`- Set actualizado: ${args.out}`);
    console.log(`- Material style: ${args.style}`);
    console.log(`- Material weight: ${args.weight}`);
    console.log(`- Tokens requeridos: ${ICONS.length}`);

    if (args.dryRun) {
      console.log(`- Iconos que crearía: ${wouldAdd.length}`);
      console.log(`- Iconos que sobrescribiría: ${wouldOverwrite.length}`);
      console.log(`- Iconos que omitiría porque ya existen: ${skipped.length}`);
      console.log("- Archivos escritos: 0");
    } else {
      console.log(`- Iconos añadidos verificados: ${added.length}`);
      console.log(`- Iconos omitidos porque ya existían: ${skipped.length}`);
      console.log(`- Iconos sobrescritos verificados: ${overwritten.length}`);
      console.log(`- Archivos esperados verificados: ${ICONS.length}`);
    }

    console.log("- Errores: 0");
    console.log(`- Directorio del set: ${setDir}`);

    console.log("");
    console.log("Mapa token -> Material usado:");
    for (const icon of ICONS) {
      const item = plan.find((entry) => entry.token === icon.token);
      const material = item?.materialName || "?";
      const note = icon.token === "editor_tail" ? "  (aproximación; no hay candidato perfecto)" : "";
      console.log(`- ${icon.token} -> ${material}${note}`);
    }

    if (args.dryRun) {
      console.log("");
      console.log("Dry-run: no se ha escrito nada.");
    }
  } finally {
    if (download) removeDirIfExists(download.tmpDir);
  }
}

main().catch((error) => {
  console.error("");
  console.error(`ERROR: ${error.message}`);
  process.exitCode = 1;
});
