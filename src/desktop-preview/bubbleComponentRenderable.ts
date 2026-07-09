import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type {
  BubbleDesignContract,
  BubblePalettePairContract,
} from "./bubbleComponentContract.js";
import {
  audioComponentToRenderableAt,
  measureAudioComponent,
} from "./audioComponentRenderable.js";
import {
  avatarComponentToRenderableAt,
} from "./avatarComponentRenderable.js";
import {
  boundedCenterBox,
  boxEdgeIntrusionInsets,
  cssColorWithAlpha,
  iconTokenStyle,
  placeChild,
  renderScale,
  numberToken,
  resolvePaletteColor,
  selectedColor,
  scalePlacement,
  selectedPaletteColor,
  translateBox,
  variants,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  labelComponentToRenderableAt,
  measureLabelComponent,
} from "./labelComponentRenderable.js";
import {
  measureMediaComponent,
  mediaComponentToRenderableAt,
} from "./mediaComponentRenderable.js";
import {
  surfaceComponentToRenderableAtWithColors,
  type SurfaceColorOverride,
} from "./surfaceComponentRenderable.js";
import {
  measureTextBoxComponent,
  textBoxComponentToRenderableAt,
} from "./textBoxComponentRenderable.js";
import {
  approximateTextWidth,
  resolveTypographyStyle,
} from "./previewTextHelpers.js";

