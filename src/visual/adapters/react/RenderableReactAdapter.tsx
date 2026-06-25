import type { CSSProperties, ReactNode } from "react";
import type {
  RenderableBox,
  RenderableNode,
} from "../../renderable/types.js";

export interface RenderableReactAdapterProps {
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
  const backgroundImage = stringValue(style.backgroundImage);
  const backgroundSize = stringValue(style.backgroundSize);
  const backgroundPosition = stringValue(style.backgroundPosition);
  const backgroundRepeat = stringValue(style.backgroundRepeat);
  const color = stringValue(style.textColor ?? style.color ?? style.foreground);
  const borderRadius = numberValue(style.borderRadius ?? style.cornerRadius);
  const borderColor = stringValue(style.borderColor);
  const borderWidth = numberValue(style.borderWidth);
  const opacity = node.transform?.opacity;
  const separatorWidth = numberValue(style.separatorWidth);
  return {
    ...boxStyle(node.box, parentOrigin),
    backgroundColor,
    backgroundImage,
    backgroundSize,
    backgroundPosition,
    backgroundRepeat,
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
    border:
      borderColor && borderWidth
        ? `${borderWidth}px solid ${borderColor}`
        : undefined,
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

function generatedBatteryNode(node: RenderableNode): ReactNode {
  const metadata = node.metadata ?? {};
  const level = Math.max(0, Math.min(100, numberValue(metadata.value) ?? 0));
  const charging = metadata.charging === true;
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
      }}
      title={`Battery ${level}%${charging ? " charging" : ""}`}
    >
      <span
        style={{
          position: "relative",
          display: "inline-block",
          width: "1.55em",
          height: "0.72em",
          border: "0.11em solid currentColor",
          borderRadius: "0.18em",
          boxSizing: "border-box",
        }}
      >
        <span
          style={{
            position: "absolute",
            left: "0.11em",
            top: "0.11em",
            bottom: "0.11em",
            width: `calc(${level}% - 0.22em)`,
            maxWidth: "calc(100% - 0.22em)",
            borderRadius: "0.08em",
            background: "currentColor",
          }}
        />
        <span
          style={{
            position: "absolute",
            right: "-0.26em",
            top: "0.2em",
            width: "0.16em",
            height: "0.24em",
            borderRadius: "0 0.08em 0.08em 0",
            background: "currentColor",
          }}
        />
        {charging ? (
          <span
            style={{
              position: "absolute",
              left: "50%",
              top: "50%",
              display: "block",
              width: "0.5em",
              height: "0.82em",
              background: "#34c759",
              clipPath:
                "polygon(58% 0, 18% 48%, 45% 48%, 32% 100%, 84% 38%, 56% 38%)",
              filter: "drop-shadow(0 0 0.04em rgba(0, 0, 0, 0.36))",
              transform: "translate(-50%, -50%)",
            }}
          />
        ) : null}
      </span>
    </span>
  );
}

function generatedSignalNode(node: RenderableNode): ReactNode {
  const bars = Math.max(0, Math.min(4, numberValue(node.metadata?.value) ?? 0));
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "flex-end",
        gap: "0.12em",
        height: "0.85em",
      }}
      title={`Signal ${bars}`}
    >
      {[1, 2, 3, 4].map((bar) => (
        <span
          key={bar}
          style={{
            width: "0.18em",
            height: `${bar * 22}%`,
            borderRadius: "0.06em",
            background: "currentColor",
            opacity: bar <= bars ? 1 : 0.24,
          }}
        />
      ))}
    </span>
  );
}

function iconTokenLabel(token: string) {
  const parts = token.split("_").filter(Boolean);
  return parts.at(-1)?.slice(0, 2).toUpperCase() ?? "IC";
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
  if (node.type === "status_bar_item") {
    if (node.role === "generatedBattery") return generatedBatteryNode(node);
    if (node.role === "generatedSignal") return generatedSignalNode(node);
    if (node.role === "iconToken") {
      const token = stringValue(node.metadata?.token) ?? node.text ?? "";
      if (stringValue(node.style?.maskImage)) {
        return <span title={token} />;
      }
      return <span title={token}>{iconTokenLabel(token)}</span>;
    }
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
          paddingLeft: numberValue(node.style?.paddingX) ?? 48,
          paddingRight: numberValue(node.style?.paddingX) ?? 48,
          fontSize: numberValue(node.style?.fontSize) ?? 42,
          lineHeight: numberValue(node.style?.lineHeight)
            ? `${numberValue(node.style?.lineHeight)}px`
            : undefined,
          fontWeight: 600,
        }
      : node.type === "status_bar_zone"
        ? {
            display: "flex",
            alignItems: "center",
            justifyContent:
              stringValue(node.style?.justifyContent) === "flex-start"
                ? "flex-start"
                : "flex-end",
            gap: numberValue(node.style?.gap) ?? 6,
            minWidth: 0,
            flex: "1 1 0",
          }
        : node.type === "status_bar_item"
          ? {
              display: "inline-flex",
              alignItems: "center",
              justifyContent: "center",
              minWidth: stringValue(node.style?.maskImage)
                ? numberValue(node.style?.fontSize)
                : 0,
              width: stringValue(node.style?.maskImage)
                ? numberValue(node.style?.fontSize)
                : undefined,
              height: numberValue(node.style?.lineHeight),
              fontSize: numberValue(node.style?.fontSize),
              lineHeight: numberValue(node.style?.lineHeight)
                ? `${numberValue(node.style?.lineHeight)}px`
                : undefined,
              whiteSpace: "nowrap",
              backgroundColor: stringValue(node.style?.maskImage)
                ? "currentColor"
                : undefined,
              maskImage: stringValue(node.style?.maskImage),
              maskPosition: stringValue(node.style?.maskImage) ? "center" : undefined,
              maskRepeat: stringValue(node.style?.maskImage) ? "no-repeat" : undefined,
              maskSize: stringValue(node.style?.maskImage) ? "contain" : undefined,
              WebkitMaskImage: stringValue(node.style?.WebkitMaskImage),
              WebkitMaskPosition: stringValue(node.style?.WebkitMaskImage)
                ? "center"
                : undefined,
              WebkitMaskRepeat: stringValue(node.style?.WebkitMaskImage)
                ? "no-repeat"
                : undefined,
              WebkitMaskSize: stringValue(node.style?.WebkitMaskImage)
                ? "contain"
                : undefined,
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

export const RenderableReactAdapter = ({
  tree,
}: RenderableReactAdapterProps) => {
  return <RenderNode node={tree} parentOrigin={{ x: 0, y: 0 }} />;
};
