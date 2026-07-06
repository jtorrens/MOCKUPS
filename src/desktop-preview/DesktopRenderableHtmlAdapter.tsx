import React, { type CSSProperties, type ReactNode } from "react";
import type {
  RenderableBox,
  RenderableNode,
} from "../visual/renderable/types.js";
import {
  numberValue as commonNumberValue,
  stringValue as commonStringValue,
} from "./previewColorHelpers.js";
import { asRecord } from "./previewJsonHelpers.js";

export interface DesktopRenderableHtmlAdapterProps {
  tree: RenderableNode;
  showBounds?: boolean;
}

const supportedNodeTypes = new Set([
  "component_preview_unsupported",
  "design_preview_surface",
  "group",
  "icon_token",
  "image",
  "path",
  "surface",
  "text",
]);

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function optionalStringValue(value: unknown) {
  const text = commonStringValue(value, "");
  return typeof value === "string" ? text : undefined;
}

function optionalNumberValue(value: unknown) {
  const number = commonNumberValue(value, NaN);
  return Number.isFinite(number) ? number : undefined;
}

function cssString(value: unknown): string {
  return String(value ?? "").replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function cssFontWeight(value: unknown): CSSProperties["fontWeight"] | undefined {
  if (typeof value === "number") return value;
  if (typeof value !== "string") return undefined;
  const normalized = value.toLowerCase().replace(/[\s_-]+/g, "");
  if (normalized.includes("thin")) return 100;
  if (normalized.includes("extralight") || normalized.includes("ultralight")) return 200;
  if (normalized.includes("light")) return 300;
  if (normalized.includes("regular") || normalized.includes("normal") || normalized.includes("book")) return 400;
  if (normalized.includes("medium")) return 500;
  if (normalized.includes("semibold") || normalized.includes("demibold")) return 600;
  if (normalized.includes("bold")) return 700;
  if (normalized.includes("extrabold") || normalized.includes("ultrabold")) return 800;
  if (normalized.includes("black") || normalized.includes("heavy")) return 900;
  return value as CSSProperties["fontWeight"];
}

function boxStyle(
  box: RenderableBox | undefined,
  parentOrigin: { x: number; y: number },
): CSSProperties {
  if (!box) return { position: "relative" };
  return {
    position: "absolute",
    left: box.x - parentOrigin.x,
    top: box.y - parentOrigin.y,
    width: box.width,
    height: box.height,
  };
}

function shadowValue(value: unknown): string | undefined {
  const shadow = asRecord(value);
  const color = optionalStringValue(shadow.color);
  if (!color) return undefined;
  return `${optionalNumberValue(shadow.offsetX) ?? 0}px ${optionalNumberValue(shadow.offsetY) ?? 0}px ${optionalNumberValue(shadow.blur) ?? 0}px ${color}`;
}

function intensityColor(value: unknown): string | undefined {
  const intensity = optionalNumberValue(value);
  if (intensity === undefined || intensity === 0) return undefined;
  const alpha = Math.min(1, Math.abs(intensity));
  return intensity > 0
    ? `rgba(255, 255, 255, ${alpha})`
    : `rgba(0, 0, 0, ${alpha})`;
}

function surfaceReliefValue(value: unknown): string | undefined {
  const relief = asRecord(value);
  if (!Object.keys(relief).length) return undefined;
  const angleDeg = optionalNumberValue(relief.angleDeg) ?? -45;
  const extension = optionalNumberValue(relief.extension) ?? 1;
  const spread = optionalNumberValue(relief.spread) ?? 0;
  const angleRad = (angleDeg * Math.PI) / 180;
  const x = Math.cos(angleRad) * extension;
  const y = Math.sin(angleRad) * extension;
  const upperColor = intensityColor(relief.upperIntensity);
  const lowerColor = intensityColor(relief.lowerIntensity);
  return [
    upperColor ? `inset ${x}px ${y}px ${spread}px ${upperColor}` : undefined,
    lowerColor ? `inset ${-x}px ${-y}px ${spread}px ${lowerColor}` : undefined,
  ]
    .filter(Boolean)
    .join(", ") || undefined;
}

function joinedBoxShadow(...values: Array<string | undefined>) {
  return values.filter(Boolean).join(", ") || undefined;
}

function fontFaceCss(tree: RenderableNode) {
  const fontFaces = Array.isArray(tree.metadata?.fontFaces)
    ? tree.metadata.fontFaces
    : [];
  return fontFaces
    .filter(isRecord)
    .map((fontFace) => {
      const family = optionalStringValue(fontFace.family);
      const uri = optionalStringValue(fontFace.uri);
      if (!family || !uri) return "";
      const weight =
        typeof fontFace.weight === "number" || typeof fontFace.weight === "string"
          ? fontFace.weight
          : 400;
      const style = optionalStringValue(fontFace.style) ?? "normal";
      return `@font-face{font-family:"${cssString(family)}";src:url("${cssString(uri)}");font-weight:${weight};font-style:${cssString(style)};font-display:block;}`;
    })
    .filter(Boolean)
    .join("\n");
}

function svgStrokeLinecap(value: unknown): "butt" | "round" | "square" | "inherit" | undefined {
  return value === "butt" || value === "round" || value === "square" || value === "inherit"
    ? value
    : undefined;
}

function svgStrokeLinejoin(value: unknown): "miter" | "round" | "bevel" | "inherit" | undefined {
  return value === "miter" || value === "round" || value === "bevel" || value === "inherit"
    ? value
    : undefined;
}

function iconTokenLabel(token: string) {
  const parts = token.split("_").filter(Boolean);
  return parts.at(-1)?.slice(0, 2).toUpperCase() ?? "IC";
}

function nodeTransform(node: RenderableNode): string | undefined {
  const transform = node.transform;
  if (!transform) return undefined;
  const parts = [
    optionalNumberValue(transform.x) !== undefined || optionalNumberValue(transform.y) !== undefined
      ? `translate(${optionalNumberValue(transform.x) ?? 0}px, ${optionalNumberValue(transform.y) ?? 0}px)`
      : undefined,
    optionalNumberValue(transform.rotation) !== undefined
      ? `rotate(${optionalNumberValue(transform.rotation)}deg)`
      : undefined,
    optionalNumberValue(transform.scale) !== undefined
      ? `scale(${optionalNumberValue(transform.scale)})`
      : undefined,
  ].filter(Boolean);
  return parts.join(" ") || undefined;
}

function nodeStyle(
  node: RenderableNode,
  parentOrigin: { x: number; y: number },
): CSSProperties {
  const style = node.style ?? {};
  const shadow = shadowValue(style.shadow);
  const relief = surfaceReliefValue(style.surfaceRelief);
  const background = optionalStringValue(style.background ?? style.backgroundColor);
  const color = optionalStringValue(style.textColor ?? style.color ?? style.foreground);
  const borderColor = optionalStringValue(style.borderColor);
  const borderWidth = optionalNumberValue(style.borderWidth);
  const maskImage = optionalStringValue(style.maskImage);
  const webkitMaskImage = optionalStringValue(style.WebkitMaskImage);
  const paddingX = optionalNumberValue(style.paddingX);
  const paddingY = optionalNumberValue(style.paddingY);
  const display = optionalStringValue(style.display);
  const alignItems = optionalStringValue(style.alignItems);
  const justifyContent = optionalStringValue(style.justifyContent);
  const flexDirection = optionalStringValue(style.flexDirection);
  const width = optionalStringValue(style.width) ?? optionalNumberValue(style.width) ?? node.box?.width;
  const height = optionalStringValue(style.height) ?? optionalNumberValue(style.height) ?? node.box?.height;

  return {
    ...boxStyle(node.box, parentOrigin),
    alignItems: alignItems as CSSProperties["alignItems"],
    background,
    backgroundColor: maskImage || webkitMaskImage
      ? background ?? "currentColor"
      : undefined,
    border: borderColor && borderWidth && borderWidth > 0
      ? `${borderWidth}px solid ${borderColor}`
      : undefined,
    borderRadius: optionalNumberValue(style.borderRadius ?? style.cornerRadius),
    boxShadow: joinedBoxShadow(shadow, relief),
    boxSizing: "border-box",
    color,
    display: display as CSSProperties["display"],
    flexDirection: flexDirection as CSSProperties["flexDirection"],
    fontFamily: optionalStringValue(style.fontFamily),
    fontSize: optionalNumberValue(style.fontSize),
    fontStyle: optionalStringValue(style.fontStyle) as CSSProperties["fontStyle"],
    fontWeight: cssFontWeight(style.fontWeight),
    height,
    justifyContent: justifyContent as CSSProperties["justifyContent"],
    lineHeight: optionalNumberValue(style.lineHeight)
      ? `${optionalNumberValue(style.lineHeight)}px`
      : undefined,
    marginTop: optionalNumberValue(style.marginTop),
    maskImage,
    maskPosition: maskImage ? "center" : undefined,
    maskRepeat: maskImage ? "no-repeat" : undefined,
    maskSize: maskImage ? "contain" : undefined,
    opacity: optionalNumberValue(node.transform?.opacity) ?? optionalNumberValue(style.opacity),
    overflow: optionalStringValue(style.overflow) as CSSProperties["overflow"],
    paddingBottom: paddingY,
    paddingLeft: paddingX ?? optionalNumberValue(style.paddingLeft),
    paddingRight: paddingX,
    paddingTop: paddingY,
    textAlign: optionalStringValue(style.textAlign) as CSSProperties["textAlign"],
    transform: nodeTransform(node),
    transformOrigin: nodeTransform(node) ? "center center" : undefined,
    WebkitMaskImage: webkitMaskImage,
    WebkitMaskPosition: webkitMaskImage ? "center" : undefined,
    WebkitMaskRepeat: webkitMaskImage ? "no-repeat" : undefined,
    WebkitMaskSize: webkitMaskImage ? "contain" : undefined,
    whiteSpace: optionalStringValue(style.whiteSpace) as CSSProperties["whiteSpace"],
    width,
    zIndex: optionalNumberValue(style.zIndex),
  };
}

function pathContent(node: RenderableNode): ReactNode {
  const pathData = optionalStringValue(node.style?.pathData);
  if (!pathData) return null;
  return (
    <svg
      aria-hidden="true"
      preserveAspectRatio={optionalStringValue(node.style?.preserveAspectRatio) ?? "none"}
      style={{
        display: "block",
        height: "100%",
        overflow: "visible",
        width: "100%",
      }}
      viewBox={optionalStringValue(node.style?.viewBox) ?? "0 0 100 100"}
    >
      <path
        d={pathData}
        fill={optionalStringValue(node.style?.fill) ?? "currentColor"}
        stroke={optionalStringValue(node.style?.stroke)}
        strokeLinecap={svgStrokeLinecap(node.style?.strokeLinecap)}
        strokeLinejoin={svgStrokeLinejoin(node.style?.strokeLinejoin)}
        strokeWidth={optionalNumberValue(node.style?.strokeWidth)}
        vectorEffect={optionalStringValue(node.style?.vectorEffect)}
      />
    </svg>
  );
}

function imageContent(node: RenderableNode): ReactNode {
  const uri = optionalStringValue(node.asset?.uri);
  const fallbackText = optionalStringValue(node.metadata?.fallbackText) ?? node.text ?? "";
  if (!uri) return fallbackText;
  const imageScale = Math.max(0.01, optionalNumberValue(node.metadata?.imageScale) ?? 1);
  const imageBaseSize = Math.max(
    1,
    optionalNumberValue(node.metadata?.imageBaseSize) ??
      optionalNumberValue(node.box?.width) ??
      1,
  );
  const boxWidth = Math.max(1, optionalNumberValue(node.box?.width) ?? 1);
  const imageOffsetX =
    ((optionalNumberValue(node.metadata?.imageOffsetX) ?? 0) / imageBaseSize) *
    boxWidth;
  const imageOffsetY =
    ((optionalNumberValue(node.metadata?.imageOffsetY) ?? 0) / imageBaseSize) *
    boxWidth;
  const hasCustomPlacement =
    node.metadata?.imageScale !== undefined ||
    node.metadata?.imageOffsetX !== undefined ||
    node.metadata?.imageOffsetY !== undefined;
  return (
    <img
      alt={fallbackText}
      draggable={false}
      src={uri}
      style={hasCustomPlacement
        ? {
            display: "block",
            height: `${imageScale * 100}%`,
            left: "50%",
            maxHeight: "none",
            maxWidth: "none",
            objectFit: "cover",
            position: "absolute",
            top: "50%",
            transform: `translate(calc(-50% + ${imageOffsetX}px), calc(-50% + ${imageOffsetY}px))`,
            width: `${imageScale * 100}%`,
          }
        : {
            display: "block",
            height: "100%",
            objectFit: (optionalStringValue(node.style?.objectFit) ?? "cover") as CSSProperties["objectFit"],
            width: "100%",
          }}
    />
  );
}

function iconTokenContent(node: RenderableNode): ReactNode {
  const token = optionalStringValue(node.metadata?.token) ?? node.text ?? "";
  if (optionalStringValue(node.style?.maskImage) || optionalStringValue(node.style?.WebkitMaskImage)) {
    return <span title={token} />;
  }
  return <span title={token}>{iconTokenLabel(token)}</span>;
}

function nodeContent(node: RenderableNode): ReactNode {
  if (!supportedNodeTypes.has(node.type)) {
    return `Unsupported desktop primitive: ${node.type}`;
  }
  if (node.type === "image") return imageContent(node);
  if (node.type === "path") return pathContent(node);
  if (node.type === "icon_token") return iconTokenContent(node);
  if (node.children?.some((child) => child.type === "text")) return null;
  return node.text;
}

function semanticStyle(node: RenderableNode): CSSProperties {
  if (!supportedNodeTypes.has(node.type)) {
    return {
      alignItems: "center",
      background: "#ff00ff",
      color: "#ffffff",
      display: "flex",
      fontFamily: "system-ui, sans-serif",
      fontSize: 14,
      fontWeight: 700,
      justifyContent: "center",
      overflow: "hidden",
      padding: 8,
      textAlign: "center",
    };
  }
  if (node.type === "component_preview_unsupported") {
    return {
      alignItems: "center",
      display: "flex",
      justifyContent: "center",
      overflow: "hidden",
      paddingLeft: 12,
      paddingRight: 12,
      whiteSpace: "nowrap",
    };
  }
  if (node.type === "icon_token") {
    return {
      alignItems: "center",
      backgroundColor: optionalStringValue(node.style?.maskImage) ? "currentColor" : undefined,
      display: "inline-flex",
      justifyContent: "center",
      overflow: "visible",
    };
  }
  if (node.type === "text") {
    return {
      display: (optionalStringValue(node.style?.display) as CSSProperties["display"]) ?? "inline",
      overflow: (optionalStringValue(node.style?.overflow) as CSSProperties["overflow"]) ?? "hidden",
      whiteSpace: (optionalStringValue(node.style?.whiteSpace) as CSSProperties["whiteSpace"]) ?? "pre-wrap",
    };
  }
  return {};
}

function RenderNode({
  node,
  parentOrigin,
  showBounds = false,
}: {
  node: RenderableNode;
  parentOrigin: { x: number; y: number };
  showBounds?: boolean;
}) {
  const currentOrigin = node.box
    ? { x: node.box.x, y: node.box.y }
    : parentOrigin;
  return (
    <div
      data-renderable-role={node.role}
      data-renderable-type={node.type}
      style={{
        ...nodeStyle(node, parentOrigin),
        ...semanticStyle(node),
        ...(showBounds
          ? {
              outline: "1px solid rgba(255, 0, 255, 0.72)",
              outlineOffset: "-1px",
            }
          : {}),
      }}
    >
      {nodeContent(node)}
      {node.children?.map((child) => (
        <RenderNode
          key={child.id}
          node={child}
          parentOrigin={currentOrigin}
          showBounds={showBounds}
        />
      ))}
    </div>
  );
}

export function DesktopRenderableHtmlAdapter({
  tree,
  showBounds = false,
}: DesktopRenderableHtmlAdapterProps) {
  const css = fontFaceCss(tree);
  return (
    <>
      {css ? <style>{css}</style> : null}
      <RenderNode
        node={tree}
        parentOrigin={{ x: 0, y: 0 }}
        showBounds={showBounds}
      />
    </>
  );
}
