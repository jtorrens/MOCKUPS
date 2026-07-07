import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import type { RenderableFontFace } from "../visual/renderable/types.js";
import type {
  DesignPreviewFontFacePayload,
  DesignPreviewPayload,
} from "./designPreviewPayload.js";
import { previewFontFaceFamily } from "./previewFontHelpers.js";
import { asRecord, parseObject } from "./previewJsonHelpers.js";
import { stringValue } from "./previewValueHelpers.js";

export function iconUriForToken(payload: DesignPreviewPayload, token: string) {
  const mapping = parseObject(payload.iconMappingJson ?? "{}");
  const tokens = asRecord(mapping.tokens);
  const iconToken = asRecord(tokens[token]);
  const file = typeof iconToken.file === "string" ? iconToken.file : "";
  const assetRoot = payload.iconAssetRoot?.replace(/\/+$/g, "") ?? "";
  if (!file || !assetRoot) return "";

  const candidates = [
    path.resolve(payload.projectMediaRoot ?? "", assetRoot, file),
    path.resolve("assets/FOQN_S2", assetRoot, file),
    path.resolve("assets", assetRoot, file),
    path.resolve(assetRoot, file),
  ];
  const fullPath = candidates.find((candidate) => existsSync(candidate));
  if (!fullPath) return "";

  const svg = readFileSync(fullPath);
  return `data:image/svg+xml;base64,${svg.toString("base64")}`;
}

export function fontFacesForPayload(
  payload: DesignPreviewPayload,
): RenderableFontFace[] {
  const requirements = fontRequirementsForPayload(payload);
  return (payload.fontFaces ?? []).flatMap((face) => {
    const requirement = requirements.get(face.fontId);
    if (!requirement) return [];
    if (!fontFaceMatches(face, requirement)) return [];

    const fullPath = path.resolve(payload.projectMediaRoot ?? "", face.relativePath);
    if (!existsSync(fullPath)) return [];

    return [{
      family: previewFontFaceFamily(face.fontId),
      uri: fontDataUri(fullPath),
      weight: fontFaceWeight(face),
      style: face.style,
    }];
  });
}

type FontRequirement = {
  weights: Set<number>;
  styles: Set<string>;
};

function fontRequirementsForPayload(payload: DesignPreviewPayload) {
  const requirements = new Map<string, FontRequirement>();
  const themeTypography = asRecord(parseObject(payload.themeTokensJson).typography);
  const themeFontId = stringValue(themeTypography.fontFamilyId).trim();
  const themeEmojiFontId = stringValue(themeTypography.emojiFontFamilyId).trim();
  const themeWeight = numberOrStringValue(themeTypography.weight, 400);
  const themeStyle = fontStyleValue(themeTypography.style);

  if (themeEmojiFontId) {
    addFontRequirement(requirements, themeEmojiFontId, 400, "normal");
  }

  for (const root of [
    parseObject(payload.configJson),
    parseObject(payload.componentBaseConfigsJson ?? "{}"),
  ]) {
    collectTypographyFontRequirements(
      root,
      requirements,
      themeFontId,
      themeWeight,
      themeStyle,
    );
  }

  return requirements;
}

function collectTypographyFontRequirements(
  value: unknown,
  requirements: Map<string, FontRequirement>,
  themeFontId: string,
  themeWeight: number,
  themeStyle: string,
) {
  if (Array.isArray(value)) {
    for (const entry of value) {
      collectTypographyFontRequirements(entry, requirements, themeFontId, themeWeight, themeStyle);
    }
    return;
  }

  if (typeof value !== "object" || value === null) return;
  const record = value as Record<string, unknown>;
  if ("fontFamilyId" in record) {
    const fontId = stringValue(record.fontFamilyId).trim();
    const resolvedFontId = fontId === "theme" ? themeFontId : fontId === "system" ? "" : fontId;
    if (resolvedFontId) {
      addFontRequirement(
        requirements,
        resolvedFontId,
        fontWeightValue(record.weight, themeWeight),
        fontStyleValue(record.style, themeStyle),
      );
    }
  }

  for (const child of Object.values(record)) {
    collectTypographyFontRequirements(child, requirements, themeFontId, themeWeight, themeStyle);
  }
}

function addFontRequirement(
  requirements: Map<string, FontRequirement>,
  fontId: string,
  weight: number,
  style: string,
) {
  const requirement = requirements.get(fontId) ?? {
    weights: new Set<number>(),
    styles: new Set<string>(),
  };
  requirement.weights.add(weight);
  requirement.styles.add(style);
  requirements.set(fontId, requirement);
}

function fontFaceMatches(
  face: DesignPreviewFontFacePayload,
  requirement: FontRequirement,
) {
  return requirement.styles.has(face.style)
    && (requirement.weights.has(face.weight) || isVariableFontFace(face));
}

function fontFaceWeight(face: DesignPreviewFontFacePayload) {
  return isVariableFontFace(face) ? "100 900" : face.weight;
}

function isVariableFontFace(face: DesignPreviewFontFacePayload) {
  return /variablefont/i.test(face.relativePath);
}

function numberOrStringValue(value: unknown, fallback: number) {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value === "string") {
    const parsed = Number(value.replace(",", "."));
    return Number.isFinite(parsed) ? parsed : fallback;
  }
  return fallback;
}

function fontWeightValue(value: unknown, themeWeight: number) {
  return stringValue(value) === "theme.typography.weight"
    ? themeWeight
    : numberOrStringValue(value, themeWeight);
}

function fontStyleValue(value: unknown, fallback = "normal") {
  const style = stringValue(value);
  if (style === "theme.typography.style") return fallback;
  return style === "italic" ? "italic" : "normal";
}

function fontDataUri(fullPath: string) {
  const mimeType = fontMimeType(fullPath);
  const data = readFileSync(fullPath).toString("base64");
  return `data:${mimeType};base64,${data}`;
}

function fontMimeType(fullPath: string) {
  switch (path.extname(fullPath).toLowerCase()) {
    case ".otf":
      return "font/otf";
    case ".ttf":
    case ".ttc":
      return "font/ttf";
    case ".woff":
      return "font/woff";
    case ".woff2":
      return "font/woff2";
    default:
      return "application/octet-stream";
  }
}
