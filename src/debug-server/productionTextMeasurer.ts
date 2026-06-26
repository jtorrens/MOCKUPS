import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import type { SQLiteDatabase } from "../persistence/sqlite/createDatabase.js";
import type { TextMeasurer, TextMeasureStyle } from "../visual/layout/types.js";

type Row = Record<string, unknown>;

interface FontFaceMetrics {
  family: string;
  style: string;
  measure(text: string, fontSize: number): number;
}

export interface ProductionFontFaceSource {
  family: string;
  filePath: string;
  relativeFilePath: string;
  style: string;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === "object" && !Array.isArray(value));
}

function parseJsonObject(value: unknown): Record<string, unknown> {
  if (typeof value !== "string" || !value.trim()) return {};
  try {
    const parsed = JSON.parse(value) as unknown;
    return isRecord(parsed) ? parsed : {};
  } catch {
    return {};
  }
}

function normalize(value: string) {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, "");
}

function productionRoot(database: SQLiteDatabase, productionId: string) {
  const row = database
    .prepare("SELECT settings_json FROM productions WHERE id = ?")
    .get(productionId) as Row | undefined;
  const settings = parseJsonObject(row?.settings_json);
  return typeof settings.mediaRoot === "string" ? settings.mediaRoot : "";
}

function u16(buffer: Buffer, offset: number) {
  return buffer.readUInt16BE(offset);
}

function i16(buffer: Buffer, offset: number) {
  return buffer.readInt16BE(offset);
}

function u32(buffer: Buffer, offset: number) {
  return buffer.readUInt32BE(offset);
}

function tableMap(buffer: Buffer) {
  const tables = new Map<string, { offset: number; length: number }>();
  const sfntVersion = u32(buffer, 0);
  if (
    sfntVersion !== 0x00010000 &&
    sfntVersion !== 0x4f54544f &&
    sfntVersion !== 0x74727565
  ) {
    return tables;
  }
  const tableCount = u16(buffer, 4);
  for (let index = 0; index < tableCount; index += 1) {
    const offset = 12 + index * 16;
    const tag = buffer.toString("ascii", offset, offset + 4);
    tables.set(tag, {
      offset: u32(buffer, offset + 8),
      length: u32(buffer, offset + 12),
    });
  }
  return tables;
}

function format4GlyphId(buffer: Buffer, offset: number, codePoint: number) {
  const segCount = u16(buffer, offset + 6) / 2;
  const endCodeOffset = offset + 14;
  const startCodeOffset = endCodeOffset + segCount * 2 + 2;
  const idDeltaOffset = startCodeOffset + segCount * 2;
  const idRangeOffsetOffset = idDeltaOffset + segCount * 2;
  for (let index = 0; index < segCount; index += 1) {
    const endCode = u16(buffer, endCodeOffset + index * 2);
    const startCode = u16(buffer, startCodeOffset + index * 2);
    if (codePoint < startCode || codePoint > endCode) continue;
    const delta = i16(buffer, idDeltaOffset + index * 2);
    const rangeOffsetAddress = idRangeOffsetOffset + index * 2;
    const rangeOffset = u16(buffer, rangeOffsetAddress);
    if (rangeOffset === 0) return (codePoint + delta) & 0xffff;
    const glyphOffset =
      rangeOffsetAddress + rangeOffset + (codePoint - startCode) * 2;
    if (glyphOffset < 0 || glyphOffset + 2 > buffer.length) return 0;
    const glyph = u16(buffer, glyphOffset);
    return glyph === 0 ? 0 : (glyph + delta) & 0xffff;
  }
  return 0;
}

function format12GlyphId(buffer: Buffer, offset: number, codePoint: number) {
  const groupCount = u32(buffer, offset + 12);
  for (let index = 0; index < groupCount; index += 1) {
    const groupOffset = offset + 16 + index * 12;
    const startCharCode = u32(buffer, groupOffset);
    const endCharCode = u32(buffer, groupOffset + 4);
    if (codePoint < startCharCode || codePoint > endCharCode) continue;
    return u32(buffer, groupOffset + 8) + codePoint - startCharCode;
  }
  return 0;
}

function createGlyphMapper(buffer: Buffer, cmapOffset: number) {
  const subtableCount = u16(buffer, cmapOffset + 2);
  let selectedOffset = 0;
  let selectedScore = -1;
  for (let index = 0; index < subtableCount; index += 1) {
    const recordOffset = cmapOffset + 4 + index * 8;
    const platform = u16(buffer, recordOffset);
    const encoding = u16(buffer, recordOffset + 2);
    const subtableOffset = cmapOffset + u32(buffer, recordOffset + 4);
    const format = u16(buffer, subtableOffset);
    const score =
      format === 12 && platform === 3 && encoding === 10
        ? 5
        : format === 4 && platform === 3 && encoding === 1
          ? 4
          : format === 12 && platform === 0
            ? 3
            : format === 4 && platform === 0
              ? 2
              : -1;
    if (score > selectedScore) {
      selectedScore = score;
      selectedOffset = subtableOffset;
    }
  }
  if (!selectedOffset) return undefined;
  const format = u16(buffer, selectedOffset);
  if (format === 12) {
    return (codePoint: number) => format12GlyphId(buffer, selectedOffset, codePoint);
  }
  if (format === 4) {
    return (codePoint: number) =>
      codePoint <= 0xffff ? format4GlyphId(buffer, selectedOffset, codePoint) : 0;
  }
  return undefined;
}

