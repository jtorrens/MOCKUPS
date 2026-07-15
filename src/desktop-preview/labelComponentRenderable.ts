import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import {
  centerBox,
  colorForMode,
  numberToken,
  renderScale,
  selectedColor,
  translateBox,
  unionBoxes,
  variants,
} from "./componentRenderableCommon.js";
import {
  measuredTextWidth,
  resolveTypographyStyle,
} from "./previewTextHelpers.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";
import type { SurfaceColorOverride } from "./surfaceComponentRenderable.js";

export function measureLabelComponent(
  label: LabelDesignContract,
  payload: DesignPreviewPayload,
) {
  const scale = renderScale(payload);
  const textTypography = scaleTypography(
    resolveTypographyStyle(payload, label.textTypography, scale),
    label.textSizeMultiplier,
  );
  const subtextTypography = scaleTypography(
    resolveTypographyStyle(payload, label.subtextTypography, scale),
    label.subtextSizeMultiplier,
  );
  return labelSize(label, textTypography, subtextTypography, scale, payload);
}

function labelSize(
  label: LabelDesignContract,
  textTypography: ReturnType<typeof resolveTypographyStyle>,
  subtextTypography: ReturnType<typeof resolveTypographyStyle>,
  scale: number,
  payload: DesignPreviewPayload,
) {
  const paddingX = numberToken(payload, label.padding.xToken) * scale;
  const paddingY = numberToken(payload, label.padding.yToken) * scale;
  const content = labelContentLayout(
    label,
    textTypography,
    subtextTypography,
    scale,
    payload,
  );
  if (label.dimensionMode === "fixed") {
    return {
      width: label.size.width * scale,
      height: label.size.height * scale,
      lineHeight: textTypography.lineHeight,
      subtextLineHeight: subtextTypography.lineHeight,
      hasSubtext: content.hasSubtext,
    };
  }

  return {
    width: Math.max(1, content.bounds.width + paddingX * 2),
    height: Math.max(1, content.bounds.height + paddingY * 2),
    lineHeight: textTypography.lineHeight,
    subtextLineHeight: subtextTypography.lineHeight,
    hasSubtext: content.hasSubtext,
  };
}

function labelContentLayout(
  label: LabelDesignContract,
  textTypography: ReturnType<typeof resolveTypographyStyle>,
  subtextTypography: ReturnType<typeof resolveTypographyStyle>,
  scale: number,
  payload: DesignPreviewPayload,
  textWidth?: number,
) {
  const measuredPrimaryWidth = Math.max(1, measuredTextWidth(label.text, textTypography));
  const textBox = {
    x: 0,
    y: 0,
    width: Math.max(1, textWidth ?? measuredPrimaryWidth),
    height: textTypography.lineHeight,
  };
  const hasSubtext = label.subtext.trim().length > 0;
  if (!hasSubtext && !label.reserveSubtextSpace) {
    return { textBox, subtextBox: undefined, bounds: textBox, hasSubtext };
  }

  const gap = numberToken(payload, label.textGapToken) * scale;
  const subtextWidth = Math.max(1, hasSubtext ? measuredTextWidth(label.subtext, subtextTypography) : 1);
  const subtextBox = subtextBoxRelativeToText(
    textBox,
    measuredPrimaryWidth,
    label.textAlign,
    { width: subtextWidth, height: subtextTypography.lineHeight },
    label.subtextHorizontalAlign,
    label.subtextVerticalPosition,
    gap,
  );
  return {
    textBox,
    subtextBox,
    bounds: unionBoxes([textBox, subtextBox]),
    hasSubtext,
  };
}

export function subtextBoxRelativeToText(
  textBox: RenderableBox,
  primaryTextWidth: number,
  textAlign: "left" | "center" | "right",
  subtextSize: { width: number; height: number },
  horizontalAlign: "left" | "center" | "right",
  verticalPosition: "top" | "bottom",
  gap: number,
): RenderableBox {
  const primaryTextX = textBox.x + alignedOffset(textBox.width, primaryTextWidth, textAlign);
  return {
    x: primaryTextX + alignedOffset(primaryTextWidth, subtextSize.width, horizontalAlign),
    y: verticalPosition === "top"
      ? textBox.y - gap - subtextSize.height
      : textBox.y + textBox.height + gap,
    width: subtextSize.width,
    height: subtextSize.height,
  };
}

function alignedOffset(
  referenceWidth: number,
  childWidth: number,
  alignment: "left" | "center" | "right",
) {
  if (alignment === "left") return 0;
  if (alignment === "right") return referenceWidth - childWidth;
  return (referenceWidth - childWidth) / 2;
}

