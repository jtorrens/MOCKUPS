import type { CSSProperties, ReactNode } from "react";
import type {
  RenderableBox,
  RenderableNode,
} from "../visual/renderable/types.js";

export interface RemotionRenderableAdapterProps {
  tree: RenderableNode;
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function stringValue(value: unknown): string | undefined {
  return typeof value === "string" ? value : undefined;
}

function numberValue(value: unknown): number | undefined {
  return typeof value === "number" ? value : undefined;
}

function cssFontWeight(value: unknown): CSSProperties["fontWeight"] | undefined {
  if (typeof value === "number") return value;
  if (typeof value !== "string") return undefined;
  const normalized = value.toLowerCase().replace(/[\s_-]+/g, "");
  if (normalized.includes("thin")) return 100;
  if (normalized.includes("extralight") || normalized.includes("ultralight")) {
    return 200;
  }
  if (normalized.includes("light")) return 300;
  if (
    normalized.includes("regular") ||
    normalized.includes("normal") ||
    normalized.includes("book")
  ) {
    return 400;
  }
  if (normalized.includes("medium")) return 500;
  if (normalized.includes("semibold") || normalized.includes("demibold")) {
    return 600;
  }
  if (normalized.includes("bold")) return 700;
  if (normalized.includes("extrabold") || normalized.includes("ultrabold")) {
    return 800;
  }
  if (normalized.includes("black") || normalized.includes("heavy")) return 900;
  return value as CSSProperties["fontWeight"];
}

function boxStyle(
  box: RenderableBox | undefined,
  parentOrigin: { x: number; y: number },
): CSSProperties {
  if (!box) {
    return { position: "relative" };
  }
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
  const color = stringValue(shadow.color);
  if (!color) {
    return undefined;
  }
  return `${numberValue(shadow.offsetX) ?? 0}px ${numberValue(shadow.offsetY) ?? 0}px ${numberValue(shadow.blur) ?? 0}px ${color}`;
}

function nodeStyle(
  node: RenderableNode,
  parentOrigin: { x: number; y: number },
): CSSProperties {
  const style = node.style ?? {};
  const backgroundColor = stringValue(style.backgroundColor ?? style.background);
  const color = stringValue(style.textColor ?? style.color ?? style.foreground);
  const borderRadius = numberValue(style.borderRadius ?? style.cornerRadius);
  const opacity = node.transform?.opacity;
  const separatorWidth = numberValue(style.separatorWidth);
  return {
    ...boxStyle(node.box, parentOrigin),
    backgroundColor,
    color,
    fontFamily: stringValue(style.fontFamily) ?? "Arial, sans-serif",
    fontSize: numberValue(style.fontSize),
    fontWeight: cssFontWeight(style.fontWeight),
    lineHeight: numberValue(style.lineHeight)
      ? `${numberValue(style.lineHeight)}px`
      : undefined,
    borderRadius,
    overflow: stringValue(style.overflow) as CSSProperties["overflow"],
    opacity,
    boxSizing: "border-box",
    boxShadow: shadowValue(style.shadow),
    borderBottom:
      separatorWidth && separatorWidth > 0
        ? `${separatorWidth}px solid ${stringValue(style.separatorColor) ?? "transparent"}`
        : undefined,
  };
}

function statusIndicatorText(node: RenderableNode): string {
  const metadata = node.metadata ?? {};
  const signalBars = numberValue(metadata.signalBars) ?? 0;
  const wifi = metadata.wifiEnabled
    ? metadata.wifiIconState === "connected"
      ? "Wi-Fi"
      : "Wi-Fi·"
    : "";
  const battery = Math.round((numberValue(metadata.batteryLevel) ?? 0) * 100);
  return `${"▮".repeat(signalBars)}  ${wifi}  ${battery}%`;
}

function nodeContent(node: RenderableNode): ReactNode {
  if (node.type === "avatar") {
    const label = stringValue(node.metadata?.label) ?? "?";
    return (
      <div
        style={{
          width: "100%",
          height: "100%",
          borderRadius: "50%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          background: "linear-gradient(145deg, #8e8e93, #3a3a3c)",
          color: "white",
          fontWeight: 700,
          fontSize: "45%",
        }}
        title={node.asset?.uri}
      >
        {Array.from(label)[0]?.toUpperCase() ?? "?"}
      </div>
    );
  }
  if (node.type === "status_indicators") {
    return statusIndicatorText(node);
  }
  if (node.children?.some((child) => child.type === "text")) {
    return null;
  }
  return node.text;
}

function RenderNode({
  node,
  parentOrigin,
}: {
  node: RenderableNode;
  parentOrigin: { x: number; y: number };
}) {
  const currentOrigin = node.box
    ? { x: node.box.x, y: node.box.y }
    : parentOrigin;
  const hasPositionlessChildren = node.children?.some(
    (child) => child.box === undefined,
  );
  const semanticStyle: CSSProperties =
    node.type === "status_bar"
      ? {
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          padding: "0 48px",
          fontSize: 42,
          fontWeight: 600,
        }
      : node.type === "message_bubble"
        ? { display: "block" }
        : node.type === "text"
          ? { whiteSpace: "pre-wrap", overflow: "hidden" }
          : {};

  return (
    <div
      data-renderable-type={node.type}
      data-renderable-role={node.role}
      style={{
        ...nodeStyle(node, parentOrigin),
        ...semanticStyle,
        ...(hasPositionlessChildren ? { gap: 20 } : {}),
      }}
    >
      {nodeContent(node)}
      {node.children?.map((child) => (
        <RenderNode key={child.id} node={child} parentOrigin={currentOrigin} />
      ))}
    </div>
  );
}

export const RemotionRenderableAdapter = ({
  tree,
}: RemotionRenderableAdapterProps) => {
  return <RenderNode node={tree} parentOrigin={{ x: 0, y: 0 }} />;
};
