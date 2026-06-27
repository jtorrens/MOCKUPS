import type { AppRecord } from "../../api/client.js";

export interface PaletteColorOption {
  id: string;
  token: string;
  valueHex: string;
  isNeutral: boolean;
}

export interface PaletteColorCatalog {
  colors: PaletteColorOption[];
  byToken: Map<string, PaletteColorOption>;
  byHex: Map<string, PaletteColorOption>;
}

function isPaletteRecord(record: AppRecord): boolean {
  return (
    typeof record.id === "string" &&
    typeof record.token === "string" &&
    typeof record.value_hex === "string"
  );
}

function normalizeHex(value: string) {
  return value.trim().toUpperCase();
}

function hexToRgb(hex: string) {
  const normalized = normalizeHex(hex).replace("#", "");
  return {
    red: Number.parseInt(normalized.slice(0, 2), 16),
    green: Number.parseInt(normalized.slice(2, 4), 16),
    blue: Number.parseInt(normalized.slice(4, 6), 16),
  };
}

function rgbToHsl({
  red,
  green,
  blue,
}: {
  red: number;
  green: number;
  blue: number;
}) {
  const r = red / 255;
  const g = green / 255;
  const b = blue / 255;
  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const lightness = (max + min) / 2;
  const delta = max - min;
  if (delta === 0) {
    return { hue: -1, saturation: 0, lightness };
  }
  const saturation =
    lightness > 0.5 ? delta / (2 - max - min) : delta / (max + min);
  const hue =
    max === r
      ? (g - b) / delta + (g < b ? 6 : 0)
      : max === g
        ? (b - r) / delta + 2
        : (r - g) / delta + 4;
  return { hue: hue * 60, saturation, lightness };
}

function paletteSortKey(color: PaletteColorOption) {
  const hsl = rgbToHsl(hexToRgb(color.valueHex));
  const neutralBand = hsl.saturation < 0.08 ? 0 : 1;
  return {
    neutralBand,
    hue: hsl.hue < 0 ? 361 : hsl.hue,
    saturation: hsl.saturation,
    lightness: hsl.lightness,
    token: color.token,
  };
}

export function createPaletteColorCatalog(
  records: Record<string, AppRecord[]> = {},
  productionId?: string,
): PaletteColorCatalog {
  const colors = (records.palette_colors ?? [])
    .filter(isPaletteRecord)
    .filter(
      (record) =>
        !productionId ||
        typeof record.production_id !== "string" ||
        record.production_id === productionId,
    )
    .map((record) => ({
      id: record.id,
      token: String(record.token),
      valueHex: normalizeHex(String(record.value_hex)),
      isNeutral:
        record.is_neutral === true ||
        record.is_neutral === 1 ||
        record.is_neutral === "1",
    }))
    .sort((left, right) => {
      const leftKey = paletteSortKey(left);
      const rightKey = paletteSortKey(right);
      if (leftKey.neutralBand !== rightKey.neutralBand) {
        return leftKey.neutralBand - rightKey.neutralBand;
      }
      if (leftKey.neutralBand === 0) {
        return (
          leftKey.lightness - rightKey.lightness ||
          leftKey.token.localeCompare(rightKey.token)
        );
      }
      return (
        leftKey.hue - rightKey.hue ||
        leftKey.lightness - rightKey.lightness ||
        rightKey.saturation - leftKey.saturation ||
        leftKey.token.localeCompare(rightKey.token)
      );
    });

  return {
    colors,
    byToken: new Map(colors.map((color) => [color.token, color])),
    byHex: new Map(colors.map((color) => [color.valueHex, color])),
  };
}
