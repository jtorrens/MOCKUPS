import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import {
  buildIconThemeMapping,
  IconThemeManifestSchema,
} from "../domain/iconThemes/iconThemeMapping.js";
import { createDatabase } from "../persistence/sqlite/createDatabase.js";
import { developmentDatabasePath } from "../persistence/sqlite/paths.js";
import { stringifyJsonObject } from "../persistence/sqlite/json.js";

type CliOptions = Record<string, string | boolean>;

function parseArgs(args: string[]): CliOptions {
  const options: CliOptions = {};
  for (let index = 0; index < args.length; index += 1) {
    const arg = args[index];
    if (!arg.startsWith("--")) continue;
    const key = arg.slice(2);
    const next = args[index + 1];
    if (!next || next.startsWith("--")) {
      options[key] = true;
      continue;
    }
    options[key] = next;
    index += 1;
  }
  return options;
}

function stringOption(
  options: CliOptions,
  key: string,
  fallback: string,
): string {
  const value = options[key];
  return typeof value === "string" && value.trim() ? value.trim() : fallback;
}

function slugify(value: string): string {
  return (
    value
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "_")
      .replace(/^_+|_+$/g, "") || "icon_theme"
  );
}

const options = parseArgs(process.argv.slice(2));
const directory = resolve(
  process.cwd(),
  stringOption(
    options,
    "directory",
    "assets/FOQN_S2/icon-themes/material-rounded-basic",
  ),
);
const manifestPath = resolve(directory, "manifest.json");
const manifest = IconThemeManifestSchema.parse(
  JSON.parse(readFileSync(manifestPath, "utf8")),
);
const productionId = stringOption(options, "production-id", "production_demo");
const id = stringOption(options, "id", `icon_theme_${slugify(manifest.name)}`);
const name = stringOption(options, "name", manifest.name);
const family = stringOption(
  options,
  "family",
  manifest.style ? `material-${manifest.style}` : "material",
);
const assetRoot = stringOption(
  options,
  "asset-root",
  "icon-themes/material-rounded-basic",
);
const assignThemes = options["assign-themes"] !== false;
const mapping = buildIconThemeMapping(manifest);
const metadata = {
  importedFrom: directory,
  manifestPath,
  importedAt: new Date().toISOString(),
  sourceManifest: manifest,
};

const database = createDatabase(developmentDatabasePath);
try {
  const production = database
    .prepare("SELECT id FROM productions WHERE id = ?")
    .get(productionId);
  if (!production) {
    const productions = database
      .prepare("SELECT id, name FROM productions ORDER BY name, id")
      .all() as { id: string; name: string }[];
    throw new Error(
      `Production ${productionId} not found. Available: ${
        productions.map((item) => `${item.name} (${item.id})`).join(", ") ||
        "none"
      }`,
    );
  }

  database
    .prepare(
      `INSERT INTO icon_themes (
        id,
        production_id,
        name,
        family,
        asset_root,
        mapping_json,
        metadata_json
      ) VALUES (?, ?, ?, ?, ?, ?, ?)
      ON CONFLICT(id) DO UPDATE SET
        production_id = excluded.production_id,
        name = excluded.name,
        family = excluded.family,
        asset_root = excluded.asset_root,
        mapping_json = excluded.mapping_json,
        metadata_json = excluded.metadata_json`,
    )
    .run(
      id,
      productionId,
      name,
      family,
      assetRoot,
      stringifyJsonObject(mapping, "icon_themes.mapping_json"),
      stringifyJsonObject(metadata, "icon_themes.metadata_json"),
    );

  if (assignThemes) {
    database
      .prepare(
        `UPDATE themes
         SET icon_theme_id = ?
         WHERE production_id = ?
           AND (icon_theme_id IS NULL OR icon_theme_id = '')`,
      )
      .run(id, productionId);
  }

  console.log(
    `Imported ${manifest.icons.length} icon tokens into ${id} for ${productionId}.`,
  );
} finally {
  database.close();
}
