import type { AppRecord, AppTableDefinition } from "../api/client.js";

export interface PaletteTokenUsage {
  tableId: string;
  tableLabel: string;
  recordId: string;
  recordLabel: string;
  field: string;
  count: number;
}

function countTokenReferences(value: unknown, token: string): number {
  if (!token) return 0;
  if (Array.isArray(value)) {
    return value.reduce(
      (total, entry) => total + countTokenReferences(entry, token),
      0,
    );
  }
  if (value && typeof value === "object") {
    return Object.values(value).reduce(
      (total, entry) => total + countTokenReferences(entry, token),
      0,
    );
  }
  return value === token ? 1 : 0;
}

function recordLabel(table: AppTableDefinition | undefined, record: AppRecord) {
  const titleColumn = table?.titleColumn ?? "id";
  return String(record[titleColumn] ?? record.id);
}

function pushUsage({
  usages,
  tablesById,
  tableId,
  record,
  field,
  token,
}: {
  usages: PaletteTokenUsage[];
  tablesById: Map<string, AppTableDefinition>;
  tableId: string;
  record: AppRecord;
  field: string;
  token: string;
}) {
  const count = countTokenReferences(record[field], token);
  if (count === 0) return;
  const table = tablesById.get(tableId);
  usages.push({
    tableId,
    tableLabel: table?.label ?? tableId,
    recordId: record.id,
    recordLabel: recordLabel(table, record),
    field,
    count,
  });
}

export function paletteTokenUsages({
  tables,
  records,
  record,
  token,
}: {
  tables: AppTableDefinition[];
  records: Record<string, AppRecord[]>;
  record: AppRecord | undefined;
  token: string;
}): PaletteTokenUsage[] {
  const productionId =
    typeof record?.production_id === "string" ? record.production_id : "";
  if (!productionId || !token) return [];
  const tablesById = new Map(tables.map((table) => [table.id, table]));
  const usages: PaletteTokenUsage[] = [];
  const themes = (records.themes ?? []).filter(
    (theme) => theme.production_id === productionId,
  );
  const themeIds = new Set(themes.map((theme) => theme.id));
  for (const theme of themes) {
    pushUsage({
      usages,
      tablesById,
      tableId: "themes",
      record: theme,
      field: "tokens_json",
      token,
    });
  }
  for (const app of records.apps ?? []) {
    if (app.production_id !== productionId) continue;
    pushUsage({ usages, tablesById, tableId: "apps", record: app, field: "config_json", token });
    pushUsage({ usages, tablesById, tableId: "apps", record: app, field: "metadata_json", token });
  }
  for (const actor of records.actors ?? []) {
    if (actor.production_id !== productionId) continue;
    pushUsage({ usages, tablesById, tableId: "actors", record: actor, field: "metadata_json", token });
  }
  for (const statusBar of records.status_bars ?? []) {
    if (statusBar.production_id !== productionId) continue;
    pushUsage({ usages, tablesById, tableId: "status_bars", record: statusBar, field: "config_json", token });
  }
  for (const navigationBar of records.navigation_bars ?? []) {
    if (navigationBar.production_id !== productionId) continue;
    pushUsage({ usages, tablesById, tableId: "navigation_bars", record: navigationBar, field: "config_json", token });
  }
  for (const config of records.module_theme_configs ?? []) {
    if (!themeIds.has(String(config.theme_id ?? ""))) continue;
    pushUsage({
      usages,
      tablesById,
      tableId: "module_theme_configs",
      record: config,
      field: "tokens_json",
      token,
    });
    pushUsage({
      usages,
      tablesById,
      tableId: "module_theme_configs",
      record: config,
      field: "metadata_json",
      token,
    });
  }
  return usages;
}

export function paletteTokenUsageCount(usages: PaletteTokenUsage[]) {
  return usages.reduce((total, usage) => total + usage.count, 0);
}