export function bubbleComponentToRenderable(
  payload: DesignPreviewPayload,
  bubble: BubbleDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const paddingX = Math.max(0, numberToken(payload, bubble.padding.xToken) * scale);
  const paddingY = Math.max(0, numberToken(payload, bubble.padding.yToken) * scale);
  const textBoxForContent = {
    ...bubble.textBox,
    dimensionMode: "content" as const,
    overflowMode: "clip" as const,
    size: {
      width: Math.max(1, bubble.maxWidth - (paddingX * 2) / scale),
      height: 1,
    },
  };
  const measuredTextBox = measureTextBoxComponent(payload, textBoxForContent);
  const statusSize = measureBubbleStatus(payload, bubble);
  const media = activeBubbleMedia(bubble);
  const mediaSize = media
    ? media.kind === "audio"
      ? measureAudioComponent(payload, media.value)
      : measureMediaComponent(payload, media.value)
    : undefined;
  const basePadding = {
    left: paddingX,
    top: paddingY,
    right: paddingX,
    bottom: paddingY,
    gapX: paddingX,
    gapY: paddingY,
  };
  const baseContentLayout = bubbleContentLayout(
    { width: measuredTextBox.width, height: measuredTextBox.height },
    statusSize,
    mediaSize,
    bubble.mediaSlot.position,
    basePadding,
  );
  const baseSurfaceBox = {
    x: 0,
    y: 0,
    width: baseContentLayout.width + basePadding.left + basePadding.right,
    height: baseContentLayout.height + basePadding.top + basePadding.bottom,
  };
  const baseLabelBox = bubble.actorLabelSlot.label
    ? placeChild(
        baseSurfaceBox,
        measureLabelComponent(bubble.actorLabelSlot.label, payload),
        scalePlacement(bubble.actorLabelSlot.placement, scale),
      )
    : undefined;
  const labelIntrusion = boxEdgeIntrusionInsets(baseSurfaceBox, baseLabelBox);
  const contentPadding = {
    left: paddingX,
    top: paddingY + labelIntrusion.top,
    right: paddingX,
    bottom: paddingY + labelIntrusion.bottom,
    gapX: paddingX,
    gapY: paddingY,
  };
  const contentLayout = bubbleContentLayout(
    { width: measuredTextBox.width, height: measuredTextBox.height },
    statusSize,
    mediaSize,
    bubble.mediaSlot.position,
    contentPadding,
  );
  const localSurfaceBox = {
    x: 0,
    y: 0,
    width: contentLayout.width + contentPadding.left + contentPadding.right,
    height: contentLayout.height + contentPadding.top + contentPadding.bottom,
  };
  const localLabelBox = bubble.actorLabelSlot.label
    ? placeChild(
        localSurfaceBox,
        measureLabelComponent(bubble.actorLabelSlot.label, payload),
        scalePlacement(bubble.actorLabelSlot.placement, scale),
    )
    : undefined;
  const localAvatarBox = bubble.avatarSlot.avatar
    ? placeChild(
        localSurfaceBox,
        {
          width: bubble.avatarSlot.avatar.size * scale,
          height: bubble.avatarSlot.avatar.size * scale,
        },
        scalePlacement(bubble.avatarSlot.placement, scale),
      )
    : undefined;
  const groupBox = boundedCenterBox(payload, localSurfaceBox.width, localSurfaceBox.height);
  const origin = {
    x: groupBox.x - localSurfaceBox.x,
    y: groupBox.y - localSurfaceBox.y,
  };
  const surfaceBox = translateBox(localSurfaceBox, origin);
  const textBox = translateBox(contentLayout.textBox, origin);
  const mediaBox = contentLayout.mediaBox
    ? translateBox(contentLayout.mediaBox, origin)
    : undefined;
  const statusBox = contentLayout.statusBox
    ? translateBox(contentLayout.statusBox, origin)
    : undefined;
  const labelBox = localLabelBox ? translateBox(localLabelBox, origin) : undefined;
  const avatarBox = localAvatarBox ? translateBox(localAvatarBox, origin) : undefined;
  const stateColors = bubble.colors[bubble.state];
  const surfaceColors = bubbleSurfaceColors(payload, stateColors.background, bubble.surface.backgroundAlpha);
  const textColor = selectedPalettePairColor(payload, stateColors.text);

  return {
    id: bubble.id,
    type: "group",
    frame: 0,
    box: groupBox,
    style: {
      overflow: "visible",
    },
    children: [
      surfaceComponentToRenderableAtWithColors(
        payload,
        bubble.surface,
        surfaceBox,
        surfaceColors,
      ),
      textBoxComponentToRenderableAt(
        payload,
        textBoxForContent,
        textBox,
        {
          surfaceVisible: false,
          textColors: {
            textColor,
            placeholderColor: textColor,
          },
        },
      ),
      ...(media && mediaBox
        ? [
            media.kind === "audio"
              ? audioComponentToRenderableAt(payload, media.value, mediaBox)
              : mediaComponentToRenderableAt(payload, media.value, mediaBox),
          ]
        : []),
      ...(statusBox
        ? [bubbleStatusToRenderable(payload, bubble, statusBox, textColor)]
        : []),
      ...(bubble.actorLabelSlot.label && labelBox
        ? [
            labelComponentToRenderableAt(
              payload,
              bubble.actorLabelSlot.label,
              labelBox,
              {
                surfaceColors: bubbleSurfaceColors(
                  payload,
                  stateColors.background,
                  bubble.actorLabelSlot.label.surface.backgroundAlpha,
                ),
                textColor: bubble.actorLabelSlot.textColorOverride,
              },
            ),
          ]
        : []),
      ...(bubble.avatarSlot.avatar && avatarBox
        ? [avatarComponentToRenderableAt(payload, bubble.avatarSlot.avatar, avatarBox)]
        : []),
    ],
  };
}

