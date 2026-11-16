import { wrapVisualAccent } from "./visualAccent.js";
import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { ButtonDesignContract } from "./buttonComponentContract.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  boundedCenterBox,
  iconTokenStyle,
  numberToken,
  renderScale,
  selectedColor,
} from "./componentRenderableCommon.js";
import { labelComponentToRenderableAt, measureLabelComponent } from "./labelComponentRenderable.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";
import { badgeComponentToRenderableAt } from "./badgeComponentRenderable.js";

export function buttonComponentToRenderable(payload: DesignPreviewPayload, button: ButtonDesignContract): RenderableNode {
  const size = measureButtonComponent(payload, button);
  return buttonComponentToRenderableAt(
    payload,
    button,
    boundedCenterBox(payload, size.width, size.height),
  );
}

// Both intrinsic measurement and painting consume the same content geometry.
function buttonContentLayout(payload: DesignPreviewPayload, button: ButtonDesignContract) {
  const scale = renderScale(payload);
  const iconSize = button.contentMode === "text" || button.iconToken === null
    ? 0 : numberToken(payload, button.iconSizeToken) * scale;
  const labelSize = button.appearance.label ? measureLabelComponent(button.appearance.label, payload) : undefined;
  const gap = iconSize > 0 && labelSize ? numberToken(payload, button.contentGapToken) * scale : 0;
  const labelWidth = labelSize?.width ?? 0;
  const labelHeight = labelSize?.height ?? 0;
  const vertical = button.labelPosition === "top" || button.labelPosition === "bottom";
  const labelFirst = button.labelPosition === "top" || button.labelPosition === "left";
  const width = vertical ? Math.max(iconSize, labelWidth) : iconSize + gap + labelWidth;
  const height = vertical ? iconSize + gap + labelHeight : Math.max(iconSize, labelHeight);
  return {
    width, height, iconSize, labelSize,
    iconX: vertical ? (width - iconSize) / 2 : labelFirst ? labelWidth + gap : 0,
    iconY: vertical ? labelFirst ? labelHeight + gap : 0 : (height - iconSize) / 2,
    labelX: vertical ? (width - labelWidth) / 2 : labelFirst ? 0 : iconSize + gap,
    labelY: vertical ? labelFirst ? 0 : iconSize + gap : (height - labelHeight) / 2,
  };
}

export function measureButtonComponent(payload: DesignPreviewPayload, button: ButtonDesignContract) {
  const scale = renderScale(payload);
  const content = buttonContentLayout(payload, button);
  const paddingX = numberToken(payload, button.padding.xToken) * scale;
  const paddingY = numberToken(payload, button.padding.yToken) * scale;
  const width = button.dimensionMode === "fixed" ? button.size.width * scale : content.width + paddingX * 2;
  const height = button.dimensionMode === "fixed" ? button.size.height * scale : content.height + paddingY * 2;
  return { width: Math.max(1, width), height: Math.max(1, height) };
}

export function buttonComponentToRenderableAt(
  payload: DesignPreviewPayload,
  button: ButtonDesignContract,
  box: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const content = buttonContentLayout(payload, button);
  const { iconSize, labelSize } = content;
  const contentX = box.x + (box.width - content.width) * 0.5;
  const contentY = box.y + (box.height - content.height) * 0.5;
  const children: RenderableNode[] = [surfaceComponentToRenderableAt(payload, button.appearance.surface, box)];

  if (iconSize > 0 && button.iconToken !== null) {
    children.push({
      id: `${button.id}.glyph`, type: "icon", frame: 0,
      box: { x: contentX + content.iconX, y: contentY + content.iconY, width: iconSize, height: iconSize },
      text: button.iconToken,
      style: { ...iconTokenStyle(payload, button.iconToken, selectedColor(payload, button.appearance.iconColorToken, 1)) },
    });
  }
  if (button.appearance.label && labelSize) {
    const labelBox: RenderableBox = {
      x: contentX + content.labelX,
      y: contentY + content.labelY,
      width: labelSize.width,
      height: labelSize.height,
    };
    children.push(labelComponentToRenderableAt(payload, button.appearance.label, labelBox));
  }
  if (button.badge) children.push(badgeComponentToRenderableAt(payload, button.badge, box));

  return wrapVisualAccent({
    id: button.id,
    type: "group",
    frame: 0,
    box,
    transform: {
      opacity: button.opacity,
      scale: button.scale,
    },
    style: {
      overflow: "visible",
    },
    children,
  }, button.accent, scale);
}
