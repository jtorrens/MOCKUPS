import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { avatarComponentToRenderableAt } from "./avatarComponentRenderable.js";
import {
  boundedCenterBox,
  placeChild,
  renderScale,
  scalePlacement,
  selectedColor,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { iconRowComponentToRenderableAt } from "./iconRowComponentRenderable.js";
import { labelComponentToRenderableAt } from "./labelComponentRenderable.js";
import type {
  ListItemDesignContract,
  ListItemElement,
} from "./listItemComponentContract.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";

export function listItemComponentToRenderable(
  payload: DesignPreviewPayload,
  listItem: ListItemDesignContract,
  assignedBox?: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const intrinsicSize = {
    width: listItem.size.width * scale,
    height: listItem.size.height * scale,
  };
  const box = assignedBox ?? boundedCenterBox(
    payload,
    intrinsicSize.width,
    intrinsicSize.height,
  );
  const elements = listItem.elements.map((element) =>
    elementToRenderable(payload, element, box, scale));
  const elementsGroup: RenderableNode = {
    id: `${listItem.id}.elements`,
    type: "group",
    frame: 0,
    box,
    style: { overflow: "visible" },
    transform: { opacity: listItem.elementsOpacity },
    children: elements,
  };
  return {
    id: listItem.id,
    type: "group",
    frame: 0,
    box,
    style: { overflow: "hidden" },
    children: [
      surfaceComponentToRenderableAt(payload, listItem.surface, box),
      elementsGroup,
    ],
  };
}

function elementToRenderable(
  payload: DesignPreviewPayload,
  element: ListItemElement,
  box: RenderableBox,
  scale: number,
) {
  const elementBox = placeChild(
    box,
    {
      width: element.size.width * scale,
      height: element.size.height * scale,
    },
    scalePlacement(element.placement, scale),
  );
  if (element.componentType === "avatar") {
    const side = Math.min(elementBox.width, elementBox.height);
    return avatarComponentToRenderableAt(
      payload,
      element.component,
      {
        x: elementBox.x + (elementBox.width - side) / 2,
        y: elementBox.y + (elementBox.height - side) / 2,
        width: side,
        height: side,
      },
    );
  }
  if (element.componentType === "label") {
    return labelComponentToRenderableAt(
      payload,
      element.component,
      elementBox,
      {
        maximumWidth: elementBox.width,
        textColor: selectedColor(payload, element.textColorToken),
        subtextColor: selectedColor(payload, element.subtextColorToken),
      },
    );
  }
  return iconRowComponentToRenderableAt(
    payload,
    element.component,
    elementBox,
  );
}
