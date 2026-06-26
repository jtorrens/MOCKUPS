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
  const shadow = shadowValue(style.shadow);
  const backgroundColor = stringValue(style.backgroundColor ?? style.background);
  const backgroundImage = stringValue(style.backgroundImage);
  const backgroundSize = stringValue(style.backgroundSize);
  const backgroundPosition = stringValue(style.backgroundPosition);
  const backgroundRepeat = stringValue(style.backgroundRepeat);
  const color = stringValue(style.textColor ?? style.color ?? style.foreground);
  const textAlign = stringValue(style.textAlign);
  const borderRadius = numberValue(style.borderRadius ?? style.cornerRadius);
  const borderColor = node.type === "avatar" ? undefined : stringValue(style.borderColor);
  const borderWidth = node.type === "avatar" ? undefined : numberValue(style.borderWidth);
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
    textAlign: textAlign as CSSProperties["textAlign"],
    fontFamily: stringValue(style.fontFamily) ?? "Arial, sans-serif",
    fontSize: numberValue(style.fontSize),
    fontWeight: cssFontWeight(style.fontWeight),
    lineHeight: numberValue(style.lineHeight)
      ? `${numberValue(style.lineHeight)}px`
      : undefined,
    borderRadius,
    overflow:
      node.type === "avatar"
        ? "visible"
        : (stringValue(style.overflow) as CSSProperties["overflow"]),
    opacity,
    boxSizing: "border-box",
    boxShadow:
      node.type === "avatar" || node.type === "message_bubble_shape"
        ? undefined
        : shadow,
    filter:
      node.type === "message_bubble_shape" && shadow
        ? `drop-shadow(${shadow})`
        : undefined,
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

function roundedPolygonPath(
  points: Array<{ x: number; y: number }>,
  radius: number,
) {
  if (points.length < 3 || radius <= 0) {
    return `M ${points.map((point) => `${point.x} ${point.y}`).join(" L ")} Z`;
  }
  const rounded = points.map((point, index) => {
    const previous = points[(index - 1 + points.length) % points.length];
    const next = points[(index + 1) % points.length];
    const previousVector = {
      x: previous.x - point.x,
      y: previous.y - point.y,
    };
    const nextVector = {
      x: next.x - point.x,
      y: next.y - point.y,
    };
    const previousLength = Math.hypot(previousVector.x, previousVector.y);
    const nextLength = Math.hypot(nextVector.x, nextVector.y);
    const offset = Math.min(radius, previousLength * 0.42, nextLength * 0.42);
    const previousPoint = {
      x: point.x + (previousVector.x / previousLength) * offset,
      y: point.y + (previousVector.y / previousLength) * offset,
    };
    const nextPoint = {
      x: point.x + (nextVector.x / nextLength) * offset,
      y: point.y + (nextVector.y / nextLength) * offset,
    };
    return { corner: point, previousPoint, nextPoint };
  });
  const [first, ...rest] = rounded;
  return [
    `M ${first.previousPoint.x} ${first.previousPoint.y}`,
    `Q ${first.corner.x} ${first.corner.y} ${first.nextPoint.x} ${first.nextPoint.y}`,
    ...rest.flatMap((point) => [
      `L ${point.previousPoint.x} ${point.previousPoint.y}`,
      `Q ${point.corner.x} ${point.corner.y} ${point.nextPoint.x} ${point.nextPoint.y}`,
    ]),
    "Z",
  ].join(" ");
}

