import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { appendFileSync, existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import type { RenderableFontFace } from "../visual/renderable/types.js";
import type {
  DesignPreviewFontFacePayload,
  DesignPreviewPayload,
} from "./designPreviewPayload.js";
import { previewFontFaceFamily } from "./previewFontHelpers.js";
import { parseObject } from "./previewJsonHelpers.js";
import {
  requiredNumberValue,
  requiredRecord,
  requiredString,
  stringValue,
} from "./previewValueHelpers.js";

const videoFrameCache = new Map<string, string>();
const lastVideoFrameByAsset = new Map<string, string>();
const videoDurationCache = new Map<string, number>();
const videoIdentityByPath = new Map<string, string>();
const maxVideoFrameCacheEntries = 240;
const emojiRasterCache = new Map<string, { uri: string; width: number; height: number }>();

export function emojiRasterDataUri(text: string, fontSize: number) {
  const pixelScale = 4;
  const glyphCount = Math.max(1, [...text].filter((character) => character !== "\ufe0f").length);
  const width = Math.ceil(Math.max(fontSize * 1.2, glyphCount * fontSize * 1.28));
  const height = Math.ceil(fontSize * 1.28);
  const cacheKey = `${text}\u001f${fontSize}`;
  const cached = emojiRasterCache.get(cacheKey);
  if (cached) return cached;
  const directory = path.join(os.tmpdir(), "mockups-emoji-raster");
  mkdirSync(directory, { recursive: true });
  const identity = createHash("sha256").update(cacheKey).digest("hex");
  const sourcePath = path.join(directory, `${identity}.svg`);
  const outputPath = path.join(directory, `${identity}.png`);
  if (!existsSync(outputPath)) {
    const pixelWidth = width * pixelScale;
    const pixelHeight = height * pixelScale;
    writeFileSync(sourcePath, `<svg xmlns="http://www.w3.org/2000/svg" width="${pixelWidth}" height="${pixelHeight}"><text x="0" y="${fontSize * pixelScale}" font-family="Apple Color Emoji" font-size="${fontSize * pixelScale}">${escapeXml(text)}</text></svg>`);
    execFileSync("/usr/bin/sips", ["-s", "format", "png", sourcePath, "--out", outputPath], { stdio: "ignore" });
  }
  const result = { uri: `data:image/png;base64,${readFileSync(outputPath).toString("base64")}`, width, height };
  emojiRasterCache.set(cacheKey, result);
  return result;
}

function escapeXml(value: string) {
  return value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

export function iconUriForToken(payload: DesignPreviewPayload, token: string) {
  const systemIcon = systemIconUri(token);
  if (systemIcon) return systemIcon;

  if (payload.iconMappingJson === undefined) return "";
  const mapping = parseObject(payload.iconMappingJson, "icon mapping");
  const tokens = requiredRecord(mapping, "tokens", "icon mapping.tokens");
  if (!Object.hasOwn(tokens, token)) return "";
  const iconToken = requiredRecord(tokens, token, `icon mapping.tokens.${token}`);
  const file = requiredString(iconToken, "file", `icon mapping.tokens.${token}.file`);
  if (!/^[^/\\]+\.svg$/i.test(file)) {
    throw new Error(`Invalid local SVG file ${file} for icon token ${token}`);
  }
  if (typeof payload.iconAssetRoot !== "string" || !payload.iconAssetRoot.trim()) {
    throw new Error(`Missing Icon Theme asset root for token ${token}`);
  }
  const assetRoot = payload.iconAssetRoot.replace(/\/+$/g, "");

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

function systemIconUri(token: string) {
  if (!/^system_[a-z0-9_]+$/i.test(token)) return "";

  const file = `${token}.svg`;
  const candidates = [
    path.resolve("assets/system/system_icons", file),
    path.resolve("assets", "system", "system_icons", file),
  ];
  const fullPath = candidates.find((candidate) => existsSync(candidate));
  if (!fullPath) return "";

  const svg = readFileSync(fullPath);
  return `data:image/svg+xml;base64,${svg.toString("base64")}`;
}

export function mediaFrameUriForPath(
  payload: DesignPreviewPayload,
  source: string,
  timeSeconds: number,
) {
  const trimmed = source.trim();
  if (!trimmed) return { uri: "", error: "No media source" };
  if (/^data:image\//i.test(trimmed)) return { uri: trimmed };
  if (/^data:/i.test(trimmed)) {
    return { uri: "", error: "Unsupported data URI media source" };
  }
  if (/^https?:/i.test(trimmed)) {
    return videoMimeType(trimmed)
      ? { uri: "", error: "Remote video frame extraction is not supported" }
      : { uri: trimmed };
  }

  if (/^file:/i.test(trimmed)) {
    return localMediaFrameUri(fileURLToPath(trimmed), timeSeconds);
  }

  const candidates = mediaSourceCandidates(payload.projectMediaRoot ?? "", trimmed);
  const fullPath = candidates.find((candidate) => existsSync(candidate));
  return fullPath
    ? localMediaFrameUri(fullPath, timeSeconds)
    : { uri: "", error: `Media source not found: ${trimmed}` };
}

function mediaSourceCandidates(projectMediaRoot: string, source: string) {
  if (path.isAbsolute(source)) return [source];

  const candidates = [
    path.resolve(projectMediaRoot, source),
    path.resolve(source),
  ];
  const stripped = stripDuplicatedMediaRootPrefix(projectMediaRoot, source);
  if (stripped && stripped !== source) {
    candidates.unshift(path.resolve(projectMediaRoot, stripped));
  }

  return [...new Set(candidates)];
}

function stripDuplicatedMediaRootPrefix(projectMediaRoot: string, source: string) {
  const rootParts = normalizePathParts(projectMediaRoot);
  const sourceParts = normalizePathParts(source);
  const max = Math.min(rootParts.length, sourceParts.length);
  for (let length = max; length > 0; length -= 1) {
    const rootSuffix = rootParts.slice(rootParts.length - length);
    const sourcePrefix = sourceParts.slice(0, length);
    if (rootSuffix.every((part, index) => part === sourcePrefix[index])) {
      return sourceParts.slice(length).join("/");
    }
  }

  return source;
}

function normalizePathParts(value: string) {
  return value
    .replace(/\\/g, "/")
    .split("/")
    .filter(Boolean);
}

function imageDataUri(fullPath: string) {
  const mimeType = imageMimeType(fullPath);
  if (!mimeType) return undefined;

  const data = readFileSync(fullPath);
  return `data:${mimeType};base64,${data.toString("base64")}`;
}

function localMediaFrameUri(fullPath: string, timeSeconds: number) {
  const imageUri = imageDataUri(fullPath);
  if (imageUri) return { uri: imageUri };

  if (!videoMimeType(fullPath)) {
    return { uri: "", error: `Unsupported media file type: ${path.extname(fullPath)}` };
  }

  return videoFrameFileUri(fullPath, timeSeconds);
}

function videoFrameFileUri(fullPath: string, timeSeconds: number) {
  const assetIdentity = currentVideoAssetIdentity(fullPath);
  const normalizedTime = Math.max(0, Number.isFinite(timeSeconds) ? timeSeconds : 0);
  const duration = videoDurationSeconds(fullPath, assetIdentity);
  const effectiveTime =
    duration > 0 ? Math.min(normalizedTime, Math.max(0, duration - 0.001)) : normalizedTime;
  const cacheKey = `${assetIdentity}#${effectiveTime.toFixed(3)}`;
  const cached = videoFrameCache.get(cacheKey);
  if (cached) {
    debugVideoFrame("cache-hit", {
      source: fullPath,
      requested: normalizedTime,
      effective: effectiveTime,
      duration,
      uriChars: cached.length,
    });
    return { uri: cached };
  }

  try {
    const framePath = cachedVideoFramePath(assetIdentity, effectiveTime);
    const hadFrame = existingNonEmptyFile(framePath);
    if (!existingNonEmptyFile(framePath)) {
      mkdirSync(path.dirname(framePath), { recursive: true });
      execFileSync(
        ffmpegExecutable(),
        [
          "-hide_banner",
          "-loglevel",
          "error",
          "-ss",
          effectiveTime.toFixed(3),
          "-i",
          fullPath,
          "-frames:v",
          "1",
          "-q:v",
          "4",
          "-y",
          framePath,
        ],
        {
          maxBuffer: 8 * 1024 * 1024,
        },
      );
    }

    if (!existingNonEmptyFile(framePath)) {
      debugVideoFrame("empty", {
        source: fullPath,
        requested: normalizedTime,
        effective: effectiveTime,
        duration,
        framePath,
      });
      return lastVideoFrameOrError(
        assetIdentity,
        `No video frame at ${effectiveTime.toFixed(3)}s`,
      );
    }

    const uri = imageDataUri(framePath);
    if (!uri) {
      debugVideoFrame("unsupported", {
        source: fullPath,
        requested: normalizedTime,
        effective: effectiveTime,
        duration,
        framePath,
        bytes: fileSize(framePath),
      });
      return lastVideoFrameOrError(
        assetIdentity,
        `Unsupported extracted video frame: ${framePath}`,
      );
    }

    cacheVideoFrame(cacheKey, uri);
    lastVideoFrameByAsset.set(assetIdentity, uri);
    debugVideoFrame(hadFrame ? "disk-hit" : "extract", {
      source: fullPath,
      requested: normalizedTime,
      effective: effectiveTime,
      duration,
      framePath,
      bytes: fileSize(framePath),
      uriChars: uri.length,
    });
    return { uri };
  } catch (error) {
    const message = videoFrameErrorMessage(error);
    debugVideoFrame("error", {
      source: fullPath,
      requested: normalizedTime,
      effective: effectiveTime,
      duration,
      error: message,
    });
    return lastVideoFrameOrError(
      assetIdentity,
      `Video frame extraction failed: ${message}`,
    );
  }
}

function lastVideoFrameOrError(assetIdentity: string, error: string) {
  const lastFrame = lastVideoFrameByAsset.get(assetIdentity);
  debugVideoFrame(lastFrame ? "last-frame" : "missing", {
    source: assetIdentity,
    error,
    uriChars: lastFrame?.length ?? 0,
  });
  return lastFrame ? { uri: lastFrame, error } : { uri: "", error };
}

function videoDurationSeconds(fullPath: string, assetIdentity: string) {
  const cached = videoDurationCache.get(assetIdentity);
  if (cached !== undefined) return cached;

  try {
    const output = execFileSync(
      ffprobeExecutable(),
      [
        "-v",
        "error",
        "-show_entries",
        "format=duration",
        "-of",
        "default=noprint_wrappers=1:nokey=1",
        fullPath,
      ],
      {
        encoding: "utf8",
        maxBuffer: 1024 * 1024,
        timeout: 5000,
      },
    );
    const duration = Number.parseFloat(output.trim());
    const safeDuration = Number.isFinite(duration) && duration > 0 ? duration : 0;
    videoDurationCache.set(assetIdentity, safeDuration);
    return safeDuration;
  } catch {
    videoDurationCache.set(assetIdentity, 0);
    return 0;
  }
}

function cachedVideoFramePath(assetIdentity: string, timeSeconds: number) {
  const key = `${assetIdentity}#${timeSeconds.toFixed(3)}`;
  const hash = createHash("sha1").update(key).digest("hex");
  return path.join(os.tmpdir(), "mockups-video-frames", `${hash}.jpg`);
}

function currentVideoAssetIdentity(fullPath: string) {
  const stats = statSync(fullPath);
  const normalizedPath = path.resolve(fullPath);
  const identity = `${normalizedPath}|${stats.size}|${stats.mtimeMs}|${stats.ctimeMs}`;
  const previousIdentity = videoIdentityByPath.get(normalizedPath);
  if (previousIdentity && previousIdentity !== identity) {
    videoDurationCache.delete(previousIdentity);
    lastVideoFrameByAsset.delete(previousIdentity);
    for (const key of videoFrameCache.keys()) {
      if (key.startsWith(`${previousIdentity}#`)) videoFrameCache.delete(key);
    }
  }
  videoIdentityByPath.set(normalizedPath, identity);
  return identity;
}

function existingNonEmptyFile(fullPath: string) {
  try {
    return existsSync(fullPath) && statSync(fullPath).size > 0;
  } catch {
    return false;
  }
}

function fileSize(fullPath: string) {
  try {
    return statSync(fullPath).size;
  } catch {
    return 0;
  }
}

function debugVideoFrame(event: string, details: Record<string, unknown>) {
  try {
    mkdirSync("logs", { recursive: true });
    const fields = Object.entries(details)
      .map(([key, value]) => `${key}=${debugValue(value)}`)
      .join("\t");
    appendFileSync(
      process.env.MOCKUPS_PREVIEW_DEBUG_LOG || path.resolve("logs", "desktop-preview-debug.log"),
      `${new Date().toISOString()}\tpreview.asset.video-frame\tevent=${event}\t${fields}\n`,
    );
  } catch {
  }
}

function debugValue(value: unknown) {
  if (typeof value === "number") return Number.isFinite(value) ? value.toFixed(3) : String(value);
  if (typeof value === "string") return JSON.stringify(value);
  return JSON.stringify(value);
}

function videoFrameErrorMessage(error: unknown) {
  if (isExecError(error) && Buffer.isBuffer(error.stderr) && error.stderr.length > 0) {
    return oneLine(error.stderr.toString("utf8"));
  }

  return oneLine(error instanceof Error ? error.message : String(error));
}

function isExecError(error: unknown): error is { stderr?: unknown } {
  return typeof error === "object" && error !== null && "stderr" in error;
}

function oneLine(value: string) {
  return value.replace(/\s+/g, " ").trim().slice(0, 180);
}

function cacheVideoFrame(key: string, uri: string) {
  if (videoFrameCache.size >= maxVideoFrameCacheEntries) {
    const firstKey = videoFrameCache.keys().next().value;
    if (firstKey) videoFrameCache.delete(firstKey);
  }

  videoFrameCache.set(key, uri);
}

function ffmpegExecutable() {
  const candidates = [
    process.env.MOCKUPS_FFMPEG,
    process.env.FFMPEG_PATH,
    "/opt/homebrew/bin/ffmpeg",
    "/usr/local/bin/ffmpeg",
    "/usr/bin/ffmpeg",
    "ffmpeg",
    "ffmpeg.exe",
  ].filter(Boolean) as string[];

  return candidates.find((candidate) =>
    path.isAbsolute(candidate) ? existsSync(candidate) : true,
  ) ?? "ffmpeg";
}

function ffprobeExecutable() {
  const candidates = [
    process.env.MOCKUPS_FFPROBE,
    process.env.FFPROBE_PATH,
    "/opt/homebrew/bin/ffprobe",
    "/usr/local/bin/ffprobe",
    "/usr/bin/ffprobe",
    "ffprobe",
    "ffprobe.exe",
  ].filter(Boolean) as string[];

  return candidates.find((candidate) =>
    path.isAbsolute(candidate) ? existsSync(candidate) : true,
  ) ?? "ffprobe";
}

function imageMimeType(fullPath: string) {
  const extension = path.extname(fullPath).toLowerCase();
  if (extension === ".png") return "image/png";
  if (extension === ".jpg" || extension === ".jpeg") return "image/jpeg";
  if (extension === ".webp") return "image/webp";
  if (extension === ".gif") return "image/gif";
  if (extension === ".heic") return "image/heic";
  if (extension === ".heif") return "image/heif";
  return "";
}

function videoMimeType(fullPath: string) {
  const extension = path.extname(fullPath).toLowerCase();
  if (extension === ".mp4") return "video/mp4";
  if (extension === ".mov") return "video/quicktime";
  if (extension === ".m4v") return "video/x-m4v";
  if (extension === ".webm") return "video/webm";
  return "";
}

export function fontFacesForPayload(
  payload: DesignPreviewPayload,
): RenderableFontFace[] {
  const requirements = fontRequirementsForPayload(payload);
  const resolved: RenderableFontFace[] = [];
  for (const [fontId, requirement] of requirements) {
    const matchingFaces = (payload.fontFaces ?? []).filter((face) =>
      face.fontId === fontId && fontFaceMatches(face, requirement));
    if (matchingFaces.length === 0) {
      throw new Error(`Required production font face is unavailable: ${fontId}`);
    }

    for (const face of matchingFaces) {
      const fullPath = path.resolve(payload.projectMediaRoot ?? "", face.relativePath);
      if (!existsSync(fullPath)) {
        throw new Error(`Required production font file is missing: ${face.relativePath}`);
      }
      resolved.push({
        family: previewFontFaceFamily(face.fontId),
        uri: fontFileUri(fullPath),
        weight: fontFaceWeight(face),
        style: face.style,
      });
    }
  }
  return resolved;
}

type FontRequirement = {
  weights: Set<number>;
  styles: Set<string>;
};

function fontRequirementsForPayload(payload: DesignPreviewPayload) {
  const requirements = new Map<string, FontRequirement>();
  const themeRoot = parseObject(payload.themeTokensJson);
  const themeTypography = requiredRecord(themeRoot, "typography", "theme.typography");
  const themeFontId = requiredString(
    themeTypography,
    "fontFamilyId",
    "theme.typography.fontFamilyId",
  );
  const themeSystemFontId = requiredString(
    themeTypography,
    "systemFontFamilyId",
    "theme.typography.systemFontFamilyId",
  );
  const themeEmojiFontId = requiredString(
    themeTypography,
    "emojiFontFamilyId",
    "theme.typography.emojiFontFamilyId",
  );
  const themeWeight = requiredNumberValue(
    themeTypography.weight,
    "theme.typography.weight",
  );
  const themeStyle = requiredThemeFontStyle(
    themeTypography.style,
    "theme.typography.style",
  );

  if (themeFontId) {
    addFontRequirement(requirements, themeFontId, themeWeight, themeStyle);
  }
  if (themeSystemFontId) {
    addFontRequirement(requirements, themeSystemFontId, themeWeight, themeStyle);
  }
  if (themeEmojiFontId) {
    addFontRequirement(requirements, themeEmojiFontId, 400, "normal");
  }

  for (const root of [
    parseObject(payload.configJson),
    parseObject(payload.componentBaseConfigsJson),
  ]) {
    collectTypographyFontRequirements(
      root,
      requirements,
      themeFontId,
      themeSystemFontId,
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
  themeSystemFontId: string,
  themeWeight: number,
  themeStyle: string,
) {
  if (Array.isArray(value)) {
    for (const entry of value) {
      collectTypographyFontRequirements(entry, requirements, themeFontId, themeSystemFontId, themeWeight, themeStyle);
    }
    return;
  }

  if (typeof value !== "object" || value === null) return;
  const record = value as Record<string, unknown>;
  if ("fontFamilyId" in record) {
    const fontId = stringValue(record.fontFamilyId).trim();
    const resolvedFontId = fontId === "theme"
      ? themeFontId
      : fontId === "theme.system"
        ? themeSystemFontId
        : fontId;
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
    collectTypographyFontRequirements(child, requirements, themeFontId, themeSystemFontId, themeWeight, themeStyle);
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

function requiredThemeFontStyle(value: unknown, path: string) {
  if (value === "normal" || value === "italic") return value;
  throw new Error(`Unsupported theme font style ${path}`);
}

function fontFileUri(fullPath: string) {
  return pathToFileURL(fullPath).href;
}
