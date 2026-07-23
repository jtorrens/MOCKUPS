import { RenderableNodeSchema } from "../visual/renderable/schema.js";
import { fileURLToPath } from "node:url";
import * as fontkit from "fontkit";
import type { RenderableBox, RenderableFontFace, RenderableNode } from "../visual/renderable/types.js";
import { numberValue, stringValue } from "./previewValueHelpers.js";
import { textGraphemes } from "./previewTextRevealHelpers.js";
import { emojiRasterDataUri } from "./previewAssetResolver.js";

/**
 * Serializes an already-resolved paint tree to SVG.
 *
 * This adapter intentionally knows only the shared RenderableNode primitives.
 * Component identity and embedding remain represented by the existing node
 * hierarchy and stable node ids; no component contract enters this layer.
 */
export interface SvgExportOptions {
  target?: "web" | "affinity";
}

interface SvgRenderContext {
  target: "web" | "affinity";
  fontFaces: RenderableFontFace[];
  ids: Map<string, number>;
}

type OutlineFont = {
  unitsPerEm: number;
  layout: (text: string) => {
    advanceWidth: number;
    glyphs: Array<{ path: { toSVG: () => string } }>;
    positions: Array<{ xAdvance: number }>;
  };
};
const outlinedFontCache = new Map<string, OutlineFont>();

export function renderableToSvg(tree: RenderableNode, options: SvgExportOptions = {}): string {
  const root = RenderableNodeSchema.parse(tree);
  const canvas = requiredBox(root);
  const context: SvgRenderContext = { target: options.target ?? "web", fontFaces: root.metadata?.fontFaces ?? [], ids: new Map() };
  return [
    '<?xml version="1.0" encoding="UTF-8"?>',
    `<svg xmlns="http://www.w3.org/2000/svg" width="${number(canvas.width)}" height="${number(canvas.height)}" viewBox="${number(canvas.x)} ${number(canvas.y)} ${number(canvas.width)} ${number(canvas.height)}">`,
    context.target === "web" ? fontFaceDefinitions(root) : "",
    renderNode(root, context),
    "</svg>",
  ].filter(Boolean).join("\n");
}

function renderNode(node: RenderableNode, context: SvgRenderContext): string {
  const svgId = nextSvgId(node.id, context);
  const attributes = groupAttributes(node, svgId);
  const content = primitiveMarkup(node, context, svgId);
  const children = (node.children ?? []).map((child) => renderNode(child, context)).join("\n");
  const clippedChildren = clipChildrenMarkup(node, svgId, children);
  return `<g ${attributes}>${content}${clippedChildren ? `\n${clippedChildren}` : ""}</g>`;
}

function clipChildrenMarkup(node: RenderableNode, svgId: string, children: string) {
  if (!children) return "";
  const box = node.box;
  if (!box || stringValue(node.style?.overflow, "") !== "hidden") return children;
  const radius = Math.max(0, finite(node.style?.borderRadius ?? node.style?.cornerRadius) ?? 0);
  const clipId = `clip-${safeId(svgId)}`;
  return `<defs><clipPath id="${clipId}"><rect ${attributes({ x: box.x, y: box.y, width: box.width, height: box.height, rx: radius, ry: radius })}/></clipPath></defs><g clip-path="url(#${clipId})">${children}</g>`;
}

function groupAttributes(node: RenderableNode, svgId: string): string {
  const opacity = finite(node.transform?.opacity) ?? finite(node.style?.opacity);
  return attributes({
    id: svgId,
    "data-renderable-id": node.id,
    "data-renderable-type": node.type,
    opacity,
    transform: transformMarkup(node),
  });
}

function primitiveMarkup(node: RenderableNode, context: SvgRenderContext, svgId: string): string {
  if (node.type === "group") return "";
  if (node.type === "surface") return surfaceMarkup(node);
  if (node.type === "text") return context.target === "affinity" ? outlinedTextMarkup(node, context.fontFaces) : textMarkup(node);
  if (node.type === "image") return imageMarkup(node, context.target);
  if (node.type === "path") return pathMarkup(node);
  return iconMarkup(node, context.target, svgId);
}

function surfaceMarkup(node: RenderableNode): string {
  const box = node.box;
  if (!box) return "";
  const style = node.style ?? {};
  return `<rect ${attributes({
    x: box.x,
    y: box.y,
    width: box.width,
    height: box.height,
    rx: finite(style.borderRadius ?? style.cornerRadius),
    ry: finite(style.borderRadius ?? style.cornerRadius),
    fill: color(style.background ?? style.backgroundColor) ?? "none",
    stroke: color(style.borderColor),
    "stroke-width": finite(style.borderWidth),
  })}/>`;
}