export function labelComponentToRenderable(
  payload: DesignPreviewPayload,
  label: LabelDesignContract,
): RenderableNode {
  const size = measureLabelComponent(label, payload);
  return labelComponentToRenderableAt(
    payload,
    label,
    centerBox(payload, size.width, size.height),
  );
}

export function labelComponentToRenderableAt(
  payload: DesignPreviewPayload,
  label: LabelDesignContract,
  box: RenderableBox,
  options: {
    surfaceColors?: SurfaceColorOverride;
    textColor?: string;
    subtextColor?: string;
  } = {},
): RenderableNode {
  const scale = renderScale(payload);
  const textTypography = scaleTypography(
    resolveTypographyStyle(payload, label.textTypography, scale),
    label.textSizeMultiplier,
  );
  const subtextTypography = scaleTypography(
    resolveTypographyStyle(payload, label.subtextTypography, scale),
    label.subtextSizeMultiplier,
  );
  const paddingX = numberToken(payload, label.padding.xToken) * scale;
  const size = labelSize(label, textTypography, subtextTypography, scale, payload);
  const content = labelContentLayout(
    label,
    textTypography,
    subtextTypography,
    scale,
    payload,
    label.dimensionMode === "fixed"
      ? Math.max(1, box.width - paddingX * 2)
      : undefined,
  );
  const contentOrigin = {
    x: box.x + (box.width - content.bounds.width) / 2 - content.bounds.x,
    y: box.y + (box.height - content.bounds.height) / 2 - content.bounds.y,
  };
  const textBox = translateBox(content.textBox, contentOrigin);
  const subtextBox = content.subtextBox
    ? translateBox(content.subtextBox, contentOrigin)
    : undefined;
  const surfaceNode = options.surfaceColors
    ? surfaceComponentToRenderableAt(payload, label.surface, box, options.surfaceColors)
    : surfaceComponentToRenderableAt(payload, label.surface, box);
  const textColor = options.textColor ?? selectedColor(payload, label.textColorToken);
  const subtextColor = options.subtextColor ?? selectedColor(payload, label.subtextColorToken);

  return {
    id: label.id,
    type: "group",
    frame: 0,
    box,
    style: {
      overflow: "visible",
    },
    children: [
      surfaceNode,
      {
        id: `${label.id}.content`,
        type: "group",
        frame: 0,
        box,
        style: {
          overflow: "visible",
          whiteSpace: "nowrap",
          colorModes: Object.fromEntries(
            variants(payload).map((mode) => [
              mode,
              {
                textColor: options.textColor ?? colorForMode(payload, label.textColorToken, mode),
                subtextColor: options.subtextColor
                  ?? colorForMode(payload, label.subtextColorToken, mode),
              },
            ]),
          ),
        },
        children: [
          {
            id: `${label.id}.text`,
            type: "text",
            frame: 0,
            box: textBox,
            text: label.text,
            style: {
              textColor,
              fontSize: textTypography.fontSize,
              fontFamily: textTypography.fontFamily,
              lineHeight: size.lineHeight,
              textAlign: label.textAlign,
              display: "block",
              overflow: "hidden",
              fontStyle: textTypography.fontStyle,
              fontWeight: textTypography.fontWeight,
              whiteSpace: "nowrap",
            },
          },
          ...(size.hasSubtext && subtextBox
            ? [
                {
                  id: `${label.id}.subtext`,
                  type: "text",
                  frame: 0,
                  box: subtextBox,
                  text: label.subtext,
                  style: {
                    textColor: subtextColor,
                    fontSize: subtextTypography.fontSize,
                    fontFamily: subtextTypography.fontFamily,
                    lineHeight: size.subtextLineHeight,
                    textAlign: label.textAlign,
                    display: "block",
                    overflow: "hidden",
                    fontStyle: subtextTypography.fontStyle,
                    fontWeight: subtextTypography.fontWeight,
                    whiteSpace: "nowrap",
                  },
                } satisfies RenderableNode,
              ]
            : []),
        ],
      },
    ],
  };
}

function scaleTypography(
  typography: ReturnType<typeof resolveTypographyStyle>,
  multiplier: number,
): ReturnType<typeof resolveTypographyStyle> {
  return {
    ...typography,
    fontSize: typography.fontSize * multiplier,
    lineHeight: typography.lineHeight * multiplier,
    measureTextWidth: (text) => typography.measureTextWidth(text) * multiplier,
  };
}
