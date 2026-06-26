import { existsSync } from "node:fs";
import { copyFile, mkdir, readdir } from "node:fs/promises";
import { homedir } from "node:os";
import path from "node:path";
import { createDatabase } from "./createDatabase.js";
import { developmentDatabasePath } from "./paths.js";

const FONT_EXTENSIONS = new Set([".ttf", ".otf", ".woff", ".woff2"]);
const STYLE_MARKERS = new Set(
  [
    "black",
    "bold",
    "book",
    "compressed",
    "condensed",
    "demibold",
    "display",
    "extrabold",
    "extralight",
    "heavy",
    "italic",
    "light",
    "medium",
    "regular",
    "semibold",
    "thin",
    "variable",
    "variablefont",
    "wght",
  ].map((value) => value.toLowerCase()),
);

type Row = Record<string, unknown>;

interface ProductionRow {
  id: string;
  name: string;
  settings_json?: string | null;
}

interface FontReference {
  productionId: string;
  table: "themes" | "apps" | "module_theme_configs";
  id: string;
  column: string;
  path: string[];
  family: string;
}

interface FontFile {
  family: string;
  style: string;
  sourcePath: string;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === "object" && !Array.isArray(value));
}

function parseJsonObject(value: unknown): Record<string, unknown> {
  if (typeof value !== "string" || !value.trim()) return {};
  try {
    const parsed = JSON.parse(value) as unknown;
    return isRecord(parsed) ? parsed : {};
  } catch {
    return {};
  }
}

function productionRoot(row: ProductionRow) {
  const settings = parseJsonObject(row.settings_json);
  return typeof settings.mediaRoot === "string" ? settings.mediaRoot : "";
}

function valueAtPath(root: Record<string, unknown>, pathParts: string[]) {
  let cursor: unknown = root;
  for (const part of pathParts) {
    if (!isRecord(cursor)) return undefined;
    cursor = cursor[part];
  }
  return cursor;
}

function setAtPath(
  root: Record<string, unknown>,
  pathParts: string[],
  value: unknown,
) {
  let cursor: Record<string, unknown> = root;
  for (const part of pathParts.slice(0, -1)) {
    if (!isRecord(cursor[part])) cursor[part] = {};
    cursor = cursor[part] as Record<string, unknown>;
  }
  cursor[pathParts[pathParts.length - 1] ?? ""] = value;
}

function isFontFile(filePath: string) {
  return FONT_EXTENSIONS.has(path.extname(filePath).toLowerCase());
}

function titleCase(value: string) {
  return value
    .replace(/[_-]+/g, " ")
    .trim()
    .replace(/\s+/g, " ")
    .replace(/\b\w/g, (match) => match.toUpperCase());
}

function inferFontFile(sourcePath: string): FontFile {
  const name = path.parse(sourcePath).name;
  const parts = name.split(/[-_]+/).filter(Boolean);
  const styleIndex = parts.findIndex((part) =>
    STYLE_MARKERS.has(part.toLowerCase()),
  );
  if (styleIndex > 0) {
    const family = titleCase(parts.slice(0, styleIndex).join(" "));
    const styleParts = parts.slice(styleIndex);
    const variable = styleParts.some((part) =>
      ["variable", "variablefont", "wght"].includes(part.toLowerCase()),
    );
    return {
      family,
      style: variable ? "Variable" : titleCase(styleParts.join(" ")),
      sourcePath,
    };
  }
  if (parts.length > 1) {
    return {
      family: titleCase(parts.slice(0, -1).join(" ")),
      style: titleCase(parts[parts.length - 1] ?? "Regular"),
      sourcePath,
    };
  }
  return {
    family: titleCase(name),
    style: "Regular",
    sourcePath,
  };
}

async function collectFontFiles(directory: string): Promise<FontFile[]> {
  if (!directory || !existsSync(directory)) return [];
  const entries = await readdir(directory, { withFileTypes: true });
  const files: FontFile[] = [];
  for (const entry of entries) {
    const filePath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      if (
        ["/System/Library/Fonts", "/Library/Fonts", path.join(homedir(), "Library/Fonts")].includes(
          directory,
        )
      ) {
        files.push(...(await collectFontFiles(filePath)));
      }
      continue;
    }
    if (entry.isFile() && isFontFile(filePath)) {
      files.push(inferFontFile(filePath));
    }
  }
  return files;
}

function normalizeFamily(value: string) {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, "");
}

function safePathPart(value: string) {
  return (
    value
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9._-]+/g, "_")
      .replace(/^_+|_+$/g, "")
      .slice(0, 80) || "font"
  );
}

function fontReferences(database: ReturnType<typeof createDatabase>) {
  const references: FontReference[] = [];
  const themes = database
    .prepare("SELECT id, production_id, tokens_json FROM themes")
    .all() as Row[];
  for (const row of themes) {
    const tokens = parseJsonObject(row.tokens_json);
    const family = valueAtPath(tokens, ["fonts", "family"]);
    if (typeof family === "string" && family.trim()) {
      references.push({
        productionId: String(row.production_id),
        table: "themes",
        id: String(row.id),
        column: "tokens_json",
        path: ["fonts"],
        family,
      });
    }
  }

  const apps = database
    .prepare("SELECT id, production_id, config_json FROM apps")
    .all() as Row[];
  for (const row of apps) {
    const config = parseJsonObject(row.config_json);
    const family = valueAtPath(config, ["tokens_json", "fonts", "family"]);
    if (typeof family === "string" && family.trim()) {
      references.push({
        productionId: String(row.production_id),
        table: "apps",
        id: String(row.id),
        column: "config_json",
        path: ["tokens_json", "fonts"],
        family,
      });
    }
  }

  const moduleConfigs = database
    .prepare("SELECT id, production_id, tokens_json FROM module_theme_configs")
    .all() as Row[];
  for (const row of moduleConfigs) {
    const tokens = parseJsonObject(row.tokens_json);
    const typography = valueAtPath(tokens, ["typography"]);
    if (!isRecord(typography)) continue;
    for (const [group, value] of Object.entries(typography)) {
      if (!isRecord(value)) continue;
      const family = value.fontFamily;
      if (typeof family === "string" && family.trim()) {
        references.push({
          productionId: String(row.production_id),
          table: "module_theme_configs",
          id: String(row.id),
          column: "tokens_json",
          path: ["typography", group],
          family,
        });
      }
    }
  }
  return references;
}

