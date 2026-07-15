import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { numberToken, previewScreenBox, renderScale } from "./componentRenderableCommon.js";
import { codeIndicatorComponentToRenderableAt, measureCodeIndicatorComponent } from "./codeIndicatorComponentRenderable.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { iconBarComponentToRenderableAt } from "./iconBarComponentRenderable.js";
import { keypadComponentToRenderableAt, measureKeypadComponent } from "./keypadComponentRenderable.js";
import { labelComponentToRenderableAt, measureLabelComponent } from "./labelComponentRenderable.js";
import type { PasswordDesignContract } from "./passwordComponentContract.js";

export function passwordComponentToRenderable(
  payload: DesignPreviewPayload,
  password: PasswordDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const labelSize = measureLabelComponent(password.label, payload);
  const indicatorSize = measureCodeIndicatorComponent(payload, password.indicator);
  const keypadSize = measureKeypadComponent(payload, password.keypad);
  const iconBarHeight = password.iconBar.size.height * scale;
  const labelIndicatorGap = gap(payload, password.labelIndicatorGapToken, scale);
  const startGap = gap(payload, password.startGapToken, scale);
  const upperGap = gap(payload, password.upperGapToken, scale);
  const lowerGap = gap(payload, password.lowerGapToken, scale);
  const endGap = gap(payload, password.endGapToken, scale);
  const box = previewScreenBox(payload);
  const keypadBox = centeredChildBox(
    box,
    box.y + (box.height - keypadSize.height) * 0.5,
    keypadSize.width,
    keypadSize.height,
  );
  const upperBlockHeight = labelSize.height + labelIndicatorGap + indicatorSize.height;
  const upperY = password.upperAnchor === "container"
    ? box.y + startGap
    : keypadBox.y - upperGap - upperBlockHeight;
  const labelBox = centeredChildBox(box, upperY, labelSize.width, labelSize.height);
  const indicatorBox = centeredChildBox(
    box,
    upperY + labelSize.height + labelIndicatorGap,
    indicatorSize.width,
    indicatorSize.height,
  );
  const iconBarY = password.lowerAnchor === "container"
    ? box.y + box.height - endGap - iconBarHeight
    : keypadBox.y + keypadBox.height + lowerGap;
  const iconBarBox: RenderableBox = {
    x: box.x,
    y: iconBarY,
    width: box.width,
    height: iconBarHeight,
  };
  return {
    id: password.id,
    type: "group",
    frame: 0,
    box,
    style: { overflow: "visible" },
    children: [
      labelComponentToRenderableAt(payload, password.label, labelBox),
      codeIndicatorComponentToRenderableAt(payload, password.indicator, indicatorBox),
      keypadComponentToRenderableAt(payload, password.keypad, keypadBox),
      iconBarComponentToRenderableAt(payload, password.iconBar, iconBarBox),
    ],
  };
}

function gap(payload: DesignPreviewPayload, token: string, scale: number) {
  return Math.max(0, numberToken(payload, token) * scale);
}

function centeredChildBox(
  parent: RenderableBox,
  y: number,
  width: number,
  height: number,
): RenderableBox {
  return { x: parent.x + (parent.width - width) * 0.5, y, width, height };
}