function textMarkup(node: RenderableNode): string {
  const box = node.box;
  if (!box || node.text === undefined) return "";
  const style = node.style ?? {};
  const fontSize = finite(style.fontSize) ?? 16;
  const lineHeight = finite(style.lineHeight) ?? fontSize * 1.2;
  const textAlign = stringValue(style.textAlign, "left");
  const x = textAlign === "center" ? box.x + box.width / 2 : textAlign === "right" ? box.x + box.width : box.x;
  const anchor = textAlign === "center" ? "middle" : textAlign === "right" ? "end" : "start";
  const lines = node.text.split(/\r?\n/);
  const tspans = lines.map((line, index) =>
    `<tspan x="${number(x)}" dy="${index === 0 ? 0 : number(lineHeight)}">${escapeText(line)}</tspan>`,
  ).join("");
  const cursor = inlineCursorMarkup(node, x, box.y + fontSize, fontSize, textAlign);
  return `<text ${attributes({
    x,
    y: box.y + fontSize,
    fill: color(style.textColor ?? style.color ?? style.foreground) ?? "currentColor",
    "font-family": stringValue(style.fontFamily, "sans-serif"),
    "font-size": fontSize,
    "font-style": stringValue(style.fontStyle, ""),
    "font-weight": style.fontWeight === undefined ? undefined : String(style.fontWeight),
    "text-anchor": anchor,
    "xml:space": "preserve",
  })}>${tspans}</text>${cursor}`;
}

function outlinedTextMarkup(node: RenderableNode, fontFaces: RenderableFontFace[]): string {
  const box = node.box;
  if (!box || node.text === undefined) return "";
  const style = node.style ?? {};
  const fontSize = finite(style.fontSize) ?? 16;
  const font = outlineFont(style.fontFamily, fontFaces);
  if (!font) return textMarkup(node);
  const scale = fontSize / Math.max(1, font.unitsPerEm);
  const runs = textGraphemes(node.text).map((grapheme) => ({ grapheme, run: font.layout(grapheme) }));
  const advance = runs.reduce((total, item) => total + item.run.advanceWidth * scale, 0);
  const textAlign = stringValue(style.textAlign, "left");
  const left = textAlign === "center" ? box.x + (box.width - advance) / 2 : textAlign === "right" ? box.x + box.width - advance : box.x;
  let cursor = 0;
  const paths = runs.map(({ grapheme, run }) => {
    const x = left + cursor * scale;
    const isEmoji = /\p{Extended_Pictographic}/u.test(grapheme);
    const outlined = !isEmoji && run.glyphs.every((glyph) => glyph.path.toSVG().length > 0);
    const markup = outlined
      ? run.glyphs.map((glyph, index) => {
        const position = run.positions[index];
        const d = glyph.path.toSVG();
        const transform = `translate(${number(left + cursor * scale)} ${number(box.y + fontSize)}) scale(${number(scale)} ${number(-scale)})`;
        cursor += position?.xAdvance ?? 0;
        return `<g transform="${transform}"><path d="${escapeAttribute(d)}"/></g>`;
      }).join("")
      : isEmoji ? emojiMarkup(grapheme, x, box.y, fontSize) : "";
    if (!outlined) cursor += isEmoji
      ? emojiRasterDataUri(grapheme, fontSize).width / scale
      : run.advanceWidth;
    return markup;
  }).join("");
  return `<g ${attributes({ fill: color(style.textColor ?? style.color ?? style.foreground) ?? "currentColor" })}>${paths}</g>${inlineCursorMarkup(node, left, box.y + fontSize, fontSize, "left")}`;
}

function emojiMarkup(grapheme: string, x: number, y: number, fontSize: number) {
  const emoji = emojiRasterDataUri(grapheme, fontSize);
  return `<image ${attributes({ x, y, width: emoji.width, height: emoji.height, href: emoji.uri, preserveAspectRatio: "xMinYMin meet" })}/>`;
}

function outlineFont(fontFamilyValue: unknown, fontFaces: RenderableFontFace[]) {
  const family = stringValue(fontFamilyValue, "").match(/MockupsFont_[A-Za-z0-9_]+/)?.[0] ?? "";
  const face = fontFaces.find((candidate) => candidate.family === family && candidate.style !== "italic")
    ?? fontFaces.find((candidate) => candidate.family === family);
  if (!face?.uri.startsWith("file:")) return undefined;
  const filePath = fileURLToPath(face.uri);
  const cached = outlinedFontCache.get(filePath);
  if (cached) return cached;
  const opened = fontkit.openSync(filePath) as unknown as OutlineFont;
  outlinedFontCache.set(filePath, opened);
  return opened;
}

