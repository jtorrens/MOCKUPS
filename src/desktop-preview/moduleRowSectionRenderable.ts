import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { embeddedComponentConfig } from "./componentPreviewDefaults.js";
import { componentClassToRenderable } from "./componentRenderableBoundary.js";
import { numberToken, previewScreenBox, selectedColor } from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { ModuleRow } from "./moduleRowSectionContract.js";
import { renderScale, translateRenderableNode } from "./previewGeometryHelpers.js";

interface MeasuredSlot {
  index: number;
  node: RenderableNode;
  width: number;
  height: number;
}

interface RenderedRow {
  node: RenderableNode;
  height: number;
}

export function rowsSectionNode(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  options: {
    ownerId: string;
    section: "header" | "footer";
    rows: [ModuleRow, ModuleRow];
    rowGapToken: string;
    height: number;
    renderSurface: (box: RenderableBox) => RenderableNode;
    edge: "top" | "bottom";
    contentEdge: number;
    horizontalInset?: number;
    edgeOffset?: number;
    bleedToScreenEdge?: boolean;
    contentAlignment?: "bottom" | "center";
  },
): RenderableNode {
  const screen = previewScreenBox(payload);
  const scale = renderScale(payload);
  const horizontalInset = Math.max(0, options.horizontalInset ?? 0);
  const sectionX = screen.x + horizontalInset;
  const sectionWidth = Math.max(1, screen.width - horizontalInset * 2);
  const first = renderRow(payload, componentBaseConfigs, options.ownerId, options.section, options.rows[0], 0, sectionX, sectionWidth);
  const gap = numberToken(payload, options.rowGapToken) * scale;
  const second = renderRow(payload, componentBaseConfigs, options.ownerId, options.section, options.rows[1], 0, sectionX, sectionWidth);
  const rowsHeight = first.height + gap + second.height;
  const sectionHeight = Math.max(options.height * scale, rowsHeight);
  const edgeOffset = Math.max(0, options.edgeOffset ?? 0);
  const sectionY = options.edge === "top"
    ? options.contentEdge + edgeOffset
    : options.contentEdge - sectionHeight - edgeOffset;
  const rowsY = options.contentAlignment === "center"
    ? sectionY + (sectionHeight - rowsHeight) * 0.5
    : sectionY + sectionHeight - rowsHeight;
  const firstNode = translateRenderableNode(first.node, { x: 0, y: rowsY });
  const secondNode = translateRenderableNode(second.node, { x: 0, y: rowsY + first.height + gap });
  const surfaceBox = options.bleedToScreenEdge === false
    ? { x: sectionX, y: sectionY, width: sectionWidth, height: sectionHeight }
    : {
      x: screen.x,
      y: options.edge === "top" ? screen.y : sectionY,
      width: screen.width,
      height: options.edge === "top"
        ? Math.max(0, sectionY + sectionHeight - screen.y)
        : Math.max(0, screen.y + screen.height - sectionY),
    };
  const surface = options.renderSurface(surfaceBox);
  return {
    id: `${options.ownerId}.${options.section}`,
    type: "group",
    frame: 0,
    box: { x: sectionX, y: sectionY, width: sectionWidth, height: sectionHeight },
    style: { overflow: "visible" },
    children: [
      surface,
      firstNode,
      secondNode,
    ],
  };
}

