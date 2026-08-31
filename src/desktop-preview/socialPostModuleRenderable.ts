import type { RenderableNode } from "../visual/renderable/types.js";
import { embeddedComponentConfig } from "./componentPreviewDefaults.js";
import { componentClassToRenderable } from "./componentRenderableBoundary.js";
import {
  numberToken,
  previewScreenBox,
  selectedColor,
} from "./componentRenderableCommon.js";
import { parseObject } from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { renderScale, translateRenderableNode } from "./previewGeometryHelpers.js";
import type {
  SocialPostComponentSlot,
  SocialPostHeaderRow,
  SocialPostModuleContract,
} from "./socialPostModuleContract.js";
import { resolveSocialPostModule } from "./socialPostModuleResolver.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";
import { wallpaperRenderable } from "./wallpaperRenderable.js";

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

export function socialPostModuleToRenderable(
  payload: DesignPreviewPayload,
): RenderableNode {
  const contract = resolveSocialPostModule(payload);
  const screen = previewScreenBox(payload);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const statusReference = payload.themeStatusBarVariantReference?.trim() ?? "";
  const navigationReference = payload.themeNavigationBarVariantReference?.trim() ?? "";
  const status = contract.showStatusBar && statusReference
    ? componentNode(payload, componentBaseConfigs, "status_bar", {
        variantReference: statusReference,
        overrides: {},
      }, {})
    : undefined;
  const navigation = contract.showNavigationBar && navigationReference
    ? componentNode(payload, componentBaseConfigs, "navigation_bar", {
        variantReference: navigationReference,
        overrides: {},
      }, {})
    : undefined;
  const contentY = screen.y + (status?.box?.height ?? 0);
  const header = contract.showHeader
    ? headerNode(payload, componentBaseConfigs, contract, contentY)
    : undefined;
  const backgroundNode = contract.useAppWallpaper
    ? wallpaperRenderable(payload, screen) ?? background(payload)
    : background(payload);
  const children: RenderableNode[] = [backgroundNode];
  if (header) children.push(header);
  if (status) children.push(withZIndex(status, 20));
  if (navigation) children.push(withZIndex(navigation, 30));
  return {
    id: contract.id,
    type: "group",
    frame: 0,
    box: screen,
    style: { overflow: "hidden" },
    children,
  };
}

function headerNode(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  contract: SocialPostModuleContract,
  contentY: number,
): RenderableNode {
  const screen = previewScreenBox(payload);
  const scale = renderScale(payload);
  const first = renderRow(payload, componentBaseConfigs, contract.rows[0], contentY);
  const gap = numberToken(payload, contract.rowGapToken) * scale;
  const secondY = contentY + first.height + gap;
  const second = renderRow(payload, componentBaseConfigs, contract.rows[1], secondY);
  const headerHeight = first.height + gap + second.height;
  const surfaceBox = {
    x: screen.x,
    y: screen.y,
    width: screen.width,
    height: Math.max(0, contentY + headerHeight - screen.y),
  };
  const surface = resolveSurfaceComponentAtSize(
    embeddedComponentConfig(
      componentBaseConfigs,
      contract.headerSurfaceSlot,
      "surface",
      "module.core.socialPost.headerSurfaceSlot",
    ),
    { width: surfaceBox.width / scale, height: surfaceBox.height / scale },
    "module.core.socialPost.header.surface",
  );
  return {
    id: "module.core.socialPost.header",
    type: "group",
    frame: 0,
    box: {
      x: screen.x,
      y: contentY,
      width: screen.width,
      height: headerHeight,
    },
    style: { overflow: "visible" },
    children: [
      surfaceComponentToRenderableAt(payload, surface, surfaceBox),
      first.node,
      second.node,
    ],
  };
}

