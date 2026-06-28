import type { AppRecord } from "../../api/client.js";
import {
  fontStyleForProductionStyle,
  fontWeightForProductionStyle,
  isVariableFontStyle,
} from "../../../domain/fonts/productionFontNormalization.js";

export interface ProductionFontCatalog {
  families: string[];
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

function faceLabel(face: Pick<ProductionFontFaceOption, "fontWeight" | "fontStyle">) {
  return `${face.fontWeight}${face.fontStyle === "italic" ? " Italic" : ""}`;
}

export function createProductionFontCatalog(
  records: Record<string, AppRecord[]> = {},
): ProductionFontCatalog {
  const facesByFamily = new Map<string, ProductionFontFaceOption[]>();
  const idsByFamily = new Map<string, string>();

  for (const record of records.production_fonts ?? []) {
    const family = typeof record.family === "string" ? record.family : "";
    if (!family) continue;
    idsByFamily.set(family, record.id);
    const filesJson = isRecord(record.files_json) ? record.files_json : {};
    const files = Array.isArray(filesJson.files) ? filesJson.files : [];
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
    facesByFamily,
    idsByFamily,
  };
}

export function productionFontIdForFamily(
  productionFontCatalog: ProductionFontCatalog | undefined,
  family: string,
) {
  return productionFontCatalog?.idsByFamily.get(family);
}

export {
  fontStyleForProductionStyle,
  fontWeightForProductionStyle,
};