function parseFontFaceMetrics(
  filePath: string,
  family: string,
  style: string,
): FontFaceMetrics | undefined {
  if (!/\.(otf|ttf)$/i.test(filePath) || !existsSync(filePath)) return undefined;
  const buffer = readFileSync(filePath);
  if (buffer.length < 12) return undefined;
  const tables = tableMap(buffer);
  const head = tables.get("head");
  const hhea = tables.get("hhea");
  const hmtx = tables.get("hmtx");
  const cmap = tables.get("cmap");
  if (!head || !hhea || !hmtx || !cmap) return undefined;
  const unitsPerEm = u16(buffer, head.offset + 18) || 1000;
  const numberOfHMetrics = u16(buffer, hhea.offset + 34);
  const glyphIdForCodePoint = createGlyphMapper(buffer, cmap.offset);
  if (!glyphIdForCodePoint || numberOfHMetrics <= 0) return undefined;
  const lastAdvance = u16(buffer, hmtx.offset + (numberOfHMetrics - 1) * 4);
  const advanceForGlyph = (glyphId: number) =>
    glyphId < numberOfHMetrics
      ? u16(buffer, hmtx.offset + glyphId * 4)
      : lastAdvance;

  return {
    family,
    style,
    measure(text: string, fontSize: number) {
      const units = Array.from(text || " ").reduce((total, character) => {
        const glyphId = glyphIdForCodePoint(character.codePointAt(0) ?? 32);
        return total + advanceForGlyph(glyphId);
      }, 0);
      return (units / unitsPerEm) * fontSize;
    },
  };
}

function styleCandidates(style: TextMeasureStyle) {
  const requested = String(style.fontWeight ?? "Regular");
  return [
    requested,
    requested.replace(/\s+/g, ""),
    "Regular",
    "Roman",
    "Book",
    "Variable",
  ].map(normalize);
}

export class ProductionTextMeasurer implements TextMeasurer {
  private readonly facesByFamily = new Map<string, FontFaceMetrics[]>();
  private readonly widthCache = new Map<string, number>();

  addFace(face: FontFaceMetrics) {
    const key = normalize(face.family);
    this.facesByFamily.set(key, [...(this.facesByFamily.get(key) ?? []), face]);
  }

  measureLineWidth(text: string, style: TextMeasureStyle): number | undefined {
    const familyKey = normalize(style.fontFamily);
    const faces = this.facesByFamily.get(familyKey);
    if (!faces?.length) return undefined;
    const candidates = styleCandidates(style);
    const face =
      candidates
        .map((candidate) =>
          faces.find((entry) => normalize(entry.style) === candidate),
        )
        .find(Boolean) ?? faces[0];
    if (!face) return undefined;
    const cacheKey = [
      familyKey,
      normalize(face.style),
      style.fontSize,
      text,
    ].join("\u0000");
    const cached = this.widthCache.get(cacheKey);
    if (cached !== undefined) return cached;
    const width = face.measure(text, style.fontSize);
    this.widthCache.set(cacheKey, width);
    return width;
  }
}

export function createProductionTextMeasurer(
  database: SQLiteDatabase,
  productionId: string,
): ProductionTextMeasurer | undefined {
  const faces = productionFontFaceSources(database, productionId);
  const measurer = new ProductionTextMeasurer();
  let count = 0;
  for (const source of faces) {
    const face = parseFontFaceMetrics(source.filePath, source.family, source.style);
    if (!face) continue;
    measurer.addFace(face);
    count += 1;
  }
  return count > 0 ? measurer : undefined;
}

export function productionFontFaceSources(
  database: SQLiteDatabase,
  productionId: string,
): ProductionFontFaceSource[] {
  const root = productionRoot(database, productionId);
  if (!root) return [];
  const rows = database
    .prepare(
      "SELECT family, files_json FROM production_fonts WHERE production_id = ?",
    )
    .all(productionId) as Row[];
  const sources: ProductionFontFaceSource[] = [];
  for (const row of rows) {
    const family = typeof row.family === "string" ? row.family : "";
    const filesJson = parseJsonObject(row.files_json);
    const files = Array.isArray(filesJson.files) ? filesJson.files : [];
    for (const file of files) {
      if (!isRecord(file) || typeof file.filePath !== "string") continue;
      const style = typeof file.style === "string" ? file.style : "Regular";
      const resolvedPath = path.resolve(root, file.filePath);
      const relative = path.relative(root, resolvedPath);
      if (relative.startsWith("..") || path.isAbsolute(relative)) continue;
      sources.push({ family, filePath: resolvedPath, relativeFilePath: file.filePath, style });
    }
  }
  return sources;
}