function renderRow(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  row: SocialPostHeaderRow,
  y: number,
): RenderedRow {
  const screen = previewScreenBox(payload);
  const scale = renderScale(payload);
  const measured = row.slots.flatMap((slot) => {
    if (!slot.componentType || !slot.componentSlot) return [];
    const node = componentNode(
      payload,
      componentBaseConfigs,
      slot.componentType,
      slot.componentSlot,
      slot.inputs,
    );
    if (!node.box) return [];
    return [{
      index: slot.index,
      node,
      width: node.box.width,
      height: node.box.height,
    } satisfies MeasuredSlot];
  });
  const rowHeight = measured.reduce((height, item) => Math.max(height, item.height), 0);
  const [leftPadding, rightPadding] = spacingPair(payload, row.padding);
  const leftEdge = screen.x + leftPadding;
  const rightEdge = screen.x + screen.width - rightPadding;
  const left = measured.find((item) => item.index === 1);
  const right = measured.find((item) => item.index === 5);
  const middle = measured.filter((item) => item.index >= 2 && item.index <= 4);
  const children: RenderableNode[] = [];

  if (left) children.push(placeMeasuredSlot(left, leftEdge, rowY(row, y, rowHeight, left.height)));
  if (right) {
    children.push(placeMeasuredSlot(
      right,
      rightEdge - right.width,
      rowY(row, y, rowHeight, right.height),
    ));
  }

  const middleLeft = leftEdge + (left?.width ?? 0);
  const middleRight = rightEdge - (right?.width ?? 0);
  const middleWidth = middle.reduce((width, item) => width + item.width, 0);
  const freeWidth = Math.max(0, middleRight - middleLeft - middleWidth);
  const middleGap = middle.length > 0 ? freeWidth / (middle.length + 1) : 0;
  let middleX = middleLeft + middleGap;
  for (const item of middle) {
    children.push(placeMeasuredSlot(item, middleX, rowY(row, y, rowHeight, item.height)));
    middleX += item.width + middleGap;
  }

  const separatorHeight = row.showSeparator ? Math.max(1, scale) : 0;
  if (row.showSeparator) {
    children.push({
      id: `module.core.socialPost.header.${row.id}.separator`,
      type: "surface",
      frame: 0,
      box: {
        x: screen.x,
        y: y + rowHeight,
        width: screen.width,
        height: separatorHeight,
      },
      style: { background: selectedColor(payload, "theme.colors.divider") },
    });
  }
  const height = rowHeight + separatorHeight;
  return {
    height,
    node: {
      id: `module.core.socialPost.header.${row.id}`,
      type: "group",
      frame: 0,
      box: { x: screen.x, y, width: screen.width, height },
      style: { overflow: "visible" },
      children,
    },
  };
}

function rowY(
  row: SocialPostHeaderRow,
  y: number,
  rowHeight: number,
  itemHeight: number,
) {
  if (row.verticalAlignment === "bottom") return y + rowHeight - itemHeight;
  if (row.verticalAlignment === "center") return y + (rowHeight - itemHeight) * 0.5;
  return y;
}

function placeMeasuredSlot(item: MeasuredSlot, x: number, y: number): RenderableNode {
  const translated = translateRenderableNode(item.node, {
    x: x - (item.node.box?.x ?? 0),
    y: y - (item.node.box?.y ?? 0),
  });
  return {
    id: `module.core.socialPost.header.slot.${item.index}.${translated.id}`,
    type: "group",
    frame: 0,
    box: translated.box,
    style: { overflow: "visible" },
    children: [translated],
  };
}

function componentNode(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  componentType: string,
  slot: SocialPostComponentSlot,
  inputs: Record<string, unknown>,
) {
  return componentClassToRenderable({
    ...payload,
    componentType,
    configJson: JSON.stringify(embeddedComponentConfig(
      componentBaseConfigs,
      slot,
      componentType,
      `module.core.socialPost.${componentType}`,
    )),
    designPreviewJson: JSON.stringify(inputs),
  });
}

function spacingPair(payload: DesignPreviewPayload, value: string) {
  const [leftToken = "theme.spacing.none", rightToken = leftToken] = value.split("|");
  const scale = renderScale(payload);
  return [
    numberToken(payload, leftToken) * scale,
    numberToken(payload, rightToken) * scale,
  ] as const;
}

function background(payload: DesignPreviewPayload): RenderableNode {
  return {
    id: "module.core.socialPost.background",
    type: "surface",
    frame: 0,
    box: previewScreenBox(payload),
    style: { background: selectedColor(payload, "theme.colors.background") },
    metadata: { paintRole: "moduleBackground" },
  };
}

function withZIndex(node: RenderableNode, zIndex: number): RenderableNode {
  return { ...node, style: { ...node.style, zIndex } };
}
