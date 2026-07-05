type JsonRecord = Record<string, unknown>;

function asRecord(value: unknown): JsonRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as JsonRecord)
    : {};
}

export function mergeComponentDefaults(
  defaults: JsonRecord,
  overrides: JsonRecord,
): JsonRecord {
  const merged: JsonRecord = { ...defaults };
  for (const [key, value] of Object.entries(overrides)) {
    const defaultValue = merged[key];
    merged[key] =
      typeof defaultValue === "object" &&
      defaultValue !== null &&
      !Array.isArray(defaultValue) &&
      typeof value === "object" &&
      value !== null &&
      !Array.isArray(value)
        ? mergeComponentDefaults(asRecord(defaultValue), asRecord(value))
        : value;
  }
  return merged;
}

export function defaultLabelComponentConfig(): JsonRecord {
  return {
    style: {
      shadowEnabled: false,
      reliefEnabled: false,
      borderWidth: 0,
      borderColorToken: "theme.borders.primary",
      cornerRadiusToken: "theme.radii.surface",
      reliefAngle: -45,
      reliefExtent: 1,
      reliefSpread: 0,
      reliefTopIntensity: 0.12,
      reliefBottomIntensity: -0.1,
    },
    label: {
      dimensionMode: "content",
      size: "120|32",
      padding: "8|4",
      backgroundColorToken: "theme.colors.background",
      alpha: 1,
      textColorToken: "theme.colors.textPrimary",
      textSizeToken: "theme.typography.sizes.s",
      textStyle: "normal",
      textAlign: "center",
      textGap: 2,
      subtextColorToken: "theme.colors.textSecondary",
      subtextSizeToken: "theme.typography.sizes.xs",
      subtextStyle: "normal",
    },
  };
}
