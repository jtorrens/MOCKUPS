import { useEffect, useState, type CSSProperties, type ReactNode } from "react";
import type {
  RenderableBox,
  RenderableNode,
} from "../../renderable/types.js";

export interface RenderableReactAdapterProps {
  tree: RenderableNode;
  showBounds?: boolean;
}

function cssString(value: unknown): string {
  return String(value ?? "").replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function stringValue(value: unknown): string | undefined {
  return typeof value === "string" ? value : undefined;
}

function extractCssUrl(value: unknown) {
  if (typeof value !== "string") return "";
  const match = /^url\((['"]?)(.*?)\1\)$/i.exec(value.trim());
  return match?.[2] ?? "";
}

function numberValue(value: unknown): number | undefined {
  return typeof value === "number" ? value : undefined;
}

function fontFaceCss(tree: RenderableNode) {
  const fontFaces = Array.isArray(tree.metadata?.fontFaces)
    ? tree.metadata.fontFaces
    : [];
  return fontFaces
    .filter(isRecord)
    .map((fontFace) => {
      const family = stringValue(fontFace.family);
      const uri = stringValue(fontFace.uri);
      if (!family || !uri) return "";
      const weight =
        typeof fontFace.weight === "number" || typeof fontFace.weight === "string"
          ? fontFace.weight
          : 400;
      const style = stringValue(fontFace.style) ?? "normal";
      return `@font-face{font-family:"${cssString(family)}";src:url("${cssString(uri)}");font-weight:${weight};font-style:${cssString(style)};font-display:block;}`;
    })
    .filter(Boolean)
    .join("\n");
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

function intensityColor(value: unknown): string | undefined {
  const intensity = numberValue(value);
  if (intensity === undefined || intensity === 0) {
    return undefined;
  }
  const alpha = Math.min(1, Math.abs(intensity));
  return intensity > 0
    ? `rgba(255, 255, 255, ${alpha})`
    : `rgba(0, 0, 0, ${alpha})`;
}

function surfaceReliefValue(value: unknown): string | undefined {
  const relief = asRecord(value);
  if (!Object.keys(relief).length) {
    return undefined;
  }
  const angleDeg = numberValue(relief.angleDeg) ?? -45;
  const extension = numberValue(relief.extension) ?? 1;
  const spread = numberValue(relief.spread) ?? 0;
  const angleRad = (angleDeg * Math.PI) / 180;
  const x = Math.cos(angleRad) * extension;
  const y = Math.sin(angleRad) * extension;
  const upperColor = intensityColor(relief.upperIntensity);
  const lowerColor = intensityColor(relief.lowerIntensity);
  return [
    upperColor
      ? `inset ${x}px ${y}px ${spread}px ${upperColor}`
      : undefined,
    lowerColor
      ? `inset ${-x}px ${-y}px ${spread}px ${lowerColor}`
      : undefined,
  ]
    .filter(Boolean)
    .join(", ") || undefined;
}

function surfaceReliefParts(value: unknown) {
  const relief = asRecord(value);
  if (!Object.keys(relief).length) {
    return [];
  }
  const angleDeg = numberValue(relief.angleDeg) ?? -45;
  const extension = numberValue(relief.extension) ?? 1;
  const angleRad = (angleDeg * Math.PI) / 180;
  const x = Math.cos(angleRad) * extension;
  const y = Math.sin(angleRad) * extension;
  const upperColor = intensityColor(relief.upperIntensity);
  const lowerColor = intensityColor(relief.lowerIntensity);
  return [
    upperColor ? { color: upperColor, x, y } : undefined,
    lowerColor ? { color: lowerColor, x: -x, y: -y } : undefined,
  ].filter(Boolean) as Array<{ color: string; x: number; y: number }>;
}

function surfaceReliefDropShadowValue(value: unknown): string | undefined {
  const relief = asRecord(value);
  const spread = numberValue(relief.spread) ?? 0;
  return surfaceReliefParts(value)
    .map((part) => `drop-shadow(${part.x}px ${part.y}px ${spread}px ${part.color})`)
    .join(" ") || undefined;
}

function joinedBoxShadow(...values: Array<string | undefined>) {
  return values.filter(Boolean).join(", ") || undefined;
}

function joinedFilter(...values: Array<string | undefined>) {
  return values.filter(Boolean).join(" ") || undefined;
}

function shapeBorderFilterValue(style: Record<string, unknown>) {
  const color = stringValue(style.borderColor);
  const width = numberValue(style.borderWidth);
  if (!color || color === "transparent" || !width || width <= 0) {
    return undefined;
  }
  return [
    `drop-shadow(${width}px 0 0 ${color})`,
    `drop-shadow(${-width}px 0 0 ${color})`,
    `drop-shadow(0 ${width}px 0 ${color})`,
    `drop-shadow(0 ${-width}px 0 ${color})`,
  ].join(" ");
}

function visualSurfaceFilterValue(
  style: Record<string, unknown>,
  options: { includeBorder?: boolean; includeShadow?: boolean; includeRelief?: boolean } = {},
) {
  const shadow = options.includeShadow === false ? undefined : shadowValue(style.shadow);
  return joinedFilter(
    options.includeBorder === false ? undefined : shapeBorderFilterValue(style),
    shadow ? `drop-shadow(${shadow})` : undefined,
    options.includeRelief === false
      ? undefined
      : surfaceReliefDropShadowValue(style.surfaceRelief),
  );
}

function buttonIconVisualStyle(value: unknown): CSSProperties {
  const buttonIcon = asRecord(value);
  const borderWidth = numberValue(buttonIcon.borderWidth) ?? 0;
  const borderColor = stringValue(buttonIcon.borderColor) ?? "transparent";
  const borderRing =
    borderWidth > 0 ? `0 0 0 ${borderWidth}px ${borderColor}` : undefined;
  return {
    borderRadius: numberValue(buttonIcon.cornerRadius),
    boxShadow: joinedBoxShadow(
      borderRing,
      buttonIcon.shadowEnabled === true
        ? shadowValue(buttonIcon.shadow)
        : undefined,
      buttonIcon.surfaceReliefEnabled === true
        ? surfaceReliefValue(buttonIcon.surfaceRelief)
        : undefined,
    ),
  };
}

function buttonIconOuterSize(style: Record<string, unknown> | undefined) {
  const iconSize = numberValue(style?.fontSize);
  if (iconSize == null) return undefined;
  const buttonIcon = asRecord(style?.buttonIcon);
  const iconPadding = numberValue(buttonIcon.iconPadding) ?? 0;
  return iconSize + iconPadding * 2;
}

function nodeStyle(
  node: RenderableNode,
  parentOrigin: { x: number; y: number },
): CSSProperties {
  const style = node.style ?? {};
  const shadow = shadowValue(style.shadow);
  const surfaceRelief = surfaceReliefValue(style.surfaceRelief);
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
  const zIndex = numberValue(style.zIndex);
  const opacity = node.transform?.opacity;
  const separatorWidth = numberValue(style.separatorWidth);
  return {
    ...boxStyle(node.box, parentOrigin),
    backgroundColor,
    backgroundImage:
      node.type === "message_bubble_media_image" ? undefined : backgroundImage,
    backgroundSize,
    backgroundPosition,
    backgroundRepeat,
    color,
    textAlign: textAlign as CSSProperties["textAlign"],
    fontFamily: stringValue(style.fontFamily),
    fontSize: numberValue(style.fontSize),
    fontStyle: stringValue(style.fontStyle) as CSSProperties["fontStyle"],
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
    zIndex,
    boxSizing: "border-box",
    boxShadow:
      node.type === "avatar" ||
      node.type === "message_bubble_shape" ||
      node.type === "message_bubble_media_image"
        ? undefined
        : joinedBoxShadow(shadow, surfaceRelief),
    filter:
      node.type === "message_bubble_shape"
        ? visualSurfaceFilterValue(style)
        : node.type === "message_bubble_media_image" && shadow
          ? visualSurfaceFilterValue(style, {
              includeBorder: false,
              includeRelief: false,
            })
          : undefined,
    border:
      node.type !== "message_bubble_shape" && borderColor && borderWidth
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
  const borderColor = stringValue(node.style?.borderColor);
  const borderWidth = numberValue(node.style?.borderWidth);
  const strokeWidth =
    borderColor && borderWidth
      ? Math.max(0.1, borderWidth)
      : undefined;
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
        stroke={borderColor}
        strokeWidth={strokeWidth}
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  );
}

function iconTokenLabel(token: string) {
  const parts = token.split("_").filter(Boolean);
  return parts.at(-1)?.slice(0, 2).toUpperCase() ?? "IC";
}

function iconButtonContent(node: RenderableNode): ReactNode {
  const token = stringValue(node.metadata?.token) ?? node.text ?? "";
  const buttonIcon = asRecord(node.style?.buttonIcon);
  const iconSize = numberValue(node.style?.fontSize) ?? 16;
  const iconPadding = numberValue(buttonIcon.iconPadding) ?? 0;
  const maskImage = stringValue(node.style?.maskImage);
  const webkitMaskImage = stringValue(node.style?.WebkitMaskImage);
  const labelEnabled = buttonIcon.labelEnabled === true;
  const labelPosition =
    stringValue(buttonIcon.labelPosition) === "top" ? "top" : "bottom";
  const label = stringValue(node.metadata?.label) ?? token;
  const labelPadding = numberValue(buttonIcon.labelPadding) ?? 0;
  const labelNode = labelEnabled ? (
    <span
      style={{
        color: stringValue(buttonIcon.labelColor) ?? "currentColor",
        fontSize: numberValue(buttonIcon.labelSize) ?? Math.max(8, iconSize * 0.42),
        lineHeight: 1,
        marginBottom: labelPosition === "top" ? labelPadding : undefined,
        marginTop: labelPosition === "bottom" ? labelPadding : undefined,
        whiteSpace: "nowrap",
      }}
    >
      {label}
    </span>
  ) : null;
  const glyph = maskImage || webkitMaskImage ? (
    <span
      title={token}
      aria-hidden="true"
      style={{
        display: "inline-block",
        width: iconSize,
        height: iconSize,
        backgroundColor: "currentColor",
        maskImage,
        maskPosition: "center",
        maskRepeat: "no-repeat",
        maskSize: "contain",
        WebkitMaskImage: webkitMaskImage,
        WebkitMaskPosition: "center",
        WebkitMaskRepeat: "no-repeat",
        WebkitMaskSize: "contain",
      }}
    />
  ) : (
    <span title={token}>{iconTokenLabel(token)}</span>
  );
  const glyphBox = (
    <span
      style={{
        ...buttonIconVisualStyle(buttonIcon),
        alignItems: "center",
        boxSizing: "content-box",
        display: "inline-flex",
        height: iconSize,
        justifyContent: "center",
        padding: iconPadding,
        width: iconSize,
      }}
    >
      {glyph}
    </span>
  );
  return (
    <>
      {labelPosition === "top" ? labelNode : null}
      {glyphBox}
      {labelPosition === "bottom" ? labelNode : null}
    </>
  );
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

function keyboardEmojiModeGlyph(node: RenderableNode): ReactNode {
  const size = (numberValue(node.style?.fontSize) ?? 18) * 0.9;
  return (
    <svg
      viewBox="0 0 24 24"
      aria-hidden="true"
      style={{
        display: "block",
        width: size,
        height: size,
        overflow: "visible",
      }}
    >
      <circle
        cx="12"
        cy="12"
        r="9"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.1"
      />
      <circle cx="8.7" cy="9.4" r="0.75" fill="currentColor" />
      <circle cx="15.3" cy="9.4" r="0.75" fill="currentColor" />
      <path
        d="M8.4 14.1c1.05 1.45 2.25 2.15 3.6 2.15s2.55-.7 3.6-2.15"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.1"
      />
    </svg>
  );
}

function StableBackgroundImage({ node }: { node: RenderableNode }) {
  const requestedBackground = stringValue(node.style?.backgroundImage);
  const requestedUrl = extractCssUrl(requestedBackground);
  const [visibleBackground, setVisibleBackground] = useState(requestedBackground);

  useEffect(() => {
    if (!requestedBackground || !requestedUrl) {
      setVisibleBackground(requestedBackground);
      return undefined;
    }
    let cancelled = false;
    const image = new Image();
    image.onload = () => {
      if (!cancelled) setVisibleBackground(requestedBackground);
    };
    image.src = requestedUrl;
    return () => {
      cancelled = true;
    };
  }, [requestedBackground, requestedUrl]);

  if (!visibleBackground) return null;
  return (
    <div
      aria-hidden="true"
      style={{
        position: "absolute",
        inset: 0,
        backgroundImage: visibleBackground,
        backgroundPosition: stringValue(node.style?.backgroundPosition) ?? "center",
        backgroundRepeat: stringValue(node.style?.backgroundRepeat) ?? "no-repeat",
        backgroundSize: stringValue(node.style?.backgroundSize) ?? "cover",
        pointerEvents: "none",
      }}
    />
  );
}

function nodeContent(node: RenderableNode): ReactNode {
  if (node.type === "message_bubble_media_image") {
    const mediaType = stringValue(node.metadata?.type) ?? "image";
    const uri = stringValue(node.metadata?.uri);
    const hasPosterBackground = stringValue(node.style?.backgroundImage) !== undefined;
    const scale = numberValue(node.metadata?.scale) ?? 1;
    const translateX = numberValue(node.metadata?.translateX) ?? 0;
    const translateY = numberValue(node.metadata?.translateY) ?? 0;
    if (hasPosterBackground) return <StableBackgroundImage node={node} />;
    if (mediaType !== "video" || !uri) return node.text;
    return (
      <video
        aria-hidden="true"
        autoPlay
        loop
        muted
        playsInline
        preload="metadata"
        src={uri}
        style={{
          position: "absolute",
          left: "50%",
          top: "50%",
          width: "100%",
          height: "100%",
          objectFit: "cover",
          pointerEvents: "none",
          transform: `translate(calc(-50% + ${translateX}px), calc(-50% + ${translateY}px)) scale(${scale})`,
          transformOrigin: "center",
        }}
      />
    );
  }
  if (node.type === "avatar") {
    const label = stringValue(node.metadata?.label) ?? "?";
    const radius = numberValue(node.style?.borderRadius);
    const avatarUri = stringValue(node.asset?.uri);
    const borderColor = stringValue(node.style?.borderColor);
    const borderWidth = numberValue(node.style?.borderWidth);
    const avatarRadius = radius !== undefined ? `${radius}px` : "50%";
    const avatarShadow = shadowValue(node.style?.shadow);
    const avatarSurfaceRelief = surfaceReliefValue(node.style?.surfaceRelief);
    const avatarBackground =
      stringValue(node.style?.backgroundColor ?? node.style?.background) ??
      "linear-gradient(145deg, #8e8e93, #3a3a3c)";
    const avatarColor = stringValue(node.style?.color ?? node.style?.textColor) ?? "white";
    const imageScale = Math.max(0.01, numberValue(node.metadata?.imageScale) ?? 1);
    const imageBaseSize = Math.max(1, numberValue(node.metadata?.imageBaseSize) ?? 640);
    const avatarBoxWidth = Math.max(1, numberValue(node.box?.width) ?? 1);
    const imageOffsetX =
      ((numberValue(node.metadata?.imageOffsetX) ?? 0) / imageBaseSize) *
      avatarBoxWidth;
    const imageOffsetY =
      ((numberValue(node.metadata?.imageOffsetY) ?? 0) / imageBaseSize) *
      avatarBoxWidth;
    return (
      <div
        style={{
          position: "relative",
          width: "100%",
          height: "100%",
          borderRadius: avatarRadius,
          overflow: "visible",
          boxShadow: avatarShadow,
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
          background: avatarBackground,
          color: avatarColor,
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
              width: `${imageScale * 100}%`,
              height: `${imageScale * 100}%`,
              left: "50%",
              top: "50%",
              maxWidth: "none",
              maxHeight: "none",
              objectFit: "cover",
              position: "absolute",
              transform: `translate(calc(-50% + ${imageOffsetX}px), calc(-50% + ${imageOffsetY}px))`,
              zIndex: 0,
            }}
          />
        ) : (
          label.toUpperCase()
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
        {avatarSurfaceRelief ? (
          <span
            aria-hidden="true"
            style={{
              position: "absolute",
              inset: 0,
              borderRadius: "inherit",
              boxShadow: avatarSurfaceRelief,
              pointerEvents: "none",
              zIndex: 2,
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
  if (node.type === "icon_token") {
    const token = stringValue(node.metadata?.token) ?? node.text ?? "";
    if (stringValue(node.style?.maskImage)) {
      return <span title={token} />;
    }
    return <span title={token}>{iconTokenLabel(token)}</span>;
  }
  if (node.type === "component_button_icon_glyph") {
    return (
      <svg
        viewBox="0 0 24 24"
        aria-hidden="true"
        style={{
          display: "block",
          width: "100%",
          height: "100%",
          overflow: "visible",
        }}
      >
        <path
          d="M12 5.2A6.8 6.8 0 1 0 12 18.8A6.8 6.8 0 1 0 12 5.2 M12 8.4V15.6 M8.4 12H15.6"
          fill="none"
          stroke="currentColor"
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth="1.7"
        />
      </svg>
    );
  }
  if (node.type === "message_bubble_tail") {
    return messageBubbleTailNode(node);
  }
  if (
    node.type === "message_bubble_status_icon" ||
    node.type === "message_bubble_audio_badge_icon" ||
    node.type === "message_bubble_video_status_icon"
  ) {
    const token = stringValue(node.metadata?.token) ?? node.text ?? "";
    if (stringValue(node.style?.maskImage)) {
      return <span title={token} />;
    }
    return <span title={token}>{node.text}</span>;
  }
  if (node.type === "keyboard_bottom_item") {
    return iconButtonContent(node);
  }
  if (node.type === "text_input_bar_item") {
    return iconButtonContent(node);
  }
  if (node.type === "chat_header_icon") {
    return iconButtonContent(node);
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
  if (node.type === "keyboard_key") {
    const isEmojiKey = node.style?.isEmojiKey === true;
    const content =
      node.style?.isEmojiModeSwitchKey === true
        ? keyboardEmojiModeGlyph(node)
        : node.text;
    return (
      <span
        style={{
          display: "inline-flex",
          alignItems: "center",
          justifyContent: "center",
          width: "100%",
          height: "100%",
          fontSize: numberValue(node.style?.fontSize),
          lineHeight: isEmojiKey ? 1 : undefined,
          textAlign: "center",
        }}
      >
        {content}
      </span>
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
  showBounds = false,
}: {
  node: RenderableNode;
  parentOrigin: { x: number; y: number };
  showBounds?: boolean;
}) {
  const currentOrigin = node.box
    ? { x: node.box.x, y: node.box.y }
    : parentOrigin;
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
              width: node.box
                ? node.box.width
                : stringValue(node.style?.maskImage)
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
                      padding: numberValue(node.style?.keyPadding),
                      boxSizing: "border-box",
                      boxShadow: joinedBoxShadow(
                        node.style?.shadow === undefined
                          ? "0 0.01em 0 rgba(255, 255, 255, 0.32) inset"
                          : shadowValue(node.style.shadow),
                        surfaceReliefValue(node.style?.surfaceRelief),
                      ),
                      whiteSpace: "nowrap",
                      overflow: "visible",
                      transform: numberValue(node.style?.pressedTransformScale)
                        ? `scale(${numberValue(node.style?.pressedTransformScale)})`
                        : undefined,
                      transformOrigin: "center center",
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
                            flexDirection:
                              stringValue(
                                asRecord(node.style?.buttonIcon).labelPosition,
                              ) === "top" ||
                              stringValue(
                                asRecord(node.style?.buttonIcon).labelPosition,
                              ) === "bottom"
                                ? "column"
                                : "row",
                            alignItems: "center",
                            justifyContent: "center",
                            minWidth: stringValue(node.style?.maskImage)
                              ? buttonIconOuterSize(node.style)
                              : 0,
                            width: stringValue(node.style?.maskImage)
                              ? buttonIconOuterSize(node.style)
                              : undefined,
                            height: buttonIconOuterSize(node.style),
                            fontSize: numberValue(node.style?.fontSize),
                            lineHeight: numberValue(node.style?.lineHeight)
                              ? `${numberValue(node.style?.lineHeight)}px`
                              : undefined,
                            overflow: "visible",
                          }
                        : node.type === "chat_header_icon"
                          ? {
                              display: "inline-flex",
                              flexDirection:
                                stringValue(
                                  asRecord(node.style?.buttonIcon).labelPosition,
                                ) === "top" ||
                                stringValue(
                                  asRecord(node.style?.buttonIcon).labelPosition,
                                ) === "bottom"
                                  ? "column"
                                  : "row",
                              alignItems: "center",
                              justifyContent: "center",
                              width: buttonIconOuterSize(node.style),
                              height: buttonIconOuterSize(node.style),
                              fontSize: numberValue(node.style?.fontSize),
                              lineHeight: numberValue(node.style?.lineHeight)
                                ? `${numberValue(node.style?.lineHeight)}px`
                                : undefined,
                              overflow: "visible",
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
                                flexDirection:
                                  stringValue(
                                    asRecord(node.style?.buttonIcon)
                                      .labelPosition,
                                  ) === "top" ||
                                  stringValue(
                                    asRecord(node.style?.buttonIcon)
                                      .labelPosition,
                                  ) === "bottom"
                                    ? "column"
                                    : "row",
                                alignItems: "center",
                                justifyContent: "center",
                                width: buttonIconOuterSize(node.style),
                                height: buttonIconOuterSize(node.style),
                                fontSize: numberValue(node.style?.fontSize),
                                lineHeight: numberValue(node.style?.lineHeight)
                                  ? `${numberValue(node.style?.lineHeight)}px`
                                  : undefined,
                                overflow: "visible",
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
        : node.type === "message_bubble_status_icon" ||
          node.type === "message_bubble_audio_badge_icon" ||
          node.type === "message_bubble_video_status_icon"
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
        : (node.type === "message_bubble_audio_play" ||
            node.type === "message_bubble_video_play_overlay")
          ? {
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              paddingLeft: numberValue(node.style?.paddingLeft),
              whiteSpace: "nowrap",
            }
        : node.type === "message_bubble_audio_duration"
          ? {
              display: "block",
              whiteSpace: "nowrap",
            }
        : node.type === "message_bubble_video_status"
          ? {
              display: "block",
              overflow: "visible",
              pointerEvents: "none",
            }
        : node.type === "message_bubble_video_status_duration"
          ? {
              display: "block",
              whiteSpace: "nowrap",
            }
        : node.type === "message_bubble_label" || node.type === "component_label"
          ? {
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              flexDirection: node.type === "component_label" ? "column" : undefined,
              paddingLeft: numberValue(node.style?.paddingX),
              paddingRight: numberValue(node.style?.paddingX),
              paddingTop: numberValue(node.style?.paddingY),
              paddingBottom: numberValue(node.style?.paddingY),
              whiteSpace: "nowrap",
              overflow: "visible",
            }
        : node.type === "component_label_text" ||
            node.type === "component_label_subtext"
          ? {
              display: "block",
              width: "100%",
              marginTop:
                node.type === "component_label_subtext"
                  ? numberValue(node.style?.marginTop)
                  : undefined,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textAlign:
                (stringValue(node.style?.textAlign) as CSSProperties["textAlign"]) ??
                "center",
            }
        : node.type === "component_preview_unsupported"
          ? {
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              paddingLeft: 12,
              paddingRight: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
            }
        : node.type === "component_button_icon_glyph"
          ? {
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              overflow: "visible",
            }
        : node.type === "icon_token"
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
              overflow: "visible",
            }
        : node.type === "text" && node.role === "actor_label_text"
          ? {
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              whiteSpace: "nowrap",
              overflow: "hidden",
            }
        : node.type === "text" && node.role === "message_text"
          ? {
              whiteSpace: "pre-wrap",
              overflow: "visible",
              display: "inline",
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
        ...(showBounds
          ? {
              outline: "1px solid rgba(255, 0, 255, 0.72)",
              outlineOffset: "-1px",
            }
          : {}),
      }}
    >
      {nodeContent(node)}
      {renderChildren?.map((child) => (
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

export const RenderableReactAdapter = ({
  tree,
  showBounds = false,
}: RenderableReactAdapterProps) => {
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
};
