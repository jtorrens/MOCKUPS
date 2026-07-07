import type { RenderableBox } from "../visual/renderable/types.js";

export interface SurfaceTailShape {
  side: "left" | "right";
  style: "rounded_wedge" | "curved_hook" | "simple_triangle" | "cut_corner";
  vertical: "top" | "bottom";
  width: number;
  height: number;
  cornerRadius: number;
}

export interface SurfaceShapeSvgInput {
  body: RenderableBox;
  borderColor: string;
  borderWidth: number;
  color: string;
  cornerRadius: number;
  tail: SurfaceTailShape;
}

export function surfaceShapeDataUri({
  body,
  borderColor,
  borderWidth,
  color,
  cornerRadius,
  tail,
}: SurfaceShapeSvgInput) {
  const tailBox = surfaceTailBox(body, tail);
  const minX = Math.min(body.x, tailBox.x);
  const minY = Math.min(body.y, tailBox.y);
  const maxX = Math.max(body.x + body.width, tailBox.x + tailBox.width);
  const maxY = Math.max(body.y + body.height, tailBox.y + tailBox.height);
  const width = Math.max(1, maxX - minX);
  const height = Math.max(1, maxY - minY);
  const bodyX = body.x - minX;
  const bodyY = body.y - minY;
  const tailX = tailBox.x - minX;
  const tailY = tailBox.y - minY;
  const escapedColor = svgAttribute(color);
  const escapedBorderColor = svgAttribute(borderColor);
  const safeRadius = Math.max(0, Math.min(cornerRadius, body.width / 2, body.height / 2));
  const shapeMarkup = `
    <rect x="${bodyX}" y="${bodyY}" width="${body.width}" height="${body.height}" rx="${safeRadius}" ry="${safeRadius}"/>
    <path d="${surfaceTailPath({
      height: tailBox.height,
      side: tail.side,
      style: tail.style,
      vertical: tail.vertical,
      width: tailBox.width,
    })}" transform="translate(${tailX} ${tailY})"/>`;
  const borderMarkup =
    borderWidth > 0 && borderColor !== "transparent"
      ? `<defs>
          <filter id="surface-shape-border" x="-20%" y="-20%" width="140%" height="140%">
            <feMorphology in="SourceAlpha" operator="dilate" radius="${borderWidth}" result="expanded"/>
            <feFlood flood-color="${escapedBorderColor}" result="borderColor"/>
            <feComposite in="borderColor" in2="expanded" operator="in" result="borderShape"/>
            <feComposite in="borderShape" in2="SourceAlpha" operator="out"/>
          </filter>
        </defs>
        <g fill="${escapedColor}" filter="url(#surface-shape-border)">${shapeMarkup}</g>`
      : "";
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${width} ${height}" width="100%" height="100%" preserveAspectRatio="none" style="display:block;overflow:visible">
    ${borderMarkup}
    <g fill="${escapedColor}">${shapeMarkup}</g>
  </svg>`;

  return {
    box: { x: minX, y: minY, width, height },
    uri: `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`,
  };
}

export function surfaceTailBox(
  body: RenderableBox,
  tail: SurfaceTailShape,
): RenderableBox {
  const width = Math.max(0, tail.width);
  const height = Math.max(0, tail.height);
  const alignTailToBodyEdge = tail.style === "cut_corner";
  const x =
    tail.side === "right"
      ? body.x + body.width - Math.ceil(width * 0.34)
      : body.x - Math.floor(width * 0.66);
  const y =
    tail.vertical === "top"
      ? alignTailToBodyEdge
        ? body.y
        : body.y + Math.round(tail.cornerRadius * 0.35)
      : alignTailToBodyEdge
        ? body.y + body.height - height
        : body.y + body.height - height - Math.round(tail.cornerRadius * 0.18);
  return { x, y, width, height };
}

function surfaceTailPath({
  height,
  side,
  style,
  vertical,
  width,
}: {
  height: number;
  side: "left" | "right";
  style: string;
  vertical: "top" | "bottom";
  width: number;
}) {
  const left = side === "left";
  const top = vertical === "top";
  const x0 = 0;
  const x1 = width;
  const y0 = 0;
  const y1 = height;

  if (style === "simple_triangle" || style === "cut_corner") {
    if (left && top) return `M ${x1} ${y1} L ${x0} ${y0} L ${x1} ${y0} Z`;
    if (!left && top) return `M ${x0} ${y1} L ${x1} ${y0} L ${x0} ${y0} Z`;
    if (left && !top) return `M ${x1} ${y0} L ${x0} ${y1} L ${x1} ${y1} Z`;
    return `M ${x0} ${y0} L ${x1} ${y1} L ${x0} ${y1} Z`;
  }

  if (style === "curved_hook") {
    if (left && top) {
      return `M ${x1} ${y1} C ${width * 0.68} ${height * 0.78} ${width * 0.42} ${height * 0.38} ${x0} ${y0} C ${width * 0.42} ${height * 0.07} ${width * 0.76} ${height * 0.23} ${x1} ${y0} Z`;
    }
    if (!left && top) {
      return `M ${x0} ${y1} C ${width * 0.32} ${height * 0.78} ${width * 0.58} ${height * 0.38} ${x1} ${y0} C ${width * 0.58} ${height * 0.07} ${width * 0.24} ${height * 0.23} ${x0} ${y0} Z`;
    }
    if (left && !top) {
      return `M ${x1} ${y0} C ${width * 0.68} ${height * 0.22} ${width * 0.42} ${height * 0.62} ${x0} ${y1} C ${width * 0.42} ${height * 0.93} ${width * 0.76} ${height * 0.77} ${x1} ${y1} Z`;
    }
    return `M ${x0} ${y0} C ${width * 0.32} ${height * 0.22} ${width * 0.58} ${height * 0.62} ${x1} ${y1} C ${width * 0.58} ${height * 0.93} ${width * 0.24} ${height * 0.77} ${x0} ${y1} Z`;
  }

  if (left && top) {
    return `M ${x1} ${y1} C ${width * 0.76} ${height * 0.74} ${width * 0.42} ${height * 0.3} ${x0} ${y0} L ${x1} ${y0} Z`;
  }
  if (!left && top) {
    return `M ${x0} ${y1} C ${width * 0.24} ${height * 0.74} ${width * 0.58} ${height * 0.3} ${x1} ${y0} L ${x0} ${y0} Z`;
  }
  if (left && !top) {
    return `M ${x1} ${y0} C ${width * 0.76} ${height * 0.26} ${width * 0.42} ${height * 0.7} ${x0} ${y1} L ${x1} ${y1} Z`;
  }
  return `M ${x0} ${y0} C ${width * 0.24} ${height * 0.26} ${width * 0.58} ${height * 0.7} ${x1} ${y1} L ${x0} ${y1} Z`;
}

function svgAttribute(value: string) {
  return value
    .replace(/&/g, "&amp;")
    .replace(/"/g, "&quot;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;");
}
