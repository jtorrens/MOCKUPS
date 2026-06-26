import type { AppRecord } from "../../api/client.js";
import { fontStylesForFamily as fallbackFontStylesForFamily } from "./systemFonts.js";

export interface ProductionFontCatalog {
  families: string[];
  stylesByFamily: Map<string, string[]>;
  idsByFamily: Map<string, string>;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === "object" && !Array.isArray(value));
}

export function createProductionFontCatalog(
  records: Record<string, AppRecord[]> = {},
): ProductionFontCatalog {
  const stylesByFamily = new Map<string, string[]>();
  const idsByFamily = new Map<string, string>();

  for (const record of records.production_fonts ?? []) {
    const family = typeof record.family === "string" ? record.family : "";
    if (!family) continue;
    idsByFamily.set(family, record.id);
    const filesJson = isRecord(record.files_json) ? record.files_json : {};
    const files = Array.isArray(filesJson.files) ? filesJson.files : [];
    const styles = files
      .map((file) =>
        isRecord(file) && typeof file.style === "string" ? file.style : "",
      )
      .filter(Boolean);
    stylesByFamily.set(
      family,
      Array.from(new Set(styles.length ? styles : ["Regular"])).sort((left, right) =>
        left.localeCompare(right),
      ),
    );
  }

  return {
    families: Array.from(idsByFamily.keys()).sort((left, right) =>
      left.localeCompare(right),
    ),
    stylesByFamily,
    idsByFamily,
  };
}

export function fontStylesForFamily(
  productionFontCatalog: ProductionFontCatalog | undefined,
  fallbackStylesByFamily: Map<string, string[]>,
  family: string | undefined,
) {
  if (family && productionFontCatalog?.stylesByFamily.has(family)) {
    return productionFontCatalog.stylesByFamily.get(family) ?? ["Regular"];
  }
  return fallbackFontStylesForFamily(fallbackStylesByFamily, family);
}

export function productionFontIdForFamily(
  productionFontCatalog: ProductionFontCatalog | undefined,
  family: string,
) {
  return productionFontCatalog?.idsByFamily.get(family);
}