function bubbleContentLayout(
  textSize: { width: number; height: number },
  statusSize: { width: number; height: number } | undefined,
  mediaSize: { width: number; height: number } | undefined,
  position: BubbleDesignContract["mediaSlot"]["position"],
  padding: {
    left: number;
    top: number;
    right: number;
    bottom: number;
    gapX: number;
    gapY: number;
  },
) {
  const statusGap = statusSize
    ? Math.max(2, Math.min(padding.gapY, statusSize.height * 0.45))
    : 0;
  const statusBlockHeight = statusSize ? statusGap + statusSize.height : 0;
  const textAndStatusBoxes = (
    textX: number,
    textY: number,
    contentWidth: number,
    statusY: number,
  ) => ({
    textBox: {
      x: textX,
      y: textY,
      width: textSize.width,
      height: textSize.height,
    },
    statusBox: statusSize
      ? {
          x: padding.left + contentWidth - statusSize.width,
          y: statusY,
          width: statusSize.width,
          height: statusSize.height,
        }
      : undefined,
  });
  if (!mediaSize) {
    const width = Math.max(textSize.width, statusSize?.width ?? 0);
    const height = textSize.height + statusBlockHeight;
    const boxes = textAndStatusBoxes(
      padding.left,
      padding.top,
      width,
      padding.top + height - (statusSize?.height ?? 0),
    );
    return {
      width,
      height,
      ...boxes,
      mediaBox: undefined,
    };
  }

  const verticalGap = padding.gapY;
  const horizontalGap = padding.gapX;
  if (position === "top" || position === "bottom") {
    const mediaGap = verticalGap;
    const width = Math.max(textSize.width, statusSize?.width ?? 0, mediaSize.width);
    const height = textSize.height + mediaGap + mediaSize.height + statusBlockHeight;
    const textX = mediaSize.width > textSize.width
      ? padding.left
      : padding.left + (width - textSize.width) / 2;
    const textY = position === "top"
      ? padding.top + mediaSize.height + verticalGap
      : padding.top;
    const mediaBox = {
      x: padding.left + (width - mediaSize.width) / 2,
      y: position === "top"
        ? padding.top
        : padding.top + textSize.height + mediaGap,
      width: mediaSize.width,
      height: mediaSize.height,
    };
    const statusY = padding.top + height - (statusSize?.height ?? 0);
    const boxes = textAndStatusBoxes(textX, textY, width, statusY);
    return {
      width,
      height,
      ...boxes,
      mediaBox,
    };
  }

  const rowWidth = textSize.width + horizontalGap + mediaSize.width;
  const width = Math.max(rowWidth, statusSize?.width ?? 0);
  const rowHeight = Math.max(textSize.height, mediaSize.height);
  const height = rowHeight + statusBlockHeight;
  const textX = position === "left"
    ? padding.left + mediaSize.width + horizontalGap
    : padding.left;
  const textY = padding.top + (rowHeight - textSize.height) / 2;
  const mediaBox = {
    x: position === "left" ? padding.left : padding.left + textSize.width + horizontalGap,
    y: padding.top + (rowHeight - mediaSize.height) / 2,
    width: mediaSize.width,
    height: mediaSize.height,
  };
  const statusY = padding.top + height - (statusSize?.height ?? 0);
  const boxes = textAndStatusBoxes(textX, textY, width, statusY);
  return {
    width,
    height,
    ...boxes,
    mediaBox,
  };
}

function activeBubbleMedia(bubble: BubbleDesignContract):
  | { kind: "media"; value: NonNullable<BubbleDesignContract["mediaSlot"]["media"]> }
  | { kind: "audio"; value: NonNullable<BubbleDesignContract["mediaSlot"]["audio"]> }
  | undefined {
  if (bubble.textBox.cursorVisible) {
    return undefined;
  }
  if (bubble.mediaSlot.mediaType === "audio" && bubble.mediaSlot.audio) {
    return { kind: "audio", value: bubble.mediaSlot.audio };
  }
  if (bubble.mediaSlot.media) {
    return { kind: "media", value: bubble.mediaSlot.media };
  }
  return undefined;
}

function activeBubbleStatusIcon(bubble: BubbleDesignContract) {
  if (bubble.status.state === "none") return undefined;
  const icon = bubble.status.icons[bubble.status.state];
  return icon.iconToken.trim().length > 0 && icon.iconToken !== "none"
    ? icon
    : undefined;
}