function inlineCursorMarkup(node: RenderableNode, x: number, baseline: number, fontSize: number, textAlign: string) {
  const cursor = node.metadata?.inlineCursor;
  if (!cursor) return "";
  const cursorColor = color(cursor.color);
  if (!cursorColor) return "";
  const estimate = (node.text ?? "").length * fontSize * 0.52;
  const cursorX = textAlign === "right" ? x - estimate : textAlign === "center" ? x + estimate / 2 : x + estimate;
  return `<rect ${attributes({
    x: cursorX,
    y: baseline - fontSize,
    width: cursor.width,
    height: fontSize * 1.05,
    rx: Math.min(cursor.width / 2, 2),
    fill: cursorColor,
    opacity: cursor.opacity ?? 1,
  })}/>`;
}

function imageMarkup(node: RenderableNode, target: "web" | "affinity"): string {
  const box = node.box;
  const uri = node.asset?.uri;
  if (!box || !uri) return "";
  const scale = Math.max(0.01, finite(node.metadata?.imageScale) ?? 1);
  const baseSize = Math.max(1, finite(node.metadata?.imageBaseSize) ?? box.width);
  const offsetX = ((finite(node.metadata?.imageOffsetX) ?? 0) / baseSize) * box.width;
  const offsetY = ((finite(node.metadata?.imageOffsetY) ?? 0) / baseSize) * box.width;
  const customPlacement = node.metadata?.imageScale !== undefined || node.metadata?.imageOffsetX !== undefined || node.metadata?.imageOffsetY !== undefined;
  const x = customPlacement ? box.x + (box.width - box.width * scale) / 2 + offsetX : box.x;
  const y = customPlacement ? box.y + (box.height - box.height * scale) / 2 + offsetY : box.y;
  if (target === "affinity" && /^data:image\/svg\+xml/i.test(uri)) {
    return inlineSvgImageMarkup({ x, y, width: box.width * scale, height: box.height * scale }, uri,
      stringValue(node.style?.objectFit, "cover") === "contain" ? "xMidYMid meet" : "xMidYMid slice");
  }
  return `<image ${attributes({
    x,
    y,
    width: box.width * scale,
    height: box.height * scale,
    href: uri,
    preserveAspectRatio: stringValue(node.style?.objectFit, "cover") === "contain" ? "xMidYMid meet" : "xMidYMid slice",
  })}/>`;
}

function inlineSvgImageMarkup(box: RenderableBox, uri: string, preserveAspectRatio: string) {
  const svg = dataSvgMarkup(uri);
  const viewBox = svg.match(/<svg\b[^>]*\bviewBox=["']([^"']+)["']/i)?.[1] ?? `0 0 ${box.width} ${box.height}`;
  const body = svg.match(/<svg\b[^>]*>([\s\S]*)<\/svg>/i)?.[1] ?? "";
  if (!body) return `<image ${attributes({ ...box, href: uri, preserveAspectRatio })}/>`;
  return `<svg ${attributes({ ...box, viewBox, preserveAspectRatio })}>${body}</svg>`;
}

function pathMarkup(node: RenderableNode): string {
  const box = node.box;
  const pathData = stringValue(node.style?.pathData, "");
  if (!box || !pathData) return "";
  return `<svg ${attributes({
    x: box.x,
    y: box.y,
    width: box.width,
    height: box.height,
    viewBox: stringValue(node.style?.viewBox, "0 0 100 100"),
    preserveAspectRatio: stringValue(node.style?.preserveAspectRatio, "none"),
  })}><path ${attributes({
    d: pathData,
    fill: color(node.style?.fill) ?? "currentColor",
    stroke: color(node.style?.stroke),
    "stroke-width": finite(node.style?.strokeWidth),
    "stroke-linecap": stringValue(node.style?.strokeLinecap, ""),
    "stroke-linejoin": stringValue(node.style?.strokeLinejoin, ""),
    "vector-effect": stringValue(node.style?.vectorEffect, ""),
  })}/></svg>`;
}

