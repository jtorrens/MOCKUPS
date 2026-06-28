import type { AppRecord } from "../../api/client.js";
import { fontStylesForFamily as fallbackFontStylesForFamily } from "./systemFonts.js";

export interface ProductionFontCatalog {
  families: string[];
  stylesByFamily: Map<string, string[]>;
  idsByFamily: Map<string, string>;
  facesByFamily: Map<string, ProductionFontFaceOption[]>;
}

export interface ProductionFontFaceOption {
  family: string;
  fontWeight: number;
  fontStyle: "normal" | "italic";
  label: string;
  sourceStyle: string;
  variable: boolean;
}

const VARIABLE_FONT_WEIGHT_OPTIONS = [200, 300, 400, 500, 600, 700, 800];

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === "object" && !Array.isArray(value));
}

function isVariableFontStyle(style: string) {
  return /variable|wght/i.test(style);
}

export function fontStyleForProductionStyle(
  style: string,
): "normal" | "italic" {
  return /italic/i.test(style) ? "italic" : "normal";
}

export function fontWeightForProductionStyle(style: string): number {
  const normalized = style.toLowerCase().replace(/[\s_-]+/g, "");
  if (normalized.includes("thin")) return 100;
  if (normalized.includes("extralight") || normalized.includes("ultralight")) {
    return 200;
  }
  if (normalized.includes("light")) return 300;
  if (
    normalized.includes("regular") ||
    normalized.includes("normal") ||
    normalized.includes("book") ||
    normalized.includes("variable")
  ) {
    return 400;
  }
  if (normalized.includes("medium")) return 500;
  if (normalized.includes("semibold") || normalized.includes("demibold")) {
    return 600;
  }
  if (normalized.includes("extrabold") || normalized.includes("ultrabold")) {
    return 800;
  }
  if (normalized.includes("black") || normalized.includes("heavy")) return 900;
  if (normalized.includes("bold")) return 700;
  const numeric = Number(style);
  return Number.isFinite(numeric) ? numeric : 400;
}

function faceLabel(face: Pick<ProductionFontFaceOption, "fontWeight" | "fontStyle">) {
  return `${face.fontWeight}${face.fontStyle === "italic" ? " Italic" : ""}`;
}

export function createProductionFontCatalog(
  records: Record<string, AppRecord[]> = {},
): ProductionFontCatalog {
  const stylesByFamily = new Map<string, string[]>();
  const facesByFamily = new Map<string, ProductionFontFaceOption[]>();
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
    const faces = files.flatMap((file) => {
      if (!isRecord(file)) return [];
      const sourceStyle =
        typeof file.style === "string" && file.style ? file.style : "Regular";
      const fontStyle = fontStyleForProductionStyle(sourceStyle);
      if (isVariableFontStyle(sourceStyle)) {
        return VARIABLE_FONT_WEIGHT_OPTIONS.map((fontWeight) => ({
          family,
          fontWeight,
          fontStyle,
          sourceStyle,
          variable: true,
          label: faceLabel({ fontWeight, fontStyle }),
        }));
      }
      const fontWeight = fontWeightForProductionStyle(sourceStyle);
      return [
        {
          family,
          fontWeight,
          fontStyle,
          sourceStyle,
          variable: false,
          label: faceLabel({ fontWeight, fontStyle }),
        },
      ];
    });
    const uniqueFaces = Array.from(
      new Map(
        faces.map((face) => [
          `${face.fontWeight}:${face.fontStyle}`,
          face,
        ]),
      ).values(),
    ).sort(
      (left, right) =>
        left.fontWeight - right.fontWeight ||
        left.fontStyle.localeCompare(right.fontStyle),
    );
    const expandedStyles = styles.some(isVariableFontStyle)
      ? VARIABLE_FONT_WEIGHT_OPTIONS.map(String)
      : styles;
    stylesByFamily.set(
      family,
      Array.from(new Set(expandedStyles.length ? expandedStyles : ["Regular"])).sort((left, right) =>
        left.localeCompare(right),
      ),
    );
    facesByFamily.set(
      family,
      uniqueFaces.length
        ? uniqueFaces
        : [
            {
              family,
              fontWeight: 400,
              fontStyle: "normal",
              sourceStyle: "Regular",
              variable: false,
              label: "400",
            },
          ],
    );
  }

  return {
    families: Array.from(idsByFamily.keys()).sort((left, right) =>
      left.localeCompare(right),
    ),
    stylesByFamily,
    facesByFamily,
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
