import type { AppRecord, AppTableDefinition } from "../api/client.js";

export function productionIdForRecord({
  table,
  record,
  records,
}: {
  table: AppTableDefinition;
  record: AppRecord | undefined;
  records: Record<string, AppRecord[]>;
}) {
  if (!record) return "";
  if (table.id === "productions") return record.id;
  if (typeof record.production_id === "string") return record.production_id;
  if (table.id === "module_instances") {
    const screen = records.screen_instances?.find(
      (item) => item.id === record.screen_instance_id,
    );
    const shot = records.shots?.find((item) => item.id === screen?.shot_id);
    return typeof shot?.production_id === "string" ? shot.production_id : "";
  }
  if (table.id === "screen_instances") {
    const shot = records.shots?.find((item) => item.id === record.shot_id);
    return typeof shot?.production_id === "string" ? shot.production_id : "";
  }
  return "";
}

export function productionMediaRootForRecord({
  table,
  record,
  records,
}: {
  table: AppTableDefinition;
  record: AppRecord | undefined;
  records: Record<string, AppRecord[]>;
}) {
  const production = records.productions?.find(
    (item) => item.id === productionIdForRecord({ table, record, records }),
  );
  const settings = production?.settings_json;
  if (!settings || typeof settings !== "object" || Array.isArray(settings)) {
    return "";
  }
  const mediaRoot = (settings as Record<string, unknown>).mediaRoot;
  return typeof mediaRoot === "string" ? mediaRoot : "";
}
