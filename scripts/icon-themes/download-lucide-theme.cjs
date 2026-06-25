#!/usr/bin/env node

/**
 * Descarga un subset equivalente de Lucide Icons en SVG
 * y lo copia en estructura plana con los mismos nombres semánticos
 * que usamos en los themes Material.
 *
 * Compatible con proyectos que usan:
 *   "type": "module"
 *
 * Por eso este archivo debe tener extensión .cjs.
 *
 * Uso:
 *   node scripts/icon-themes/download-lucide-theme.cjs
 *
 * Opciones:
 *   --out <folderName>
 *   --stroke <number>
 *   --keep-existing
 *
 * Ejemplos:
 *   node scripts/icon-themes/download-lucide-theme.cjs
 *   node scripts/icon-themes/download-lucide-theme.cjs --out lucide-basic
 *   node scripts/icon-themes/download-lucide-theme.cjs --out lucide-semibold --stroke 2.25
 *
 * Resultado por defecto:
 *   assets/icon-themes/lucide-basic/*.svg
 */

const fs = require("fs");
const path = require("path");
const os = require("os");
const { execFileSync } = require("child_process");

const PACKAGE_NAME = "lucide-static";
const DEFAULT_OUT = "lucide-basic";
const DEFAULT_STROKE = "2";

const TEXT_FALLBACKS = {
  network_5g: "5G",
  network_4g: "4G",
  network_4g_plus: "4G+",
  network_3g: "3G",
  network_lte: "LTE",
  network_lte_plus: "LTE+",
  network_edge: "E",
  network_gprs: "G",
  android_nav_gesture_bar: "BAR"
};

