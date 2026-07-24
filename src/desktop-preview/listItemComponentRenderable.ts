import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { avatarComponentToRenderableAt } from "./avatarComponentRenderable.js";
import {
  boundedCenterBox,
  numberToken,
  renderScale,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  iconRowComponentToRenderableAt,
  measureIconRowComponent,
} from "./iconRowComponentRenderable.js";
import { labelComponentToRenderableAt } from "./labelComponentRenderable.js";
import type {
  ListItemDesignContract,
  ListItemElement,
  ListItemVerticalAlignment,
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
  const elements = flowElements(payload, listItem, box);
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

function flowElements(
  payload: DesignPreviewPayload,
  listItem: ListItemDesignContract,
  box: RenderableBox,
) {
  const scale = renderScale(payload);
  const paddingX = Math.max(0, numberToken(payload, listItem.padding.xToken) * scale);
  const paddingY = Math.max(0, numberToken(payload, listItem.padding.yToken) * scale);
  const gap = Math.max(0, numberToken(payload, listItem.gapToken) * scale);
  const inner = {
    x: box.x + paddingX,
    y: box.y + paddingY,
    width: box.width - paddingX * 2,
    height: box.height - paddingY * 2,
  };
  if (inner.width <= 0 || inner.height <= 0) {
    throw new Error("component.listItem Runtime size must exceed its Variant padding");
  }

  const measured = listItem.elements.map((element) => ({
    element,
    size: elementSize(payload, element, inner.height, scale),
  }));
  const fillLabels = measured.filter(({ element }) =>
    element.componentType === "label" && element.sizeMode === "fill");
  if (fillLabels.length > 1) {
    throw new Error("component.listItem may contain at most one fill Label");
  }
  const gaps = Math.max(0, measured.length - 1) * gap;
  const fixedWidth = measured.reduce((sum, entry) =>
    sum + (entry.size.width < 0 ? 0 : entry.size.width), 0);
  const remaining = inner.width - gaps - fixedWidth;
  if (remaining < 0) {
    throw new Error(
      "component.listItem components, padding and gaps exceed the Runtime width",
    );
  }
  if (fillLabels.length === 1) {
    fillLabels[0]!.size.width = remaining;
  }

  let cursor = inner.x;
  return measured.map(({ element, size }) => {
    if (size.width <= 0 || size.height <= 0 || size.height > inner.height) {
      throw new Error(
        `component.listItem '${element.componentType}' does not fit inside the Runtime height`,
      );
    }
    const elementBox = {
      x: cursor,
      y: alignedY(inner.y, inner.height, size.height, element.verticalAlignment),
      width: size.width,
      height: size.height,
    };
    cursor += size.width + gap;
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
        { maximumWidth: elementBox.width },
      );
    }
    return iconRowComponentToRenderableAt(payload, element.component, elementBox);
  });
}

function elementSize(
  payload: DesignPreviewPayload,
  element: ListItemElement,
  innerHeight: number,
  scale: number,
) {
  if (element.componentType === "avatar") {
    const side = element.sizeMode === "auto"
      ? innerHeight
      : element.fixedSize * scale;
    return { width: side, height: side };
  }
  if (element.componentType === "label") {
    return element.sizeMode === "fill"
      ? { width: -1, height: innerHeight }
      : {
          width: element.fixedSize.width * scale,
          height: element.fixedSize.height * scale,
        };
  }
  if (element.sizeMode === "fixed") {
    return {
      width: element.fixedSize.width * scale,
      height: element.fixedSize.height * scale,
    };
  }
  const measured = measureIconRowComponent(payload, element.component);
  return { width: measured.width, height: measured.height };
}

function alignedY(
  top: number,
  availableHeight: number,
  height: number,
  alignment: ListItemVerticalAlignment,
) {
  if (alignment === "start") return top;
  if (alignment === "end") return top + availableHeight - height;
  return top + (availableHeight - height) / 2;
}
