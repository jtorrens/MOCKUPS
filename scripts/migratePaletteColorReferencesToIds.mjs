import Database from "better-sqlite3";

const databasePath = process.argv[2];
if (!databasePath) throw new Error("Usage: node scripts/migratePaletteColorReferencesToIds.mjs <database>");

const database = new Database(databasePath);
const references = [
  ["actors", "id", "project_id", ["metadata_json"]],
  ["themes", "id", "project_id", ["tokens_json", "metadata_json"]],
  ["apps", "id", "project_id", ["config_json", "metadata_json"]],
  ["component_classes", "id", "project_id", ["config_json", "metadata_json", "design_preview_json"]],
  ["devices", "id", "project_id", ["metrics_json"]],
];

function replacePaletteTokens(value, paletteIds) {
  if (typeof value === "string") {
    const direct = paletteIds.get(value);
    if (direct) return { value: direct, changed: true };

    // Palette pairs are a declared scalar ValueKind. Resolve them atomically:
    // a partially-recognized pair is not a palette reference and is left intact.
    const parts = value.split("|");
    if (parts.length === 2 && parts.every((part) => paletteIds.has(part))) {
      return { value: parts.map((part) => paletteIds.get(part)).join("|"), changed: true };
    }
    return { value, changed: false };
  }
  if (Array.isArray(value)) {
    const items = value.map((item) => replacePaletteTokens(item, paletteIds));
    return { value: items.map((item) => item.value), changed: items.some((item) => item.changed) };
  }
  if (value && typeof value === "object") {
    const entries = Object.entries(value).map(([key, item]) => [key, replacePaletteTokens(item, paletteIds)]);
    return {
      value: Object.fromEntries(entries.map(([key, item]) => [key, item.value])),
      changed: entries.some(([, item]) => item.changed),
    };
  }
  return { value, changed: false };
}

database.transaction(() => {
  const paletteByProject = new Map();
  for (const row of database.prepare("SELECT id, project_id, token FROM palette_colors").all()) {
    const entries = paletteByProject.get(row.project_id) ?? new Map();
    if (entries.has(row.token)) throw new Error(`Project '${row.project_id}' has duplicate palette token '${row.token}'.`);
    entries.set(row.token, row.id);
    paletteByProject.set(row.project_id, entries);
  }
  for (const [table, idColumn, projectColumn, columns] of references) {
    const rows = database.prepare(`SELECT ${idColumn} AS id, ${projectColumn} AS projectId, ${columns.join(", ")} FROM ${table}`).all();
    for (const row of rows) {
      const paletteIds = paletteByProject.get(row.projectId) ?? new Map();
      const updated = {};
      let changed = false;
      for (const column of columns) {
        const current = JSON.parse(row[column]);
        const next = replacePaletteTokens(current, paletteIds);
        updated[column] = next.changed ? JSON.stringify(next.value) : row[column];
        changed ||= next.changed;
      }
      if (!changed) continue;
      const assignments = columns.map((column) => `${column} = @${column}`).join(", ");
      database.prepare(`UPDATE ${table} SET ${assignments} WHERE ${idColumn} = @id`).run({ id: row.id, ...updated });
    }
  }
  const moduleRows = database.prepare(
    "SELECT m.id, a.project_id AS projectId, m.config_json, m.metadata_json, m.design_preview_json FROM modules m JOIN apps a ON a.id = m.app_id",
  ).all();
  for (const row of moduleRows) {
    const paletteIds = paletteByProject.get(row.projectId) ?? new Map();
    const updated = {};
    let changed = false;
    for (const column of ["config_json", "metadata_json", "design_preview_json"]) {
      const next = replacePaletteTokens(JSON.parse(row[column]), paletteIds);
      updated[column] = next.changed ? JSON.stringify(next.value) : row[column];
      changed ||= next.changed;
    }
    if (changed) {
      database.prepare("UPDATE modules SET config_json = @config_json, metadata_json = @metadata_json, design_preview_json = @design_preview_json WHERE id = @id")
        .run({ id: row.id, ...updated });
    }
  }
  const shotRows = database.prepare(
    "SELECT s.id, e.project_id AS projectId, s.canvas_json, s.metadata_json FROM shots s JOIN episodes e ON e.id = s.episode_id",
  ).all();
  for (const row of shotRows) {
    const paletteIds = paletteByProject.get(row.projectId) ?? new Map();
    const updated = {};
    let changed = false;
    for (const column of ["canvas_json", "metadata_json"]) {
      const next = replacePaletteTokens(JSON.parse(row[column]), paletteIds);
      updated[column] = next.changed ? JSON.stringify(next.value) : row[column];
      changed ||= next.changed;
    }
    if (changed) {
      database.prepare("UPDATE shots SET canvas_json = @canvas_json, metadata_json = @metadata_json WHERE id = @id")
        .run({ id: row.id, ...updated });
    }
  }
  const moduleInstanceRows = database.prepare(
    "SELECT i.id, e.project_id AS projectId, i.transition_json, i.content_json, i.behavior_json, i.animation_json, i.metadata_json FROM module_instances i JOIN shots s ON s.id = i.shot_id JOIN episodes e ON e.id = s.episode_id",
  ).all();
  for (const row of moduleInstanceRows) {
    const paletteIds = paletteByProject.get(row.projectId) ?? new Map();
    const updated = {};
    let changed = false;
    for (const column of ["transition_json", "content_json", "behavior_json", "animation_json", "metadata_json"]) {
      const next = replacePaletteTokens(JSON.parse(row[column]), paletteIds);
      updated[column] = next.changed ? JSON.stringify(next.value) : row[column];
      changed ||= next.changed;
    }
    if (changed) {
      database.prepare("UPDATE module_instances SET transition_json = @transition_json, content_json = @content_json, behavior_json = @behavior_json, animation_json = @animation_json, metadata_json = @metadata_json WHERE id = @id")
        .run({ id: row.id, ...updated });
    }
  }
})();
database.close();
