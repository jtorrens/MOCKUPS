#!/usr/bin/env node

/**
 * Descarga un subset básico de Material Symbols Rounded en SVG
 * y lo copia en estructura plana con nombres semánticos propios.
 *
 * Uso:
 *   node scripts/icon-themes/download-material-rounded-basic-flat.cjs
 *
 * Resultado:
 *   assets/icon-themes/material-rounded-basic/*.svg
 */

const fs = require("fs");
const path = require("path");
const os = require("os");
const { execFileSync } = require("child_process");

const PACKAGE_NAME = "@material-symbols/svg-400";
const THEME_NAME = "material-rounded-basic";
const STYLE = "rounded";

const projectRoot = process.cwd();

const outputDir = path.join(projectRoot, "assets", "icon-themes", THEME_NAME);
const licenseDir = path.join(projectRoot, "assets", "icon-themes", "_licenses");

const ICONS = [
  { key: "nav_home", material: "home" },
  { key: "nav_back", material: "arrow_back" },
  { key: "nav_forward", material: "arrow_forward" },
  { key: "nav_close", material: "close" },
  { key: "nav_menu", material: "menu" },
  { key: "nav_more_horizontal", material: "more_horiz" },
  { key: "nav_more_vertical", material: "more_vert" },
  { key: "nav_expand_more", material: "expand_more" },
  { key: "nav_chevron_left", material: "chevron_left" },
  { key: "nav_chevron_right", material: "chevron_right" },

  { key: "android_nav_back", material: "arrow_back_ios_new" },
  { key: "android_nav_back_alt", material: "arrow_back" },
  { key: "android_nav_home", material: "circle" },
  { key: "android_nav_recents", material: "crop_square" },
  { key: "android_nav_gesture_bar", material: "horizontal_rule" },
  { key: "android_nav_keyboard_hide", material: "keyboard_hide" },

  { key: "system_settings", material: "settings" },
  { key: "system_search", material: "search" },
  { key: "system_notifications", material: "notifications" },
  { key: "system_lock", material: "lock" },
  { key: "system_unlock", material: "lock_open" },
  { key: "system_visibility", material: "visibility" },
  { key: "system_visibility_off", material: "visibility_off" },
  { key: "system_info", material: "info" },
  { key: "system_warning", material: "warning" },
  { key: "system_error", material: "error" },
  { key: "system_check", material: "check" },
  { key: "system_add", material: "add" },
  { key: "system_remove", material: "remove" },
  { key: "system_edit", material: "edit" },
  { key: "system_delete", material: "delete" },

  { key: "status_wifi", material: "wifi" },
  { key: "status_bluetooth", material: "bluetooth" },
  { key: "status_signal", material: "signal_cellular_alt" },
  { key: "status_signal_0", material: "signal_cellular_0_bar" },
  { key: "status_signal_1", material: "signal_cellular_1_bar" },
  { key: "status_signal_2", material: "signal_cellular_2_bar" },
  { key: "status_signal_3", material: "signal_cellular_3_bar" },
  { key: "status_signal_4", material: "signal_cellular_4_bar" },
  { key: "status_signal_off", material: "signal_cellular_off" },
  { key: "status_no_sim", material: "sim_card_alert" },
  { key: "status_battery_full", material: "battery_full" },
  { key: "status_battery_charging", material: "battery_charging_full" },
  { key: "status_battery_alert", material: "battery_alert" },
  { key: "status_airplane", material: "airplane_mode_active" },
  { key: "status_do_not_disturb", material: "do_not_disturb_on" },

  { key: "network_5g", material: "5g" },
  { key: "network_4g", material: "4g_mobiledata" },
  { key: "network_4g_plus", material: "4g_plus_mobiledata" },
  { key: "network_3g", material: "3g_mobiledata" },
  { key: "network_lte", material: "lte_mobiledata" },
  { key: "network_lte_plus", material: "lte_plus_mobiledata" },
  { key: "network_edge", material: "e_mobiledata" },
  { key: "network_gprs", material: "g_mobiledata" },

  { key: "phone_call", material: "call" },
  { key: "phone_hangup", material: "call_end" },
  { key: "phone_in_talk", material: "phone_in_talk" },
  { key: "chat_bubble", material: "chat" },
  { key: "chat_sms", material: "sms" },
  { key: "chat_send", material: "send" },
  { key: "chat_attach", material: "attach_file" },
  { key: "chat_emoji", material: "emoji_emotions" },
  { key: "contact_person", material: "person" },
  { key: "contact_group", material: "group" },
  { key: "contact_contacts", material: "contacts" },

  { key: "media_camera", material: "photo_camera" },
  { key: "media_image", material: "image" },
  { key: "media_video", material: "videocam" },
  { key: "media_mic", material: "mic" },
  { key: "media_volume", material: "volume_up" },
  { key: "media_volume_off", material: "volume_off" },
  { key: "media_play", material: "play_arrow" },
  { key: "media_pause", material: "pause" },
  { key: "media_stop", material: "stop" },

  { key: "app_calendar", material: "calendar_month" },
  { key: "app_clock", material: "schedule" },
  { key: "app_mail", material: "mail" },
  { key: "app_map", material: "map" },
  { key: "app_location", material: "location_on" },
  { key: "app_language", material: "language" },
  { key: "app_folder", material: "folder" },
  { key: "app_download", material: "download" },
  { key: "app_upload", material: "upload" },
  { key: "app_share", material: "share" }
];

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function removeDirIfExists(dir) {
  if (fs.existsSync(dir)) fs.rmSync(dir, { recursive: true, force: true });
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

function normalizePath(p) {
  return p.replace(/\\/g, "/").toLowerCase();
}

function scoreCandidate(file, materialName) {
  const p = normalizePath(file);
  const base = path.basename(file, ".svg").toLowerCase();
  let score = 0;

  if (!p.endsWith(".svg")) return -9999;

  if (
    p.includes(`/${STYLE}/`) ||
    p.includes(`-${STYLE}`) ||
    p.includes(`_${STYLE}`) ||
    p.includes(`/materialsymbols${STYLE}/`)
  ) score += 100;
  else score -= 100;

  for (const otherStyle of ["rounded", "outlined", "sharp"]) {
    if (otherStyle === STYLE) continue;
    if (
      p.includes(`/${otherStyle}/`) ||
      p.includes(`-${otherStyle}`) ||
      p.includes(`_${otherStyle}`) ||
      p.includes(`/materialsymbols${otherStyle}/`)
    ) score -= 150;
  }

  if (base === materialName) score += 120;
  if (base.startsWith(materialName + "_")) score += 90;
  if (base.includes(materialName)) score += 30;

  if (p.includes("fill0") || p.includes("fill_0") || p.includes("/0/")) score += 20;
  if (p.includes("fill1") || p.includes("fill_1") || p.includes("/1/")) score -= 20;

  if (p.includes("24px") || p.includes("/24/")) score += 15;
  if (p.includes("20px") || p.includes("/20/")) score += 5;
  if (p.includes("48px") || p.includes("/48/")) score -= 5;

  return score;
}

function findBestSvg(allSvgFiles, materialName) {
  const candidates = allSvgFiles
    .map((file) => ({ file, score: scoreCandidate(file, materialName) }))
    .filter((item) => item.score > 0)
    .sort((a, b) => b.score - a.score);

  return candidates[0]?.file ?? null;
}

function makeSvgTintable(svg) {
  return svg
    .replace(/fill="#000000"/gi, 'fill="currentColor"')
    .replace(/fill="#000"/gi, 'fill="currentColor"')
    .replace(/fill="black"/gi, 'fill="currentColor"')
    .replace(/stroke="#000000"/gi, 'stroke="currentColor"')
    .replace(/stroke="#000"/gi, 'stroke="currentColor"')
    .replace(/stroke="black"/gi, 'stroke="currentColor"');
}

function copyLicense(extractedRoot) {
  const files = walk(extractedRoot);
  const license = files.find((file) => path.basename(file).toLowerCase() === "license");

  ensureDir(licenseDir);

  if (license) {
    fs.copyFileSync(license, path.join(licenseDir, "material-symbols-apache-2.0.txt"));
  } else {
    fs.writeFileSync(
      path.join(licenseDir, "material-symbols-license-note.txt"),
      [
        "Material Symbols by Google.",
        "Expected license: Apache License 2.0.",
        "Please verify the license from the installed npm package or official repository."
      ].join("\n"),
      "utf8"
    );
  }
}

function writeReadme() {
  const readme = `# Material Rounded Basic Icon Theme

Generated from \`${PACKAGE_NAME}\`.

Flat semantic subset of Material Symbols Rounded icons for the MOCKUPS app.

Examples:
- \`nav_back.svg\`
- \`status_wifi.svg\`
- \`status_signal_4.svg\`
- \`network_5g.svg\`
- \`android_nav_home.svg\`

SVGs are normalized to use \`currentColor\` when possible.
`;

  fs.writeFileSync(path.join(outputDir, "README.md"), readme, "utf8");
}

function main() {
  removeDirIfExists(outputDir);
  ensureDir(outputDir);

  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), "material-symbols-"));

  try {
    console.log(`Output folder: ${outputDir}`);
    console.log(`Downloading ${PACKAGE_NAME}@latest...`);

    execFileSync(
      "npm",
      ["pack", `${PACKAGE_NAME}@latest`, "--pack-destination", tmpDir],
      { stdio: "inherit" }
    );

    const tgz = fs.readdirSync(tmpDir).find((name) => name.endsWith(".tgz"));
    if (!tgz) throw new Error("No se encontró el .tgz descargado por npm pack.");

    console.log("Extracting package...");

    execFileSync("tar", ["-xzf", path.join(tmpDir, tgz), "-C", tmpDir], { stdio: "inherit" });

    const extractedRoot = path.join(tmpDir, "package");
    if (!fs.existsSync(extractedRoot)) throw new Error("No se encontró la carpeta extraída.");

    const allSvgFiles = walk(extractedRoot).filter((file) => file.toLowerCase().endsWith(".svg"));
    console.log(`Found ${allSvgFiles.length} SVG files in downloaded package.`);

    if (allSvgFiles.length === 0) throw new Error("No se encontraron SVG dentro del paquete.");

    const missing = [];
    const copied = [];

    for (const item of ICONS) {
      const { key, material } = item;
      const source = findBestSvg(allSvgFiles, material);

      if (!source) {
        missing.push(`${key} -> ${material}`);
        continue;
      }

      const target = path.join(outputDir, `${key}.svg`);
      const rawSvg = fs.readFileSync(source, "utf8");
      const normalizedSvg = makeSvgTintable(rawSvg);

      fs.writeFileSync(target, normalizedSvg, "utf8");

      copied.push({
        semanticName: key,
        materialName: material,
        file: `${key}.svg`,
        source: path.relative(extractedRoot, source)
      });
    }

    copyLicense(extractedRoot);
    writeReadme();

    const manifest = {
      name: THEME_NAME,
      source: PACKAGE_NAME,
      style: STYLE,
      weight: 400,
      generatedAt: new Date().toISOString(),
      structure: "flat",
      count: copied.length,
      icons: copied,
      missing
    };

    fs.writeFileSync(path.join(outputDir, "manifest.json"), JSON.stringify(manifest, null, 2), "utf8");

    console.log("");
    console.log(`Copied ${copied.length} icons.`);
    console.log("");
    console.log("Output:");
    console.log(outputDir);

    if (missing.length > 0) {
      console.log("");
      console.log("Missing icons:");
      for (const item of missing) console.log(`- ${item}`);
    }
  } finally {
    removeDirIfExists(tmpDir);
  }
}

main();