async function main() {
  const database = createDatabase(developmentDatabasePath);
  const productions = database
    .prepare("SELECT id, name, settings_json FROM productions")
    .all() as ProductionRow[];
  const rootsByProduction = new Map(
    productions.map((production) => [production.id, productionRoot(production)]),
  );
  const references = fontReferences(database);
  const familiesByProduction = new Map<string, Set<string>>();
  for (const reference of references) {
    const families =
      familiesByProduction.get(reference.productionId) ?? new Set<string>();
    families.add(reference.family);
    familiesByProduction.set(reference.productionId, families);
  }

  const searchDirectories = [
    ...Array.from(rootsByProduction.values()).filter(Boolean),
    "/System/Library/Fonts",
    "/Library/Fonts",
    path.join(homedir(), "Library/Fonts"),
  ];
  const fontFiles = (
    await Promise.all(searchDirectories.map((directory) => collectFontFiles(directory)))
  ).flat();

  const upsert = database.prepare(
    `INSERT INTO production_fonts (
      id,
      production_id,
      family,
      files_json,
      source_path,
      metadata_json
    ) VALUES (?, ?, ?, ?, ?, ?)
    ON CONFLICT(production_id, family) DO UPDATE SET
      files_json = excluded.files_json,
      source_path = excluded.source_path,
      metadata_json = excluded.metadata_json`,
  );
  const approved = new Map<string, string>();
  const missing: { productionId: string; family: string }[] = [];

  for (const [productionId, families] of familiesByProduction) {
    const root = rootsByProduction.get(productionId);
    if (!root) {
      for (const family of families) missing.push({ productionId, family });
      continue;
    }
    for (const family of families) {
      const matches = fontFiles.filter(
        (file) => normalizeFamily(file.family) === normalizeFamily(family),
      );
      if (!matches.length) {
        missing.push({ productionId, family });
        continue;
      }
      const familyId = `font_${safePathPart(family)}`;
      const existingFamily = database
        .prepare(
          "SELECT id FROM production_fonts WHERE production_id = ? AND family = ?",
        )
        .get(productionId, family) as { id: string } | undefined;
      const approvedFamilyId =
        existingFamily &&
        existingFamily.id !== familyId &&
        !existingFamily.id.startsWith("font_new_font")
          ? existingFamily.id
          : familyId;
      if (
        existingFamily?.id &&
        existingFamily.id !== approvedFamilyId &&
        existingFamily.id.startsWith("font_new_font")
      ) {
        database
          .prepare("UPDATE production_fonts SET id = ? WHERE id = ?")
          .run(approvedFamilyId, existingFamily.id);
      }
      const copiedFiles: Record<string, unknown>[] = [];
      const targetDir = path.join(root, "fonts", safePathPart(family));
      await mkdir(targetDir, { recursive: true });
      for (const match of matches) {
        const fileName = path.basename(match.sourcePath);
        const relativeFilePath = path.posix.join(
          "fonts",
          safePathPart(family),
          fileName,
        );
        const destination = path.join(root, relativeFilePath);
        if (path.resolve(match.sourcePath) !== path.resolve(destination)) {
          await copyFile(match.sourcePath, destination);
        }
        copiedFiles.push({
          style: match.style,
          filePath: relativeFilePath,
        });
      }
      upsert.run(
        approvedFamilyId,
        productionId,
        family,
        JSON.stringify({ files: copiedFiles }),
        path.dirname(matches[0]?.sourcePath ?? ""),
        JSON.stringify({
          approvedBy: "approveReferencedProductionFonts",
          approvedAt: new Date().toISOString(),
        }),
      );
      approved.set(`${productionId}:${normalizeFamily(family)}`, approvedFamilyId);
    }
  }

  const updates = {
    themes: database.prepare("UPDATE themes SET tokens_json = ? WHERE id = ?"),
    apps: database.prepare("UPDATE apps SET config_json = ? WHERE id = ?"),
    module_theme_configs: database.prepare(
      "UPDATE module_theme_configs SET tokens_json = ? WHERE id = ?",
    ),
  };
  for (const reference of references) {
    const familyId = approved.get(
      `${reference.productionId}:${normalizeFamily(reference.family)}`,
    );
    if (!familyId) continue;
    const row = database
      .prepare(`SELECT ${reference.column} FROM ${reference.table} WHERE id = ?`)
      .get(reference.id) as Row | undefined;
    if (!row) continue;
    const root = parseJsonObject(row[reference.column]);
    const target = valueAtPath(root, reference.path);
    if (isRecord(target)) {
      target.productionFontId = familyId;
      target.source = "production_font_family";
      setAtPath(root, reference.path, target);
      updates[reference.table].run(JSON.stringify(root), reference.id);
    }
  }

  database.close();
  console.log(
    JSON.stringify(
      {
        approvedFamilies: Array.from(approved.entries()).map(([key, id]) => ({
          key,
          id,
        })),
        missing,
      },
      null,
      2,
    ),
  );
}

void main().catch((error) => {
  console.error(error);
  process.exit(1);
});
