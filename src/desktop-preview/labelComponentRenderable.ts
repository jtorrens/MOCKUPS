import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import {
  centerBox,
  colorForMode,
  numberToken,
  renderScale,
  selectedColor,
  shadow,
  variants,
} from "./componentRenderableCommon.js";

function labelTextWidth(text: string, fontSize: number) {
  return text.length * fontSize * 0.58;
}

export function measureLabelComponent(
  label: LabelDesignContract,
  payload: DesignPreviewPayload,
) {
  const scale = renderScale(payload);
  const fontSize = numberToken(payload, label.textSizeToken) * scale;
  const subtextFontSize = numberToken(payload, label.subtextSizeToken) * scale;
  return labelSize(label, fontSize, subtextFontSize, scale);
}

function labelSize(
  label: LabelDesignContract,
  fontSize: number,
  subtextFontSize: number,
  scale: number,
) {
  const paddingX = label.padding.x * scale;
  const paddingY = label.padding.y * scale;
  const hasSubtext = label.subtext.trim().length > 0;
  const lineHeight = Math.max(fontSize * 1.2, fontSize);
  const subtextLineHeight = Math.max(subtextFontSize * 1.2, subtextFontSize);
  const textGap = hasSubtext ? label.textGap * scale : 0;
  const contentWidth = Math.max(
    labelTextWidth(label.text, fontSize),
    hasSubtext ? labelTextWidth(label.subtext, subtextFontSize) : 0,
  );
  const contentHeight =
    lineHeight + (hasSubtext ? textGap + subtextLineHeight : 0);
  if (label.dimensionMode === "fixed") {
    return {
      width: label.size.width * scale,
      height: label.size.height * scale,
      lineHeight,
      subtextLineHeight,
      hasSubtext,
    };
  }

  return {
    width: Math.max(1, contentWidth + paddingX * 2),
    height: Math.max(1, contentHeight + paddingY * 2),
    lineHeight,
    subtextLineHeight,
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
  const fontSize = numberToken(payload, label.textSizeToken) * scale;
  const subtextFontSize = numberToken(payload, label.subtextSizeToken) * scale;
  const size = labelSize(label, fontSize, subtextFontSize, scale);
  const borderWidth = label.surface.borderWidth * scale;
  const background = selectedColor(
    payload,
    label.backgroundColorToken,
    label.surfaceAlpha,
  );
  const borderColor = selectedColor(payload, label.surface.borderColorToken);
  const cornerRadius = numberToken(payload, label.surface.cornerRadiusToken) * scale;
  const surfaceRelief = label.surface.reliefEnabled
    ? {
        angleDeg: label.surface.reliefAngle,
        extension: label.surface.reliefExtent * scale,
        spread: label.surface.reliefSpread * scale,
        upperIntensity: label.surface.reliefTopIntensity * label.surfaceAlpha,
        lowerIntensity: label.surface.reliefBottomIntensity * label.surfaceAlpha,
      }
    : undefined;

  return {
    id: label.id,
    type: "surface",
    role: "label",
    frame: 0,
    box,
    style: {
      background,
      borderWidth,
      borderColor,
      borderRadius: cornerRadius,
      shadow: label.surface.shadowEnabled ? shadow(payload) : undefined,
      surfaceRelief,
      paddingX: label.padding.x * scale,
      paddingY: label.padding.y * scale,
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
            background: colorForMode(
              payload,
              label.backgroundColorToken,
              mode,
              label.surfaceAlpha,
            ),
            textColor: colorForMode(payload, label.textColorToken, mode),
            subtextColor: colorForMode(payload, label.subtextColorToken, mode),
            borderColor: colorForMode(
              payload,
              label.surface.borderColorToken,
              mode,
            ),
          },
        ]),
      ),
    },
    children: [
      {
        id: `${label.id}.text`,
        type: "text",
        role: "label_primary",
        frame: 0,
        text: label.text,
        style: {
          textColor: selectedColor(payload, label.textColorToken),
          fontSize,
          lineHeight: size.lineHeight,
          textAlign: label.textAlign,
          display: "block",
          width: "100%",
          overflow: "hidden",
          fontStyle: label.textStyle === "italic" ? "italic" : undefined,
          whiteSpace: "nowrap",
        },
      },
      ...(size.hasSubtext
        ? [
            {
              id: `${label.id}.subtext`,
              type: "text",
              role: "label_secondary",
              frame: 0,
              text: label.subtext,
              style: {
                textColor: selectedColor(payload, label.subtextColorToken),
                fontSize: subtextFontSize,
                lineHeight: size.subtextLineHeight,
                marginTop: label.textGap * scale,
                textAlign: label.textAlign,
                display: "block",
                width: "100%",
                overflow: "hidden",
                fontStyle:
                  label.subtextStyle === "italic" ? "italic" : undefined,
                whiteSpace: "nowrap",
              },
            },
          ]
        : []),
    ],
    metadata: {
      route: "component-resolver.label-renderable",
      componentType: "label",
    },
  };
}