function iconMarkup(node: RenderableNode, target: "web" | "affinity", svgId: string): string {
  const box = node.box;
  const maskImage = stringValue(node.style?.maskImage ?? node.style?.WebkitMaskImage, "");
  if (!box || !maskImage) return "";
  const uri = maskImage.match(/^url\(["']?(.*?)["']?\)$/)?.[1] ?? "";
  if (!uri) return "";
  if (target === "affinity") return inlineSvgIconMarkup(box, uri, color(node.style?.color) ?? "currentColor");
  const maskId = `mask-${safeId(svgId)}`;
  return `<defs><mask id="${maskId}"><image ${attributes({ x: box.x, y: box.y, width: box.width, height: box.height, href: uri, preserveAspectRatio: "xMidYMid meet" })}/></mask></defs><rect ${attributes({ x: box.x, y: box.y, width: box.width, height: box.height, fill: color(node.style?.color) ?? "currentColor", mask: `url(#${maskId})` })}/>`;
}

function nextSvgId(renderableId: string, context: SvgRenderContext) {
  const count = (context.ids.get(renderableId) ?? 0) + 1;
  context.ids.set(renderableId, count);
  return count === 1 ? renderableId : `${renderableId}--${count}`;
}

function inlineSvgIconMarkup(box: RenderableBox, uri: string, fill: string) {
  const svg = dataSvgMarkup(uri);
  const viewBox = svg.match(/<svg\b[^>]*\bviewBox=["']([^"']+)["']/i)?.[1] ?? "0 0 24 24";
  const body = svg.match(/<svg\b[^>]*>([\s\S]*)<\/svg>/i)?.[1] ?? "";
  if (!body) return `<image ${attributes({ x: box.x, y: box.y, width: box.width, height: box.height, href: uri, preserveAspectRatio: "xMidYMid meet" })}/>`;
  return `<svg ${attributes({ x: box.x, y: box.y, width: box.width, height: box.height, viewBox, preserveAspectRatio: "xMidYMid meet", color: fill, fill })}>${body}</svg>`;
}

function dataSvgMarkup(uri: string) {
  const base64 = uri.match(/^data:image\/svg\+xml;base64,(.*)$/i)?.[1];
  if (base64) return Buffer.from(base64, "base64").toString("utf8");
  const encoded = uri.match(/^data:image\/svg\+xml(?:;charset=[^,]+)?,(.*)$/i)?.[1];
  return encoded ? decodeURIComponent(encoded) : "";
}

function fontFaceDefinitions(tree: RenderableNode): string {
  const fontFaces = tree.metadata?.fontFaces ?? [];
  if (!fontFaces.length) return "";
  const rules = fontFaces.map((font) => `@font-face{font-family:${cssString(font.family)};src:url(${cssString(font.uri)});font-weight:${font.weight ?? 400};font-style:${font.style ?? "normal"};}`).join("");
  return `<style>${rules}</style>`;
}

function transformMarkup(node: RenderableNode) {
  const transform = node.transform;
  if (!transform) return undefined;
  const parts: string[] = [];
  const x = finite(transform.x) ?? 0;
  const y = finite(transform.y) ?? 0;
  if (x || y) parts.push(`translate(${number(x)} ${number(y)})`);
  const centerX = (node.box?.x ?? 0) + (node.box?.width ?? 0) / 2;
  const centerY = (node.box?.y ?? 0) + (node.box?.height ?? 0) / 2;
  const rotation = finite(transform.rotation);
  if (rotation) parts.push(`rotate(${number(rotation)} ${number(centerX)} ${number(centerY)})`);
  const scale = finite(transform.scale);
  if (scale !== undefined && scale !== 1) parts.push(`translate(${number(centerX)} ${number(centerY)}) scale(${number(scale)}) translate(${number(-centerX)} ${number(-centerY)})`);
  return parts.join(" ") || undefined;
}

function requiredBox(node: RenderableNode): RenderableBox {
  if (!node.box) throw new Error("SVG export requires a boxed renderable root");
  return node.box;
}

function finite(value: unknown): number | undefined {
  const candidate = numberValue(value, Number.NaN);
  return Number.isFinite(candidate) ? candidate : undefined;
}

function color(value: unknown): string | undefined {
  const candidate = stringValue(value, "");
  return candidate || undefined;
}

function attributes(values: Record<string, string | number | undefined>) {
  return Object.entries(values)
    .filter((entry): entry is [string, string | number] => entry[1] !== undefined && entry[1] !== "")
    .map(([key, value]) => `${key}="${escapeAttribute(String(value))}"`)
    .join(" ");
}

function escapeAttribute(value: string) {
  return value.replace(/&/g, "&amp;").replace(/"/g, "&quot;").replace(/</g, "&lt;");
}

function escapeText(value: string) {
  return escapeAttribute(value).replace(/>/g, "&gt;");
}

function cssString(value: string) {
  return `"${value.replace(/\\/g, "\\\\").replace(/"/g, '\\"')}"`;
}

function safeId(value: string) {
  return value.replace(/[^A-Za-z0-9_.-]/g, "_");
}

function number(value: number) {
  return Number.isInteger(value) ? String(value) : String(Number(value.toFixed(4)));
}