function measureBubbleStatus(
  payload: DesignPreviewPayload,
  bubble: BubbleDesignContract,
) {
  const text = bubble.status.text.trim();
  const icon = activeBubbleStatusIcon(bubble);
  if (!text && !icon) return undefined;

  const scale = renderScale(payload);
  const size = Math.max(1, numberToken(payload, bubble.status.sizeToken) * scale);
  const lineHeight = Math.max(size, size * 1.15);
  const iconSize = icon ? size : 0;
  const gap = text && icon ? Math.max(2, size * 0.28) : 0;
  return {
    width: (text ? approximateTextWidth(text, size) : 0) + gap + iconSize,
    height: Math.max(lineHeight, iconSize),
  };
}

function bubbleStatusToRenderable(
  payload: DesignPreviewPayload,
  bubble: BubbleDesignContract,
  box: RenderableBox,
  textColor: string,
): RenderableNode {
  const text = bubble.status.text.trim();
  const icon = activeBubbleStatusIcon(bubble);
  const scale = renderScale(payload);
  const typography = resolveTypographyStyle(payload, bubble.textBox.typography, scale);
  const fontSize = Math.max(1, numberToken(payload, bubble.status.sizeToken) * scale);
  const lineHeight = Math.max(fontSize, fontSize * 1.15);
  const iconSize = icon ? Math.min(box.height, fontSize) : 0;
  const gap = text && icon ? Math.max(2, fontSize * 0.28) : 0;
  const textWidth = text ? Math.max(1, box.width - iconSize - gap) : 0;

  return {
    id: `${bubble.id}.status`,
    type: "group",
    frame: 0,
    box,
    style: {
      alignItems: "center",
      display: "flex",
      flexDirection: "row",
      justifyContent: "flex-end",
      overflow: "visible",
    },
    children: [
      ...(text
        ? [
            {
              id: `${bubble.id}.status.text`,
              type: "text",
              frame: 0,
              box: {
                x: box.x,
                y: box.y,
                width: textWidth,
                height: box.height,
              },
              text,
              style: {
                display: "block",
                fontFamily: typography.fontFamily,
                fontSize,
                fontStyle: typography.fontStyle,
                fontWeight: typography.fontWeight,
                lineHeight,
                overflow: "visible",
                textAlign: "right",
                textColor,
                whiteSpace: "nowrap",
              },
            } satisfies RenderableNode,
          ]
        : []),
      ...(icon
        ? [
            {
              id: `${bubble.id}.status.icon`,
              type: "icon",
              frame: 0,
              box: {
                x: box.x + box.width - iconSize,
                y: box.y + (box.height - iconSize) * 0.5,
                width: iconSize,
                height: iconSize,
              },
              text: icon.iconToken,
              style: {
                ...iconTokenStyle(payload, icon.iconToken, selectedColor(payload, icon.colorToken)),
              },
            } satisfies RenderableNode,
          ]
        : []),
    ],
  };
}

function bubbleSurfaceColors(
  payload: DesignPreviewPayload,
  pair: BubblePalettePairContract,
  alpha: number,
): SurfaceColorOverride {
  return {
    background: selectedPalettePairColor(payload, pair, alpha),
    colorModes: Object.fromEntries(
      variants(payload).map((mode) => [
        mode,
        {
          background: palettePairColorForMode(payload, pair, mode, alpha),
        },
      ]),
    ),
  };
}

function selectedPalettePairColor(
  payload: DesignPreviewPayload,
  pair: BubblePalettePairContract,
  alpha = 1,
) {
  return selectedPaletteColor(
    payload,
    paletteTokenForMode(pair, payload.themeMode || "light"),
    alpha,
  );
}

function palettePairColorForMode(
  payload: DesignPreviewPayload,
  pair: BubblePalettePairContract,
  mode: string,
  alpha = 1,
) {
  return cssColorWithAlpha(resolvePaletteColor(payload, paletteTokenForMode(pair, mode)), alpha);
}

function paletteTokenForMode(pair: BubblePalettePairContract, mode: string) {
  return mode.toLowerCase().includes("dark") ? pair.dark : pair.light;
}