const ICONS = [
  { key: "nav_home", lucide: ["home"] },
  { key: "nav_back", lucide: ["arrow-left"] },
  { key: "nav_forward", lucide: ["arrow-right"] },
  { key: "nav_close", lucide: ["x"] },
  { key: "nav_menu", lucide: ["menu"] },
  { key: "nav_more_horizontal", lucide: ["ellipsis"] },
  { key: "nav_more_vertical", lucide: ["ellipsis-vertical"] },
  { key: "nav_expand_more", lucide: ["chevron-down"] },
  { key: "nav_chevron_left", lucide: ["chevron-left"] },
  { key: "nav_chevron_right", lucide: ["chevron-right"] },

  { key: "android_nav_back", lucide: ["arrow-left", "chevron-left"] },
  { key: "android_nav_back_alt", lucide: ["arrow-left"] },
  { key: "android_nav_home", lucide: ["circle"] },
  { key: "android_nav_recents", lucide: ["square"] },
  { key: "android_nav_gesture_bar", lucide: ["minus"] },
  { key: "android_nav_keyboard_hide", lucide: ["keyboard", "keyboard-off"] },

  { key: "system_settings", lucide: ["settings"] },
  { key: "system_search", lucide: ["search"] },
  { key: "system_notifications", lucide: ["bell"] },
  { key: "system_lock", lucide: ["lock"] },
  { key: "system_unlock", lucide: ["lock-open"] },
  { key: "system_visibility", lucide: ["eye"] },
  { key: "system_visibility_off", lucide: ["eye-off"] },
  { key: "system_info", lucide: ["info"] },
  { key: "system_warning", lucide: ["triangle-alert", "alert-triangle"] },
  { key: "system_error", lucide: ["circle-alert", "badge-alert"] },
  { key: "system_check", lucide: ["check"] },
  { key: "system_add", lucide: ["plus"] },
  { key: "system_remove", lucide: ["minus"] },
  { key: "system_edit", lucide: ["pencil", "edit"] },
  { key: "system_delete", lucide: ["trash-2", "trash"] },

  { key: "status_wifi", lucide: ["wifi"] },
  { key: "status_bluetooth", lucide: ["bluetooth"] },
  { key: "status_signal", lucide: ["signal"] },
  { key: "status_signal_0", lucide: ["signal-zero", "signal-low"] },
  { key: "status_signal_1", lucide: ["signal-low"] },
  { key: "status_signal_2", lucide: ["signal-medium"] },
  { key: "status_signal_3", lucide: ["signal-high", "signal-medium"] },
  { key: "status_signal_4", lucide: ["signal"] },
  { key: "status_signal_off", lucide: ["signal-zero", "signal"] },
  { key: "status_no_sim", lucide: ["badge-alert", "circle-alert"] },
  { key: "status_battery_full", lucide: ["battery-full", "battery"] },
  { key: "status_battery_charging", lucide: ["battery-charging"] },
  { key: "status_battery_alert", lucide: ["battery-warning", "battery-low"] },
  { key: "status_airplane", lucide: ["plane"] },
  { key: "status_do_not_disturb", lucide: ["circle-minus", "ban"] },

  { key: "network_5g", lucide: ["badge-5g"] },
  { key: "network_4g", lucide: ["badge-4g"] },
  { key: "network_4g_plus", lucide: ["badge-4g-plus"] },
  { key: "network_3g", lucide: ["badge-3g"] },
  { key: "network_lte", lucide: ["badge-lte"] },
  { key: "network_lte_plus", lucide: ["badge-lte-plus"] },
  { key: "network_edge", lucide: ["badge-e"] },
  { key: "network_gprs", lucide: ["badge-g"] },

  { key: "phone_call", lucide: ["phone", "phone-call"] },
  { key: "phone_hangup", lucide: ["phone-off"] },
  { key: "phone_in_talk", lucide: ["phone-call", "phone"] },
  { key: "chat_bubble", lucide: ["message-circle", "message-square"] },
  { key: "chat_sms", lucide: ["message-square", "messages-square"] },
  { key: "chat_send", lucide: ["send"] },
  { key: "chat_attach", lucide: ["paperclip"] },
  { key: "chat_emoji", lucide: ["smile"] },
  { key: "contact_person", lucide: ["user"] },
  { key: "contact_group", lucide: ["users"] },
  { key: "contact_contacts", lucide: ["contact", "contact-round", "book-user"] },

  { key: "media_camera", lucide: ["camera"] },
  { key: "media_image", lucide: ["image"] },
  { key: "media_video", lucide: ["video"] },
  { key: "media_mic", lucide: ["mic"] },
  { key: "media_volume", lucide: ["volume-2"] },
  { key: "media_volume_off", lucide: ["volume-off"] },
  { key: "media_play", lucide: ["play"] },
  { key: "media_pause", lucide: ["pause"] },
  { key: "media_stop", lucide: ["square"] },

  { key: "app_calendar", lucide: ["calendar-days", "calendar"] },
  { key: "app_clock", lucide: ["clock"] },
  { key: "app_mail", lucide: ["mail"] },
  { key: "app_map", lucide: ["map"] },
  { key: "app_location", lucide: ["map-pin"] },
  { key: "app_language", lucide: ["languages"] },
  { key: "app_folder", lucide: ["folder"] },
  { key: "app_download", lucide: ["download"] },
  { key: "app_upload", lucide: ["upload"] },
  { key: "app_share", lucide: ["share-2", "share"] }
];

function parseArgs(argv) {
  const args = {
    out: DEFAULT_OUT,
    stroke: DEFAULT_STROKE,
    keepExisting: false
  };

  for (let i = 0; i < argv.length; i++) {
    const token = argv[i];

    if (token === "--out") {
      args.out = argv[++i];
    } else if (token.startsWith("--out=")) {
      args.out = token.slice("--out=".length);
    } else if (token === "--stroke") {
      args.stroke = String(argv[++i]);
    } else if (token.startsWith("--stroke=")) {
      args.stroke = String(token.slice("--stroke=".length));
    } else if (token === "--keep-existing") {
      args.keepExisting = true;
    } else if (token === "--help" || token === "-h") {
      printHelpAndExit();
    } else {
      throw new Error(`Argumento no reconocido: ${token}`);
    }
  }

  const strokeNum = Number(args.stroke);
  if (!Number.isFinite(strokeNum) || strokeNum <= 0) {
    throw new Error("--stroke debe ser un número positivo. Ejemplo: --stroke 2.25");
  }

  return args;
}