function renderRow(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  ownerId: string,
  section: "header" | "footer",
  row: ModuleRow,
  y: number,
  sectionX: number,
  sectionWidth: number,
): RenderedRow {
  const scale = renderScale(payload);
  const measured = row.slots.flatMap((slot) => {
    if (!slot.componentType || !slot.componentSlot || !rowSlotHasContent(slot)) return [];
    const node = componentClassToRenderable({
      ...payload,
      componentType: slot.componentType,
      configJson: JSON.stringify(embeddedComponentConfig(
        componentBaseConfigs,
        slot.componentSlot,
        slot.componentType,
        `${ownerId}.${section}.${row.id}.slot.${slot.index}`,
      )),
      designPreviewJson: JSON.stringify(slot.inputs),
    });
    if (!node.box) return [];
    return [{ index: slot.index, node, width: node.box.width, height: node.box.height } satisfies MeasuredSlot];
  });
  const contentHeight = measured.reduce((height, item) => Math.max(height, item.height), 0);
  const [horizontalPadding, verticalPadding] = spacingPair(payload, row.padding);
  const rowHeight = contentHeight + verticalPadding * 2;
  const contentY = y + verticalPadding;
  const leftEdge = sectionX + horizontalPadding;
  const rightEdge = sectionX + sectionWidth - horizontalPadding;
  const left = measured.find((item) => item.index === 1);
  const right = measured.find((item) => item.index === 5);
  const middle = measured.filter((item) => item.index >= 2 && item.index <= 4);
  const children: RenderableNode[] = [];

  if (left) children.push(placeMeasuredSlot(ownerId, section, row.id, left, leftEdge, rowY(row, contentY, contentHeight, left.height)));
  if (right) children.push(placeMeasuredSlot(ownerId, section, row.id, right, rightEdge - right.width, rowY(row, contentY, contentHeight, right.height)));

  const middleLeft = leftEdge + (left?.width ?? 0);
  const middleRight = rightEdge - (right?.width ?? 0);
  const middleWidth = middle.reduce((width, item) => width + item.width, 0);
  const freeWidth = middleRight - middleLeft - middleWidth;
  const middleGap = middle.length > 0 ? freeWidth / (middle.length + 1) : 0;
  let middleX = middleLeft + middleGap;
  for (const item of middle) {
    children.push(placeMeasuredSlot(ownerId, section, row.id, item, middleX, rowY(row, contentY, contentHeight, item.height)));
    middleX += item.width + middleGap;
  }

  const separatorHeight = row.showSeparator ? Math.max(1, scale) : 0;
  if (row.showSeparator) {
    children.push({
      id: `${ownerId}.${section}.${row.id}.separator`,
      type: "surface",
      frame: 0,
      box: { x: sectionX, y: y + rowHeight, width: sectionWidth, height: separatorHeight },
      style: { background: selectedColor(payload, "theme.colors.divider") },
    });
  }
  const height = rowHeight + separatorHeight;
  return {
    height,
    node: {
      id: `${ownerId}.${section}.${row.id}`,
      type: "group",
      frame: 0,
      box: { x: sectionX, y, width: sectionWidth, height },
      style: { overflow: "visible" },
      children,
    },
  };
}

function rowSlotHasContent(slot: ModuleRow["slots"][number]) {
  if (slot.kind === "none") return false;
  if (slot.kind !== "label") return true;
  const label = typeof slot.inputs.sampleText === "string" ? slot.inputs.sampleText.trim() : "";
  const sublabel = typeof slot.inputs.sampleSubtext === "string" ? slot.inputs.sampleSubtext.trim() : "";
  return label.length > 0 || sublabel.length > 0;
}

function rowY(row: ModuleRow, y: number, rowHeight: number, itemHeight: number) {
  if (row.verticalAlignment === "bottom") return y + rowHeight - itemHeight;
  if (row.verticalAlignment === "center") return y + (rowHeight - itemHeight) * 0.5;
  return y;
}

function placeMeasuredSlot(
  ownerId: string,
  section: "header" | "footer",
  rowId: ModuleRow["id"],
  item: MeasuredSlot,
  x: number,
  y: number,
): RenderableNode {
  const translated = translateRenderableNode(item.node, {
    x: x - (item.node.box?.x ?? 0),
    y: y - (item.node.box?.y ?? 0),
  });
  return {
    id: `${ownerId}.${section}.${rowId}.slot.${item.index}.${translated.id}`,
    type: "group",
    frame: 0,
    box: translated.box,
    style: { overflow: "visible" },
    children: [translated],
  };
}

function spacingPair(payload: DesignPreviewPayload, value: string) {
  const [leftToken = "theme.spacing.none", rightToken = leftToken] = value.split("|");
  const scale = renderScale(payload);
  return [numberToken(payload, leftToken) * scale, numberToken(payload, rightToken) * scale] as const;
}
