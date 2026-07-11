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

export function buttonComponentToRenderable(payload: DesignPreviewPayload, button: ButtonDesignContract): RenderableNode {
  const scale = renderScale(payload);
  const iconSize = button.contentMode === "text" ? 0 : numberToken(payload, button.iconSizeToken) * scale;
  const labelSize = button.stateStyle.label ? measureLabelComponent(button.stateStyle.label, payload) : undefined;
  const gap = button.contentMode === "iconText" ? numberToken(payload, button.contentGapToken) * scale : 0;
  const paddingX = numberToken(payload, button.padding.xToken) * scale;
  const paddingY = numberToken(payload, button.padding.yToken) * scale;
  const contentWidth = iconSize + gap + (labelSize?.width ?? 0);
  const contentHeight = Math.max(iconSize, labelSize?.height ?? 0);
  const width = button.dimensionMode === "fixed" ? button.size.width * scale : contentWidth + paddingX * 2;
  const height = button.dimensionMode === "fixed" ? button.size.height * scale : contentHeight + paddingY * 2;
  const box = boundedCenterBox(payload, Math.max(1, width), Math.max(1, height));
  const contentX = box.x + (box.width - contentWidth) * 0.5;
  const children: RenderableNode[] = [surfaceComponentToRenderableAt(payload, button.stateStyle.surface, box)];

  if (iconSize > 0) {
    children.push({
      id: `${button.id}.glyph`, type: "icon", frame: 0,
      box: { x: contentX, y: box.y + (box.height - iconSize) * 0.5, width: iconSize, height: iconSize },
      text: button.iconToken,
      style: { ...iconTokenStyle(payload, button.iconToken, selectedColor(payload, button.stateStyle.iconColorToken, 1)) },
    });
  }
  if (button.stateStyle.label && labelSize) {
    const labelBox: RenderableBox = {
      x: contentX + iconSize + gap,
      y: box.y + (box.height - labelSize.height) * 0.5,
      width: labelSize.width,
      height: labelSize.height,
    };
    children.push(labelComponentToRenderableAt(payload, button.stateStyle.label, labelBox));
  }

  return {
    id: button.id,
    type: "group",
    frame: 0,
    box,
    style: {
      overflow: "visible",
      opacity: button.stateStyle.opacity,
    },
    children,
  };
}
