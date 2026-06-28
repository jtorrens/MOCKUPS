import type { AppRecord, AppTableDefinition } from "../api/client.js";

export interface ProductionFontUsage {
  tableId: string;
  tableLabel: string;
  recordId: string;
  recordLabel: string;
  field: string;
  count: number;
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function countFontReferences(
  value: unknown,
  {
    family,
    productionFontId,
    key,
  }: { family: string; productionFontId: string; key?: string },
): number {
  if (Array.isArray(value)) {
    return value.reduce(
      (total, entry) =>
        total + countFontReferences(entry, { family, productionFontId }),
      0,
    );
  }
  if (isObject(value)) {
    return Object.entries(value).reduce(
      (total, [entryKey, entryValue]) =>
        total +
        countFontReferences(entryValue, {
          family,
          productionFontId,
          key: entryKey,
        }),
      0,
    );
  }
  if (key === "productionFontId" && value === productionFontId) return 1;
  if ((key === "fontFamily" || key === "family") && value === family) return 1;
  return 0;
}

function recordLabel(table: AppTableDefinition | undefined, record: AppRecord) {
  const titleColumn = table?.titleColumn ?? "id";
  return String(record[titleColumn] ?? record.id);
}

function belongsToProduction({
  productionId,
  record,
  screenIds,
  shotIds,
  themeIds,
  tableId,
}: {
  productionId: string;
  record: AppRecord;
  screenIds: Set<string>;
  shotIds: Set<string>;
  themeIds: Set<string>;
  tableId: string;
}) {
  if (record.production_id === productionId) return true;
  if (tableId === "screen_instances") {
    return shotIds.has(String(record.shot_id ?? ""));
  }
  if (tableId === "module_instances") {
    return screenIds.has(String(record.screen_instance_id ?? ""));
  }
  if (tableId === "module_theme_configs") {
    return themeIds.has(String(record.theme_id ?? ""));
  }
  return false;
}

export function productionFontUsages({
  tables,
  records,
  record,
}: {
  tables: AppTableDefinition[];
  records: Record<string, AppRecord[]>;
  record: AppRecord | undefined;
}): ProductionFontUsage[] {
  const productionId =
    typeof record?.production_id === "string" ? record.production_id : "";
  const family = typeof record?.family === "string" ? record.family : "";
  const productionFontId = record?.id ?? "";
  if (!productionId || !family || !productionFontId) return [];

  const tablesById = new Map(tables.map((table) => [table.id, table]));
  const shotIds = new Set(
    (records.shots ?? [])
      .filter((shot) => shot.production_id === productionId)
      .map((shot) => shot.id),
  );
  const screenIds = new Set(
    (records.screen_instances ?? [])
      .filter((screen) => shotIds.has(String(screen.shot_id ?? "")))
      .map((screen) => screen.id),
  );
  const themeIds = new Set(
    (records.themes ?? [])
      .filter((theme) => theme.production_id === productionId)
      .map((theme) => theme.id),
  );
  const usages: ProductionFontUsage[] = [];

  for (const table of tables) {
    if (table.id === "production_fonts") continue;
    for (const candidate of records[table.id] ?? []) {
      if (
        !belongsToProduction({
          productionId,
          record: candidate,
          screenIds,
          shotIds,
          themeIds,
          tableId: table.id,
        })
      ) {
        continue;
      }
      for (const field of table.jsonFields) {
        const count = countFontReferences(candidate[field], {
          family,
          productionFontId,
        });
        if (count === 0) continue;
        usages.push({
          tableId: table.id,
          tableLabel: table.label,
          recordId: candidate.id,
          recordLabel: recordLabel(table, candidate),
          field,
          count,
        });
      }
    }
  }

  return usages;
}

export function productionFontUsageCount(usages: ProductionFontUsage[]) {
  return usages.reduce((total, usage) => total + usage.count, 0);
}
