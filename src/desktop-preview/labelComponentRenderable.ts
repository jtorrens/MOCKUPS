import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import {
  centerBox,
  colorForMode,
  numberToken,
  renderScale,
  selectedColor,
  variants,
} from "./componentRenderableCommon.js";
import {
  approximateTextWidth,
  resolveTypographyStyle,
} from "./previewTextHelpers.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";

export function measureLabelComponent(
  label: LabelDesignContract,
  payload: DesignPreviewPayload,
) {
  const scale = renderScale(payload);
  const textTypography = resolveTypographyStyle(payload, label.textTypography, scale);
  const subtextTypography = resolveTypographyStyle(payload, label.subtextTypography, scale);
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
  const hasSubtext = label.subtext.trim().length > 0;
  const textGap = hasSubtext ? label.textGap * scale : 0;
  const contentWidth = Math.max(
    approximateTextWidth(label.text, textTypography.fontSize),
    hasSubtext ? approximateTextWidth(label.subtext, subtextTypography.fontSize) : 0,
  );
  const contentHeight =
    textTypography.lineHeight + (hasSubtext ? textGap + subtextTypography.lineHeight : 0);
  if (label.dimensionMode === "fixed") {
    return {
      width: label.size.width * scale,
      height: label.size.height * scale,
      lineHeight: textTypography.lineHeight,
      subtextLineHeight: subtextTypography.lineHeight,
      hasSubtext,
    };
  }

  return {
    width: Math.max(1, contentWidth + paddingX * 2),
    height: Math.max(1, contentHeight + paddingY * 2),
    lineHeight: textTypography.lineHeight,
    subtextLineHeight: subtextTypography.lineHeight,
    hasSubtext,
  };
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
): RenderableNode {
  const scale = renderScale(payload);
  const textTypography = resolveTypographyStyle(payload, label.textTypography, scale);
  const subtextTypography = resolveTypographyStyle(payload, label.subtextTypography, scale);
  const paddingX = numberToken(payload, label.padding.xToken) * scale;
  const paddingY = numberToken(payload, label.padding.yToken) * scale;
  const size = labelSize(label, textTypography, subtextTypography, scale, payload);
  const surfaceNode = surfaceComponentToRenderableAt(payload, label.surface, box);
  const surfaceColorModes = surfaceNode.style?.colorModes as
    | Record<string, Record<string, unknown>>
    | undefined;

  return {
    ...surfaceNode,
    id: label.id,
    style: {
      ...surfaceNode.style,
      paddingX,
      paddingY,
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      flexDirection: "column",
      whiteSpace: "nowrap",
      overflow: "visible",
      colorModes: Object.fromEntries(
        variants(payload).map((mode) => [
          mode,
          {
            ...(surfaceColorModes?.[mode] ?? {}),
            textColor: colorForMode(payload, label.textColorToken, mode),
            subtextColor: colorForMode(payload, label.subtextColorToken, mode),
          },
        ]),
      ),
    },
    children: [
      {
        id: `${label.id}.text`,
        type: "text",
        frame: 0,
          text: label.text,
          style: {
            textColor: selectedColor(payload, label.textColorToken),
          fontSize: textTypography.fontSize,
          lineHeight: size.lineHeight,
          textAlign: label.textAlign,
          display: "block",
          width: "100%",
          overflow: "hidden",
          fontStyle: textTypography.fontStyle,
          fontWeight: textTypography.fontWeight,
          whiteSpace: "nowrap",
        },
      },
      ...(size.hasSubtext
        ? [
            {
              id: `${label.id}.subtext`,
              type: "text",
              frame: 0,
              text: label.subtext,
              style: {
                textColor: selectedColor(payload, label.subtextColorToken),
                fontSize: subtextTypography.fontSize,
                lineHeight: size.subtextLineHeight,
                marginTop: label.textGap * scale,
                textAlign: label.textAlign,
                display: "block",
                width: "100%",
                overflow: "hidden",
                fontStyle: subtextTypography.fontStyle,
                fontWeight: subtextTypography.fontWeight,
                whiteSpace: "nowrap",
              },
            } satisfies RenderableNode,
          ]
        : []),
    ],
  };
}
