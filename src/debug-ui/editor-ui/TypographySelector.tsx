import { InspectorFieldRow } from "../components/inspector/InspectorFieldRow.js";
import {
  fontStyleForProductionStyle,
  fontWeightForProductionStyle,
  type ProductionFontCatalog,
  type ProductionFontFaceOption,
} from "../components/json-editor/productionFonts.js";

export interface TypographySelection {
  fontFamily: string;
  fontWeight: number;
  fontStyle: "normal" | "italic";
}

export interface TypographySelectorProps {
  catalog?: ProductionFontCatalog;
  compact?: boolean;
  controlClassName?: string;
  inherited?: boolean;
  lockFamily?: boolean;
  value: {
    fontFamily?: unknown;
    fontWeight?: unknown;
    fontStyle?: unknown;
  };
  onChange: (nextValue: TypographySelection) => void;
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

function defaultFace(faces: ProductionFontFaceOption[]) {
  return (
    faces.find((face) => face.fontWeight === 400 && face.fontStyle === "normal") ??
    faces.find((face) => face.fontStyle === "normal") ??
    faces[0]
  );
}

export function TypographySelector({
  catalog,
  compact = false,
  controlClassName = "",
  inherited = false,
  lockFamily = false,
  value,
  onChange,
}: TypographySelectorProps) {
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

  const selectClassName = [
    "json-value-control",
    inherited ? "is-inherited-value" : "",
    controlClassName,
  ]
    .filter(Boolean)
    .join(" ");

  const familySelect = (
    <select
      className={selectClassName}
      style={{ minWidth: 0 }}
      value={currentFamily}
      disabled={lockFamily || !familyOptions.length}
      onChange={(event) => {
        const nextFamily = event.currentTarget.value;
        commitFace(defaultFace(catalog?.facesByFamily.get(nextFamily) ?? []));
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
  );
  const weightSelect = (
    <select
      className={selectClassName}
      style={{ minWidth: 0 }}
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
  );
  const styleSelect = (
    <select
      className={selectClassName}
      style={{ minWidth: 0 }}
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
  );

  if (compact) {
    return (
      <span
        className={controlClassName}
        style={{
          display: "grid",
          gap: 7,
          gridTemplateColumns: "minmax(0, 1fr) 74px 94px",
          minWidth: 0,
          width: "100%",
        }}
      >
        {familySelect}
        {weightSelect}
        {styleSelect}
      </span>
    );
  }

  return (
    <>
      <InspectorFieldRow
        label="Font family"
        control={familySelect}
      />
      <InspectorFieldRow
        label="Font weight"
        control={weightSelect}
      />
      <InspectorFieldRow
        label="Font style"
        control={styleSelect}
      />
    </>
  );
}