function printHelpAndExit() {
  console.log(`
Uso:
  node scripts/icon-themes/download-lucide-theme.cjs

Opciones:
  --out <folderName>
  --stroke <number>
  --keep-existing

Ejemplos:
  node scripts/icon-themes/download-lucide-theme.cjs
  node scripts/icon-themes/download-lucide-theme.cjs --out lucide-basic
  node scripts/icon-themes/download-lucide-theme.cjs --out lucide-semibold --stroke 2.25
  node scripts/icon-themes/download-lucide-theme.cjs --out lucide-bold-ish --stroke 2.75
`);
  process.exit(0);
}

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
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

function findLucideSvg(allSvgFiles, lucideNames) {
  const normalizedCandidates = lucideNames.map((name) => `${name}.svg`.toLowerCase());

  for (const candidate of normalizedCandidates) {
    const found = allSvgFiles.find((file) => path.basename(file).toLowerCase() === candidate);
    if (found) return found;
  }

  for (const name of lucideNames) {
    const suffix = `/${name}.svg`.toLowerCase();
    const found = allSvgFiles.find((file) => normalizePath(file).endsWith(suffix));
    if (found) return found;
  }

  return null;
}

function makeLucideSvgTintable(svg, strokeWidth) {
  let out = svg;

  out = out
    .replace(/stroke="#000000"/gi, 'stroke="currentColor"')
    .replace(/stroke="#000"/gi, 'stroke="currentColor"')
    .replace(/stroke="black"/gi, 'stroke="currentColor"')
    .replace(/fill="#000000"/gi, 'fill="currentColor"')
    .replace(/fill="#000"/gi, 'fill="currentColor"')
    .replace(/fill="black"/gi, 'fill="currentColor"');

  if (/stroke-width="[^"]+"/i.test(out)) {
    out = out.replace(/stroke-width="[^"]+"/gi, `stroke-width="${strokeWidth}"`);
  } else {
    out = out.replace("<svg ", `<svg stroke-width="${strokeWidth}" `);
  }

  return out;
}