function generatedNavigationButtonNode(node: RenderableNode): ReactNode {
  const metadata = node.metadata ?? {};
  const filled = metadata.filled === true;
  const strokeWidth = numberValue(metadata.strokeWidth) ?? 2;
  const cornerRadius = numberValue(metadata.cornerRadius) ?? 3;
  const role = String(node.role ?? "generatedHome");
  const backTrianglePath = roundedPolygonPath(
    [
      { x: 64, y: 20 },
      { x: 28, y: 50 },
      { x: 64, y: 80 },
    ],
    cornerRadius,
  );
  const common = {
    vectorEffect: "non-scaling-stroke",
    stroke: "currentColor",
    strokeWidth,
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
  };
  return (
    <svg
      viewBox="0 0 100 100"
      aria-hidden="true"
      style={{
        display: "block",
        width: "1em",
        height: "1em",
        overflow: "visible",
      }}
    >
      {role === "generatedBack" ? (
        <path
          d={backTrianglePath}
          fill={filled ? "currentColor" : "none"}
          {...common}
        />
      ) : role === "generatedRecents" ? (
        <rect
          x="28"
          y="28"
          width="44"
          height="44"
          rx={cornerRadius}
          fill={filled ? "currentColor" : "none"}
          {...common}
        />
      ) : (
        <circle
          cx="50"
          cy="50"
          r="23"
          fill={filled ? "currentColor" : "none"}
          {...common}
        />
      )}
    </svg>
  );
}

function tailSvgPath(style: unknown, side: unknown, vertical: unknown) {
  const tailStyle = stringValue(style) ?? "rounded_wedge";
  const tailSide = side === "left" ? "left" : "right";
  const tailVertical = vertical === "top" ? "top" : "bottom";
  const left = tailSide === "left";
  const top = tailVertical === "top";

  if (tailStyle === "simple_triangle") {
    if (left && top) return "M 100 0 L 0 0 L 100 100 Z";
    if (!left && top) return "M 0 0 L 100 0 L 0 100 Z";
    if (left && !top) return "M 100 0 L 0 100 L 100 100 Z";
    return "M 0 0 L 100 100 L 0 100 Z";
  }

  if (tailStyle === "cut_corner") {
    if (left && top) return "M 100 0 L 0 0 L 100 100 Z";
    if (!left && top) return "M 0 0 L 100 0 L 0 100 Z";
    if (left && !top) return "M 100 0 L 0 100 L 100 100 Z";
    return "M 0 0 L 100 100 L 0 100 Z";
  }

  if (tailStyle === "curved_hook") {
    if (left && top) {
      return "M 100 100 C 68 78 42 38 0 0 C 42 7 76 23 100 0 Z";
    }
    if (!left && top) {
      return "M 0 100 C 32 78 58 38 100 0 C 58 7 24 23 0 0 Z";
    }
    if (left && !top) {
      return "M 100 0 C 68 22 42 62 0 100 C 42 93 76 77 100 100 Z";
    }
    return "M 0 0 C 32 22 58 62 100 100 C 58 93 24 77 0 100 Z";
  }

  if (left && top) {
    return "M 100 100 C 76 74 42 30 0 0 L 100 0 Z";
  }
  if (!left && top) {
    return "M 0 100 C 24 74 58 30 100 0 L 0 0 Z";
  }
  if (left && !top) {
    return "M 100 0 C 76 26 42 70 0 100 L 100 100 Z";
  }
  return "M 0 0 C 24 26 58 70 100 100 L 0 100 Z";
}

function messageBubbleTailNode(node: RenderableNode): ReactNode {
  return (
    <svg
      viewBox="0 0 100 100"
      preserveAspectRatio="none"
      aria-hidden="true"
      style={{
        display: "block",
        width: "100%",
        height: "100%",
        overflow: "visible",
      }}
    >
      <path
        d={tailSvgPath(
          node.style?.tailStyle,
          node.style?.side,
          node.style?.vertical,
        )}
        fill="currentColor"
      />
    </svg>
  );
}

function iconTokenLabel(token: string) {
  const parts = token.split("_").filter(Boolean);
  return parts.at(-1)?.slice(0, 2).toUpperCase() ?? "IC";
}

