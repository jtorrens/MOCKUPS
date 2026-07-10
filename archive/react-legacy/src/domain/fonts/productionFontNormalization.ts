export function isVariableFontStyle(style: string) {
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

export function fontWeightRangeForProductionStyle(
  style: string,
): number | string {
  return isVariableFontStyle(style) ? "200 800" : fontWeightForProductionStyle(style);
}