function makeTextFallbackSvg(label) {
  if (label === "BAR") {
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none">
  <rect x="5" y="10.5" width="14" height="3" rx="1.5" fill="currentColor"/>
</svg>
`;
  }

  const fontSize = label.length <= 2 ? 9.5 : label.length <= 3 ? 8.2 : 7.1;

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
  <text
    x="12"
    y="13.5"
    text-anchor="middle"
    dominant-baseline="middle"
    fill="currentColor"
    font-family="Arial, Helvetica, sans-serif"
    font-size="${fontSize}"
    font-weight="700"
    letter-spacing="-0.4">${escapeXml(label)}</text>
</svg>
`;
}

function escapeXml(value) {
  return String(value)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function copyLicense(extractedRoot, licenseDir) {
  const files = walk(extractedRoot);
  const license = files.find((file) => path.basename(file).toLowerCase() === "license");

  ensureDir(licenseDir);

  if (license) {
    fs.copyFileSync(
      license,
      path.join(licenseDir, "lucide-isc-license.txt")
    );
  } else {
    fs.writeFileSync(
      path.join(licenseDir, "lucide-license-note.txt"),
      [
        "Lucide Icons.",
        "Expected license: ISC.",
        "Please verify the license from the installed npm package or official repository."
      ].join("\n"),
      "utf8"
    );
  }
}

function writeReadme({ outputDir, packageName, themeName, stroke }) {
  const readme = `# ${themeName}

Generated from \`${packageName}\`.

Flat semantic subset of Lucide icons for the MOCKUPS app.

## Flat structure

All icons are written directly in this folder with the same semantic filenames used by the Material themes:

- \`nav_back.svg\`
- \`status_wifi.svg\`
- \`status_signal_4.svg\`
- \`network_5g.svg\`
- \`android_nav_home.svg\`

This allows the app to switch icon themes by changing only the theme folder.

## Stroke

Generated stroke width:

\`\`\`txt
${stroke}
\`\`\`

## Network labels

Lucide does not provide native mobile network labels like 5G, 4G, LTE.
Those files are generated as simple text SVG fallbacks.

## Tinting

SVGs are normalized to use \`currentColor\` when possible.
`;

  fs.writeFileSync(path.join(outputDir, "README.md"), readme, "utf8");
}

function main() {
  const args = parseArgs(process.argv.slice(2));

  const projectRoot = process.cwd();
  const outputDir = path.join(projectRoot, "assets", "icon-themes", args.out);
  const licenseDir = path.join(projectRoot, "assets", "icon-themes", "_licenses");

  if (!args.keepExisting) {
    removeDirIfExists(outputDir);
  }

  ensureDir(outputDir);

  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), "lucide-static-"));

  try {
    console.log(`Package: ${PACKAGE_NAME}`);
    console.log(`Output folder: ${outputDir}`);
    console.log(`Stroke: ${args.stroke}`);
    console.log("");
    console.log(`Downloading ${PACKAGE_NAME}@latest...`);

    execFileSync(
      "npm",
      ["pack", `${PACKAGE_NAME}@latest`, "--pack-destination", tmpDir],
      { stdio: "inherit" }
    );

    const tgz = fs.readdirSync(tmpDir).find((name) => name.endsWith(".tgz"));

    if (!tgz) {
      throw new Error("No se encontró el .tgz descargado por npm pack.");
    }

    console.log("Extracting package...");

    execFileSync(
      "tar",
      ["-xzf", path.join(tmpDir, tgz), "-C", tmpDir],
      { stdio: "inherit" }
    );

    const extractedRoot = path.join(tmpDir, "package");

    if (!fs.existsSync(extractedRoot)) {
      throw new Error("No se encontró la carpeta extraída del paquete npm.");
    }

    const allSvgFiles = walk(extractedRoot).filter((file) =>
      file.toLowerCase().endsWith(".svg")
    );

    console.log(`Found ${allSvgFiles.length} SVG files in downloaded package.`);

    if (allSvgFiles.length === 0) {
      throw new Error("No se encontraron SVG dentro del paquete.");
    }

    const missing = [];
    const copied = [];
    const generated = [];

    for (const item of ICONS) {
      const { key, lucide } = item;
      const source = findLucideSvg(allSvgFiles, lucide);

      const target = path.join(outputDir, `${key}.svg`);

      if (!source) {
        const fallbackLabel = TEXT_FALLBACKS[key];

        if (fallbackLabel) {
          const fallbackSvg = makeTextFallbackSvg(fallbackLabel);
          fs.writeFileSync(target, fallbackSvg, "utf8");

          generated.push({
            semanticName: key,
            file: `${key}.svg`,
            generatedFallback: fallbackLabel
          });

          continue;
        }

        missing.push(`${key} -> ${lucide.join(" | ")}`);
        continue;
      }

      const rawSvg = fs.readFileSync(source, "utf8");
      const normalizedSvg = makeLucideSvgTintable(rawSvg, args.stroke);

      fs.writeFileSync(target, normalizedSvg, "utf8");

      copied.push({
        semanticName: key,
        lucideCandidates: lucide,
        file: `${key}.svg`,
        source: path.relative(extractedRoot, source)
      });
    }

    copyLicense(extractedRoot, licenseDir);

    writeReadme({
      outputDir,
      packageName: PACKAGE_NAME,
      themeName: args.out,
      stroke: args.stroke
    });

    const manifest = {
      name: args.out,
      source: PACKAGE_NAME,
      style: "lucide",
      stroke: Number(args.stroke),
      generatedAt: new Date().toISOString(),
      structure: "flat",
      count: copied.length + generated.length,
      copied,
      generated,
      missing
    };

    fs.writeFileSync(
      path.join(outputDir, "manifest.json"),
      JSON.stringify(manifest, null, 2),
      "utf8"
    );

    console.log("");
    console.log(`Copied ${copied.length} Lucide icons.`);
    console.log(`Generated ${generated.length} fallback icons.`);
    console.log("");
    console.log("Output:");
    console.log(outputDir);

    if (missing.length > 0) {
      console.log("");
      console.log("Missing icons:");
      for (const item of missing) {
        console.log(`- ${item}`);
      }
      console.log("");
      console.log("Revisa manifest.json y ajusta el mapa ICONS si hace falta.");
    }
  } finally {
    removeDirIfExists(tmpDir);
  }
}

main();
