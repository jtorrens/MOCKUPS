import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { ContentRowDesignContract } from "./contentRowComponentContract.js";
import { avatarComponentToRenderable } from "./avatarComponentRenderable.js";
import { buttonComponentToRenderable, measureButtonComponent } from "./buttonComponentRenderable.js";
import { boundedCenterBox, numberToken, renderScale, selectedColor } from "./componentRenderableCommon.js";
import { labelComponentToRenderable, measureLabelComponent } from "./labelComponentRenderable.js";
import { translateRenderableNode } from "./previewGeometryHelpers.js";

interface MeasuredSlot {
  index: number;
  node: RenderableNode;
  width: number;
  height: number;
}

export function contentRowComponentToRenderable(
  payload: DesignPreviewPayload,
  row: ContentRowDesignContract,
  assignedBox?: RenderableBox,
): RenderableNode {
  const measured = row.slots.flatMap((slot) => {
    if (!slot.content) return [];
    if (slot.kind === "label" && !("text" in slot.content && (slot.content.text.trim() || slot.content.subtext.trim()))) return [];
    const node = slot.kind === "avatar"
      ? avatarComponentToRenderable(payload, slot.content as Parameters<typeof avatarComponentToRenderable>[1])
      : slot.kind === "icon"
        ? buttonComponentToRenderable(payload, slot.content as Parameters<typeof buttonComponentToRenderable>[1])
        : labelComponentToRenderable(payload, slot.content as Parameters<typeof labelComponentToRenderable>[1]);
    const size = slot.kind === "icon"
      ? measureButtonComponent(payload, slot.content as Parameters<typeof measureButtonComponent>[1])
      : slot.kind === "label"
        ? measureLabelComponent(slot.content as Parameters<typeof measureLabelComponent>[0], payload)
        : node.box ?? { width: 1, height: 1 };
    return [{ index: slot.index, node, width: size.width, height: size.height } satisfies MeasuredSlot];
  });
  const scale = renderScale(payload);
  const paddingX = numberToken(payload, row.padding.xToken) * scale;
  const paddingY = numberToken(payload, row.padding.yToken) * scale;
  const contentHeight = measured.reduce((height, item) => Math.max(height, item.height), 0);
  const separatorHeight = row.showSeparator ? Math.max(1, scale) : 0;
  const height = contentHeight + paddingY * 2 + separatorHeight;
  const width = assignedBox?.width ?? row.size.width * scale;
  const box = assignedBox
    ? { x: assignedBox.x, y: assignedBox.y, width, height }
    : boundedCenterBox(payload, width, height);
  const contentY = box.y + paddingY;
  const leftEdge = box.x + paddingX;
  const rightEdge = box.x + box.width - paddingX;
  const left = measured.find((item) => item.index === 1);
  const right = measured.find((item) => item.index === 5);
  const middle = measured.filter((item) => item.index >= 2 && item.index <= 4);
  const children: RenderableNode[] = [];
  if (left) children.push(place(left, leftEdge, alignedY(row, contentY, contentHeight, left.height)));
  if (right) children.push(place(right, rightEdge - right.width, alignedY(row, contentY, contentHeight, right.height)));
  const middleLeft = leftEdge + (left?.width ?? 0);
  const middleRight = rightEdge - (right?.width ?? 0);
  const middleWidth = middle.reduce((value, item) => value + item.width, 0);
  const gap = middle.length ? (middleRight - middleLeft - middleWidth) / (middle.length + 1) : 0;
  let x = middleLeft + gap;
  for (const item of middle) {
    children.push(place(item, x, alignedY(row, contentY, contentHeight, item.height)));
    x += item.width + gap;
  }
  if (row.showSeparator) children.push({
    id: `${row.id}.separator`, type: "surface", frame: 0,
    box: { x: box.x, y: box.y + height - separatorHeight, width: box.width, height: separatorHeight },
    style: { background: selectedColor(payload, "theme.colors.divider") },
  });
  return { id: row.id, type: "group", frame: 0, box, style: { overflow: "visible" }, children };
}

function alignedY(row: ContentRowDesignContract, y: number, contentHeight: number, itemHeight: number) {
  if (row.verticalAlignment === "bottom") return y + contentHeight - itemHeight;
  if (row.verticalAlignment === "center") return y + (contentHeight - itemHeight) * 0.5;
  return y;
}

function place(item: MeasuredSlot, x: number, y: number): RenderableNode {
  const translated = translateRenderableNode(item.node, {
    x: x - (item.node.box?.x ?? 0),
    y: y - (item.node.box?.y ?? 0),
  });
  return { id: `component.contentRow.slot.${item.index}`, type: "group", frame: 0, box: translated.box, style: { overflow: "visible" }, children: [translated] };
}