function inlineCursorFromChildren(node: RenderableNode) {
  const cursorNode = node.children?.find(
    (child) =>
      child.type === "text_input_bar_cursor" || child.type === "message_text_cursor",
  );
  if (!cursorNode) return null;
  const cursorWidth = Math.max(1, numberValue(cursorNode.style?.width) ?? 2);
  const cursorColor =
    stringValue(cursorNode.style?.backgroundColor ?? cursorNode.style?.background) ??
    "currentColor";
  return (
    <span
      aria-hidden="true"
      style={{
        display: "inline-block",
        width: cursorWidth,
        height: "1.05em",
        minWidth: cursorWidth,
        marginLeft: "0.01em",
        background: cursorColor,
        borderRadius: Math.min(cursorWidth * 0.5, 2),
        verticalAlign: "text-bottom",
        opacity: numberValue(cursorNode.style?.opacity) ?? 1,
        flex: "0 0 auto",
      }}
    />
  );
}

function nodeContent(node: RenderableNode): ReactNode {
  if (node.type === "avatar") {
    const label = stringValue(node.metadata?.label) ?? "?";
    const radius = numberValue(node.style?.borderRadius);
    const avatarUri = stringValue(node.asset?.uri);
    const borderColor = stringValue(node.style?.borderColor);
    const borderWidth = numberValue(node.style?.borderWidth);
    const avatarRadius = radius !== undefined ? `${radius}px` : "50%";
    const avatarShadow = shadowValue(node.style?.shadow);
    return (
      <div
        style={{
          position: "relative",
          width: "100%",
          height: "100%",
          borderRadius: avatarRadius,
          overflow: "visible",
          filter: avatarShadow ? `drop-shadow(${avatarShadow})` : undefined,
        }}
        title={node.asset?.uri}
      >
      <div
        style={{
          position: "relative",
          width: "100%",
          height: "100%",
          borderRadius: "inherit",
          overflow: "hidden",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          background: "linear-gradient(145deg, #8e8e93, #3a3a3c)",
          color: "white",
          fontWeight: 700,
          fontSize: "45%",
        }}
      >
        {avatarUri ? (
          <img
            alt={label}
            draggable={false}
            src={avatarUri}
            style={{
              display: "block",
              width: "100%",
              height: "100%",
              objectFit: "cover",
              position: "relative",
              zIndex: 0,
            }}
          />
        ) : (
          Array.from(label)[0]?.toUpperCase() ?? "?"
        )}
        {borderColor && borderWidth && borderWidth > 0 ? (
          <span
            aria-hidden="true"
            style={{
              position: "absolute",
              inset: 0,
              border: `${borderWidth}px solid ${borderColor}`,
              borderRadius: "inherit",
              boxSizing: "border-box",
              pointerEvents: "none",
              zIndex: 1,
            }}
          />
        ) : null}
      </div>
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
  if (node.type === "navigation_bar_item") {
    return generatedNavigationButtonNode(node);
  }
  if (node.type === "message_bubble_tail") {
    return messageBubbleTailNode(node);
  }
  if (node.type === "message_bubble_status_icon") {
    const token = stringValue(node.metadata?.token) ?? node.text ?? "";
    if (stringValue(node.style?.maskImage)) {
      return <span title={token} />;
    }
    return <span title={token}>{node.text}</span>;
  }
  if (node.type === "keyboard_bottom_item") {
    const token = stringValue(node.metadata?.token) ?? node.text ?? "";
    if (stringValue(node.style?.maskImage)) {
      return <span title={token} />;
    }
    return <span title={token}>{iconTokenLabel(token)}</span>;
  }
  if (node.type === "text_input_bar_item") {
    const token = stringValue(node.metadata?.token) ?? node.text ?? "";
    if (stringValue(node.style?.maskImage)) {
      return <span title={token} />;
    }
    return <span title={token}>{iconTokenLabel(token)}</span>;
  }
  if (node.type === "chat_header_icon") {
    const token = stringValue(node.metadata?.token) ?? node.text ?? "";
    if (stringValue(node.style?.maskImage)) {
      return <span title={token} />;
    }
    return <span title={token}>{iconTokenLabel(token)}</span>;
  }
  if (node.type === "keyboard_key_popover") {
    return (
      <>
        <span>{node.text}</span>
        <span
          aria-hidden="true"
          style={{
            position: "absolute",
            left: "50%",
            bottom: "-0.32em",
            width: "0.72em",
            height: "0.44em",
            borderRadius: "0 0 0.22em 0.22em",
            background: "inherit",
            transform: "translateX(-50%)",
          }}
        />
      </>
    );
  }
  if (node.type === "text_input_bar_field") {
    return (
      <>
        {node.text}
        {inlineCursorFromChildren(node)}
      </>
    );
  }
  if (node.type === "text" && node.role === "message_text") {
    return (
      <>
        {node.text}
        {inlineCursorFromChildren(node)}
      </>
    );
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
  const inlineCursorChildTypes = new Set([
    "text_input_bar_cursor",
    "message_text_cursor",
  ]);
  const shouldInlineOwnChildren =
    node.type === "text_input_bar_field" ||
    (node.type === "text" && node.role === "message_text");
  const renderChildren =
    shouldInlineOwnChildren
      ? node.children?.filter((child) => !inlineCursorChildTypes.has(child.type))
      : node.children;
  const semanticStyle: CSSProperties =
    node.type === "status_bar"
    || node.type === "navigation_bar"
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
      : node.type === "status_bar_zone" || node.type === "navigation_bar_zone"
        ? {
            display: "flex",
            alignItems: "center",
            justifyContent:
              stringValue(node.style?.justifyContent) === "flex-start"
                ? "flex-start"
                : stringValue(node.style?.justifyContent) === "center"
                  ? "center"
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
          : node.type === "navigation_bar_item"
            ? {
                display: "inline-flex",
                alignItems: "center",
                justifyContent: "center",
                width: numberValue(node.style?.fontSize),
                height: numberValue(node.style?.lineHeight),
                fontSize: numberValue(node.style?.fontSize),
                lineHeight: numberValue(node.style?.lineHeight)
                  ? `${numberValue(node.style?.lineHeight)}px`
                  : undefined,
              }
            : node.type === "keyboard"
              ? {
                  display: "flex",
                  flexDirection: "column",
                  justifyContent: "flex-end",
                  paddingTop: numberValue(node.style?.paddingTop),
                  paddingLeft: numberValue(node.style?.paddingX),
                  paddingRight: numberValue(node.style?.paddingX),
                  paddingBottom: numberValue(node.style?.paddingBottom),
                  gap: numberValue(node.style?.rowGap),
                }
              : node.type === "keyboard_row"
                ? {
                    display: "flex",
                    alignItems: "center",
                    gap: numberValue(node.style?.gap),
                    height: numberValue(node.style?.keyHeight),
                    width: "100%",
                  }
                : node.type === "keyboard_key"
                  ? {
                      position: "relative",
                      display: "inline-flex",
                      alignItems: "center",
                      justifyContent: "center",
                      flex: `${numberValue(node.style?.weight) ?? 1} 1 0`,
                      height: "100%",
                      minWidth: 0,
                      boxShadow:
                        "0 0.06em 0.08em rgba(0, 0, 0, 0.18), 0 0.01em 0 rgba(255, 255, 255, 0.32) inset",
                      whiteSpace: "nowrap",
                      overflow: "visible",
                    }
                  : node.type === "keyboard_key_popover"
                    ? {
                        position: "absolute",
                        left: "50%",
                        bottom: "calc(100% + 0.18em)",
                        display: "inline-flex",
                        alignItems: "center",
                        justifyContent: "center",
                        width: `${(numberValue(node.style?.widthRatio) ?? 0.86) * 100}%`,
                        minWidth: "1.5em",
                        height: "2.18em",
                        boxShadow: "0 0.16em 0.34em rgba(0, 0, 0, 0.26)",
                        transform: "translateX(-50%)",
                        zIndex: 2,
                        pointerEvents: "none",
                        whiteSpace: "nowrap",
                      }
                  : node.type === "keyboard_bottom_utility"
                    ? {
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        height: numberValue(node.style?.height),
                        paddingLeft: numberValue(node.style?.paddingX),
                        paddingRight: numberValue(node.style?.paddingX),
                        flex: "0 0 auto",
                      }
                    : node.type === "keyboard_bottom_zone"
                      ? {
                          display: "flex",
                          alignItems: "center",
                          justifyContent:
                            stringValue(node.style?.justifyContent) === "flex-start"
                              ? "flex-start"
                              : "flex-end",
                          gap: numberValue(node.style?.gap),
                          flex: "1 1 0",
                          minWidth: 0,
                        }
                      : node.type === "keyboard_bottom_item"
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
                            backgroundColor: stringValue(node.style?.maskImage)
                              ? "currentColor"
                              : undefined,
                            maskImage: stringValue(node.style?.maskImage),
                            maskPosition: stringValue(node.style?.maskImage)
                              ? "center"
                              : undefined,
                            maskRepeat: stringValue(node.style?.maskImage)
                              ? "no-repeat"
                              : undefined,
                            maskSize: stringValue(node.style?.maskImage)
                              ? "contain"
                              : undefined,
                            WebkitMaskImage: stringValue(
                              node.style?.WebkitMaskImage,
                            ),
                            WebkitMaskPosition: stringValue(
                              node.style?.WebkitMaskImage,
                            )
                              ? "center"
                              : undefined,
                            WebkitMaskRepeat: stringValue(
                              node.style?.WebkitMaskImage,
                            )
                              ? "no-repeat"
                              : undefined,
                            WebkitMaskSize: stringValue(
                              node.style?.WebkitMaskImage,
                            )
                              ? "contain"
                              : undefined,
                          }
                        : node.type === "chat_header_icon"
                          ? {
                              display: "inline-flex",
                              alignItems: "center",
                              justifyContent: "center",
                              width: numberValue(node.style?.fontSize),
                              height: numberValue(node.style?.lineHeight),
                              fontSize: numberValue(node.style?.fontSize),
                              lineHeight: numberValue(node.style?.lineHeight)
                                ? `${numberValue(node.style?.lineHeight)}px`
                                : undefined,
                              backgroundColor: stringValue(node.style?.maskImage)
                                ? "currentColor"
                                : undefined,
                              maskImage: stringValue(node.style?.maskImage),
                              maskPosition: stringValue(node.style?.maskImage)
                                ? "center"
                                : undefined,
                              maskRepeat: stringValue(node.style?.maskImage)
                                ? "no-repeat"
                                : undefined,
                              maskSize: stringValue(node.style?.maskImage)
                                ? "contain"
                                : undefined,
                              WebkitMaskImage: stringValue(
                                node.style?.WebkitMaskImage,
                              ),
                              WebkitMaskPosition: stringValue(
                                node.style?.WebkitMaskImage,
                              )
                                ? "center"
                                : undefined,
                              WebkitMaskRepeat: stringValue(
                                node.style?.WebkitMaskImage,
                              )
                                ? "no-repeat"
                                : undefined,
                              WebkitMaskSize: stringValue(
                                node.style?.WebkitMaskImage,
                              )
                                ? "contain"
                                : undefined,
                            }
                        : node.type === "text_input_bar"
                        ? {
                            display: "flex",
                            alignItems: "center",
                            gap: numberValue(node.style?.gap),
                            paddingLeft: numberValue(node.style?.paddingX),
                            paddingRight: numberValue(node.style?.paddingX),
                            paddingTop: numberValue(node.style?.paddingY),
                            paddingBottom: numberValue(node.style?.paddingY),
                            fontSize: numberValue(node.style?.fontSize),
                            lineHeight: numberValue(node.style?.lineHeight)
                              ? `${numberValue(node.style?.lineHeight)}px`
                              : undefined,
                          }
                        : node.type === "text_input_bar_icon_zone"
                          ? {
                              display: "flex",
                              alignItems: "center",
                              justifyContent:
                                node.role === "right"
                                  ? "flex-end"
                                  : "flex-start",
                              gap: numberValue(node.style?.gap),
                              flex: "0 0 auto",
                            }
                          : node.type === "text_input_bar_item"
                            ? {
                                display: "inline-flex",
                                alignItems: "center",
                                justifyContent: "center",
                                width: numberValue(node.style?.fontSize),
                                height: numberValue(node.style?.lineHeight),
                                fontSize: numberValue(node.style?.fontSize),
                                lineHeight: numberValue(node.style?.lineHeight)
                                  ? `${numberValue(node.style?.lineHeight)}px`
                                  : undefined,
                                backgroundColor: stringValue(node.style?.maskImage)
                                  ? "currentColor"
                                  : undefined,
                                maskImage: stringValue(node.style?.maskImage),
                                maskPosition: stringValue(node.style?.maskImage)
                                  ? "center"
                                  : undefined,
                                maskRepeat: stringValue(node.style?.maskImage)
                                  ? "no-repeat"
                                  : undefined,
                                maskSize: stringValue(node.style?.maskImage)
                                  ? "contain"
                                  : undefined,
                                WebkitMaskImage: stringValue(
                                  node.style?.WebkitMaskImage,
                                ),
                                WebkitMaskPosition: stringValue(
                                  node.style?.WebkitMaskImage,
                                )
                                  ? "center"
                                  : undefined,
                                WebkitMaskRepeat: stringValue(
                                  node.style?.WebkitMaskImage,
                                )
                                  ? "no-repeat"
                                  : undefined,
                                WebkitMaskSize: stringValue(
                                  node.style?.WebkitMaskImage,
                                )
                                  ? "contain"
                                  : undefined,
                              }
                            : node.type === "text_input_bar_field"
                              ? {
                                  position: "relative",
                                  display: "block",
                                  flex: "1 1 auto",
                                  minWidth: 0,
                                  height: numberValue(node.style?.height),
                                  paddingLeft: numberValue(node.style?.paddingX),
                                  paddingRight: numberValue(node.style?.paddingX),
                                  paddingTop: numberValue(node.style?.paddingY),
                                  paddingBottom: numberValue(node.style?.paddingY),
                                  overflow: "hidden",
                                  whiteSpace:
                                    stringValue(node.style?.whiteSpace) ??
                                    "pre-wrap",
                                  lineHeight: numberValue(node.style?.lineHeight)
                                    ? `${numberValue(node.style?.lineHeight)}px`
                                    : undefined,
                                }
                              : node.type === "text_input_bar_cursor"
                                ? {
                                    display: "inline-block",
                                    width: numberValue(node.style?.width),
                                    height: "1.1em",
                                    marginLeft: "0.02em",
                                    borderRadius: 999,
                                    flex: "0 0 auto",
                                  }
      : node.type === "message_bubble"
        ? { display: "block", overflow: "visible" }
        : node.type === "message_bubble_shape"
          ? { display: "block", overflow: "visible" }
          : node.type === "message_bubble_body"
            ? { display: "block" }
        : node.type === "message_bubble_tail"
          ? {
              display: "block",
              backgroundColor: "transparent",
              color:
                stringValue(node.style?.backgroundColor ?? node.style?.background) ??
                "currentColor",
              overflow: "visible",
              pointerEvents: "none",
            }
        : node.type === "message_bubble_status"
          ? {
              display: "block",
              overflow: "visible",
              pointerEvents: "none",
            }
        : node.type === "message_bubble_status_text"
          ? {
              display: "block",
              whiteSpace: "nowrap",
              overflow: "visible",
            }
        : node.type === "message_bubble_status_icon"
          ? {
              display: "inline-flex",
              alignItems: "center",
              justifyContent: "center",
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
        : node.type === "text"
          ? {
              whiteSpace: "pre-wrap",
              overflow: "hidden",
              display: "inline",
            }
          : node.type === "message_text_cursor"
            ? {
                display: "inline-block",
                width: numberValue(node.style?.width),
                height: "1.1em",
                marginLeft: "0.02em",
                borderRadius: 999,
                verticalAlign: "text-bottom",
              }
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
      {renderChildren?.map((child) => (
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
