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
  boundedCenterBox,
  boxEdgeIntrusionInsets,
  cssColorWithAlpha,
  placeChild,
  renderScale,
  numberToken,
  resolvePaletteColor,
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
  const labelBox = localLabelBox ? translateBox(localLabelBox, origin) : undefined;
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
    ],
  };
}

function bubbleContentLayout(
  textSize: { width: number; height: number },
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
  const textBox = {
    x: padding.left,
    y: padding.top,
    width: textSize.width,
    height: textSize.height,
  };
  if (!mediaSize) {
    return {
      width: textSize.width,
      height: textSize.height,
      textBox,
      mediaBox: undefined,
    };
  }

  const verticalGap = padding.gapY;
  const horizontalGap = padding.gapX;
  if (position === "top" || position === "bottom") {
    const width = Math.max(textSize.width, mediaSize.width);
    const height = textSize.height + verticalGap + mediaSize.height;
    const mediaBox = {
      x: padding.left + (width - mediaSize.width) / 2,
      y: position === "top" ? padding.top : padding.top + textSize.height + verticalGap,
      width: mediaSize.width,
      height: mediaSize.height,
    };
    return {
      width,
      height,
      textBox: {
        ...textBox,
        x: padding.left + (width - textSize.width) / 2,
        y: position === "top" ? padding.top + mediaSize.height + verticalGap : padding.top,
      },
      mediaBox,
    };
  }

  const width = textSize.width + horizontalGap + mediaSize.width;
  const height = Math.max(textSize.height, mediaSize.height);
  const mediaBox = {
    x: position === "left" ? padding.left : padding.left + textSize.width + horizontalGap,
    y: padding.top + (height - mediaSize.height) / 2,
    width: mediaSize.width,
    height: mediaSize.height,
  };
  return {
    width,
    height,
    textBox: {
      ...textBox,
      x: position === "left" ? padding.left + mediaSize.width + horizontalGap : padding.left,
      y: padding.top + (height - textSize.height) / 2,
    },
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
