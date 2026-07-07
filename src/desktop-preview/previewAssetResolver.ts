import { existsSync, readFileSync, statSync } from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";
import type { RenderableFontFace } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { asRecord, parseObject } from "./previewJsonHelpers.js";

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
  return (payload.fontFaces ?? []).flatMap((face) => {
    const fullPath = path.resolve(payload.projectMediaRoot ?? "", face.relativePath);
    if (!existsSync(fullPath)) return [];

    return [{
      family: face.family,
      uri: `${pathToFileURL(fullPath).href}?v=${fontVersion(fullPath)}`,
      weight: face.weight,
      style: face.style,
    }];
  });
}

function fontVersion(fullPath: string) {
  try {
    return Math.trunc(statSync(fullPath).mtimeMs).toString(36);
  } catch {
    return "0";
  }
}
