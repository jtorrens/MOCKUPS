import { InspectorFieldRow } from "../inspector/InspectorFieldRow.js";
import {
  fontStyleForProductionStyle,
  fontWeightForProductionStyle,
  type ProductionFontCatalog,
  type ProductionFontFaceOption,
} from "./productionFonts.js";

export interface ProductionFontSelection {
  fontFamily: string;
  fontWeight: number;
  fontStyle: "normal" | "italic";
}

interface ProductionFontSelectorProps {
  catalog?: ProductionFontCatalog;
  value: {
    fontFamily?: unknown;
    fontWeight?: unknown;
    fontStyle?: unknown;
  };
  onChange: (nextValue: ProductionFontSelection) => void;
}

function stringValue(value: unknown, fallback = "") {
  return typeof value === "string" && value ? value : fallback;
}

function numericFontWeight(value: unknown) {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) return parsed;
    return fontWeightForProductionStyle(value);
  }
  return 400;
}

function cssFontStyle(value: unknown, weightValue: unknown): "normal" | "italic" {
  if (value === "italic" || value === "normal") return value;
  if (typeof weightValue === "string") {
    return fontStyleForProductionStyle(weightValue);
  }
  return "normal";
}

function uniqueWeights(faces: ProductionFontFaceOption[]) {
  return Array.from(new Set(faces.map((face) => face.fontWeight))).sort(
    (left, right) => left - right,
  );
}

function uniqueStyles(faces: ProductionFontFaceOption[], weight: number) {
  return Array.from(
    new Set(
      faces
        .filter((face) => face.fontWeight === weight)
        .map((face) => face.fontStyle),
    ),
  ).sort((left, right) => left.localeCompare(right));
}

function closestFace(
  faces: ProductionFontFaceOption[],
  requestedWeight: number,
  requestedStyle: "normal" | "italic",
) {
  return (
    faces.find(
      (face) =>
        face.fontWeight === requestedWeight && face.fontStyle === requestedStyle,
    ) ??
    faces.find((face) => face.fontWeight === requestedWeight) ??
    faces[0]
  );
}

export function ProductionFontSelector({
  catalog,
  value,
  onChange,
}: ProductionFontSelectorProps) {
  const families = catalog?.families ?? [];
  const currentFamily = stringValue(value.fontFamily, families[0] ?? "");
  const familyOptions = families.includes(currentFamily)
    ? families
    : currentFamily
      ? [currentFamily, ...families]
      : families;
  const faces = catalog?.facesByFamily.get(currentFamily) ?? [];
  const fallbackFace: ProductionFontFaceOption = {
    family: currentFamily,
    fontWeight: numericFontWeight(value.fontWeight),
    fontStyle: cssFontStyle(value.fontStyle, value.fontWeight),
    label: "",
    sourceStyle: "",
    variable: false,
  };
  const currentFace =
    closestFace(
      faces,
      numericFontWeight(value.fontWeight),
      cssFontStyle(value.fontStyle, value.fontWeight),
    ) ?? fallbackFace;
  const weightOptions = uniqueWeights(faces).length
    ? uniqueWeights(faces)
    : [currentFace.fontWeight];
  const styleOptions = uniqueStyles(faces, currentFace.fontWeight).length
    ? uniqueStyles(faces, currentFace.fontWeight)
    : [currentFace.fontStyle];

  function commitFace(face: ProductionFontFaceOption | undefined) {
    if (!face) return;
    onChange({
      fontFamily: face.family,
      fontWeight: face.fontWeight,
      fontStyle: face.fontStyle,
    });
  }

  return (
    <>
      <InspectorFieldRow
        label="Font family"
        control={
          <select
            className="json-value-control"
            value={currentFamily}
            disabled={!familyOptions.length}
            onChange={(event) => {
              const nextFamily = event.currentTarget.value;
              commitFace(catalog?.facesByFamily.get(nextFamily)?.[0]);
            }}
          >
            {familyOptions.length ? (
              familyOptions.map((family) => (
                <option key={family} value={family}>
                  {family}
                </option>
              ))
            ) : (
              <option value="">No production fonts</option>
            )}
          </select>
        }
      />
      <InspectorFieldRow
        label="Font weight"
        control={
          <select
            className="json-value-control"
            value={String(currentFace.fontWeight)}
            disabled={!weightOptions.length}
            onChange={(event) => {
              const nextWeight = Number(event.currentTarget.value);
              commitFace(
                closestFace(faces, nextWeight, currentFace.fontStyle) ?? {
                  ...currentFace,
                  fontWeight: nextWeight,
                },
              );
            }}
          >
            {weightOptions.map((weight) => (
              <option key={weight} value={weight}>
                {weight}
              </option>
            ))}
          </select>
        }
      />
      <InspectorFieldRow
        label="Font style"
        control={
          <select
            className="json-value-control"
            value={currentFace.fontStyle}
            disabled={!styleOptions.length}
            onChange={(event) => {
              const nextStyle = event.currentTarget.value as "normal" | "italic";
              commitFace(
                closestFace(faces, currentFace.fontWeight, nextStyle) ?? {
                  ...currentFace,
                  fontStyle: nextStyle,
                },
              );
            }}
          >
            {styleOptions.map((style) => (
              <option key={style} value={style}>
                {style === "italic" ? "Italic" : "Normal"}
              </option>
            ))}
          </select>
        }
      />
    </>
  );
}
