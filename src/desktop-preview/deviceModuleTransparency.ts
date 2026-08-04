import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type {
  DesignPreviewPayload,
  DeviceModuleTransparencyPayload,
} from "./designPreviewPayload.js";
import { selectedPaletteColor } from "./previewColorHelpers.js";

type Matrix = readonly [number, number, number, number, number, number];

const identity: Matrix = [1, 0, 0, 1, 0, 0];

export function applyDeviceModuleTransparency(
  payload: DesignPreviewPayload,
  module: RenderableNode,
): RenderableNode {
  const policy = requiredDeviceModuleTransparency(payload.previewFrame.moduleTransparency);
  if (!policy.enabled) return module;
  if (!module.box) {
    throw new Error("Device module transparency requires a resolved Module box.");
  }

  const screen = screenBox(payload);
  const content = removeModuleBackgrounds(module);
  const contentBottom = bottomVisiblePixel(content, screen);
  const start = policy.mode === "fixed"
    ? screen.y + policy.fixedStart
    : contentBottom + policy.variableOffset;
  const end = start + policy.gradientHeight;
  const background: RenderableNode = {
    id: "device.moduleTransparency.background",
    type: "surface",
    frame: payload.localFrame,
    box: screen,
    style: {
      background: selectedPaletteColor(payload, policy.paletteColor, 1),
    },
    metadata: { paintRole: "moduleBackground" },
  };

  return {
    ...content,
    style: {
      ...content.style,
      overflow: "hidden",
      opacityMask: {
        axis: "vertical",
        start,
        end,
        beforeOpacity: policy.opacity,
        afterOpacity: 0,
      },
    },
    children: [background, ...(content.children ?? [])],
  };
}

export function requiredDeviceModuleTransparency(
  value: unknown,
): DeviceModuleTransparencyPayload {
  if (!isRecord(value)) {
    throw new Error("Preview Device moduleTransparency must be an object.");
  }
  const required = [
    "enabled",
    "mode",
    "paletteColor",
    "opacity",
    "fixedStart",
    "gradientHeight",
    "variableOffset",
  ] as const;
  const keys = Object.keys(value);
  const missing = required.filter((key) => !Object.hasOwn(value, key));
  const unknown = keys.filter((key) => !required.includes(key as typeof required[number]));
  if (missing.length || unknown.length) {
    throw new Error(
      `Preview Device moduleTransparency must contain only its current properties; missing [${missing.join(", ")}], unknown [${unknown.join(", ")}].`,
    );
  }
  if (typeof value.enabled !== "boolean") {
    throw new Error("Preview Device moduleTransparency.enabled must be boolean.");
  }
  if (value.mode !== "fixed" && value.mode !== "variable") {
    throw new Error("Preview Device moduleTransparency.mode must be 'fixed' or 'variable'.");
  }
  if (typeof value.paletteColor !== "string" || !value.paletteColor.trim()) {
    throw new Error("Preview Device moduleTransparency.paletteColor must be a non-empty string.");
  }
  const opacity = finiteNumber(value.opacity, "opacity");
  const fixedStart = finiteNumber(value.fixedStart, "fixedStart");
  const gradientHeight = finiteNumber(value.gradientHeight, "gradientHeight");
  const variableOffset = finiteNumber(value.variableOffset, "variableOffset");
  if (opacity < 0 || opacity > 1) {
    throw new Error("Preview Device moduleTransparency.opacity must be between 0 and 1.");
  }
  if (fixedStart < 0) {
    throw new Error("Preview Device moduleTransparency.fixedStart must be non-negative.");
  }
  if (gradientHeight <= 0) {
    throw new Error("Preview Device moduleTransparency.gradientHeight must be positive.");
  }
  return {
    enabled: value.enabled,
    mode: value.mode,
    paletteColor: value.paletteColor,
    opacity,
    fixedStart,
    gradientHeight,
    variableOffset,
  };
}

function removeModuleBackgrounds(node: RenderableNode): RenderableNode {
  return {
    ...node,
    children: (node.children ?? [])
      .filter((child) => child.metadata?.paintRole !== "moduleBackground")
      .map(removeModuleBackgrounds),
  };
}

function bottomVisiblePixel(node: RenderableNode, screen: RenderableBox) {
  const bottoms: number[] = [];
  collectPaintBottoms(node, identity, screen, 1, bottoms);
  return bottoms.length ? Math.max(...bottoms) : screen.y;
}

function collectPaintBottoms(
  node: RenderableNode,
  parentMatrix: Matrix,
  inheritedClip: RenderableBox | undefined,
  inheritedOpacity: number,
  bottoms: number[],
) {
  const opacity = inheritedOpacity * nodeOpacity(node);
  if (opacity <= 0) return;
  const matrix = multiply(parentMatrix, nodeMatrix(node));
  const transformedBox = node.box ? transformBox(node.box, matrix) : undefined;
  const clip = node.style?.overflow === "hidden" && transformedBox
    ? intersectBoxes(inheritedClip, transformedBox)
    : inheritedClip;
  if (node.type !== "group" && transformedBox) {
    const visible = intersectBoxes(clip, transformedBox);
    if (visible && visible.width > 0 && visible.height > 0) {
      bottoms.push(visible.y + visible.height);
    }
  }
  for (const child of node.children ?? []) {
    collectPaintBottoms(child, matrix, clip, opacity, bottoms);
  }
}

function nodeOpacity(node: RenderableNode) {
  const value = node.transform?.opacity ?? node.style?.opacity ?? 1;
  return typeof value === "number" && Number.isFinite(value) ? value : 1;
}

function nodeMatrix(node: RenderableNode): Matrix {
  const transform = node.transform;
  if (!transform) return identity;
  const translate = translation(transform.x ?? 0, transform.y ?? 0);
  const rotation = transform.rotation ?? 0;
  const scale = transform.scale ?? 1;
  if ((!rotation && scale === 1) || !node.box) return translate;
  const centerX = node.box.x + node.box.width / 2;
  const centerY = node.box.y + node.box.height / 2;
  return multiply(
    translate,
    multiply(
      translation(centerX, centerY),
      multiply(
        rotationScale(rotation, scale),
        translation(-centerX, -centerY),
      ),
    ),
  );
}

function translation(x: number, y: number): Matrix {
  return [1, 0, 0, 1, x, y];
}

function rotationScale(degrees: number, scale: number): Matrix {
  const radians = degrees * Math.PI / 180;
  const cosine = Math.cos(radians) * scale;
  const sine = Math.sin(radians) * scale;
  return [cosine, sine, -sine, cosine, 0, 0];
}

function multiply(left: Matrix, right: Matrix): Matrix {
  return [
    left[0] * right[0] + left[2] * right[1],
    left[1] * right[0] + left[3] * right[1],
    left[0] * right[2] + left[2] * right[3],
    left[1] * right[2] + left[3] * right[3],
    left[0] * right[4] + left[2] * right[5] + left[4],
    left[1] * right[4] + left[3] * right[5] + left[5],
  ];
}

function transformBox(box: RenderableBox, matrix: Matrix): RenderableBox {
  const points = [
    transformPoint(box.x, box.y, matrix),
    transformPoint(box.x + box.width, box.y, matrix),
    transformPoint(box.x, box.y + box.height, matrix),
    transformPoint(box.x + box.width, box.y + box.height, matrix),
  ];
  const minX = Math.min(...points.map((point) => point.x));
  const minY = Math.min(...points.map((point) => point.y));
  const maxX = Math.max(...points.map((point) => point.x));
  const maxY = Math.max(...points.map((point) => point.y));
  return { x: minX, y: minY, width: maxX - minX, height: maxY - minY };
}

function transformPoint(x: number, y: number, matrix: Matrix) {
  return {
    x: matrix[0] * x + matrix[2] * y + matrix[4],
    y: matrix[1] * x + matrix[3] * y + matrix[5],
  };
}

function intersectBoxes(
  first: RenderableBox | undefined,
  second: RenderableBox,
): RenderableBox | undefined {
  if (!first) return second;
  const x = Math.max(first.x, second.x);
  const y = Math.max(first.y, second.y);
  const right = Math.min(first.x + first.width, second.x + second.width);
  const bottom = Math.min(first.y + first.height, second.y + second.height);
  return right > x && bottom > y
    ? { x, y, width: right - x, height: bottom - y }
    : undefined;
}

function screenBox(payload: DesignPreviewPayload): RenderableBox {
  return {
    x: payload.previewFrame.screenX,
    y: payload.previewFrame.screenY,
    width: payload.previewFrame.screenWidth,
    height: payload.previewFrame.screenHeight,
  };
}

function finiteNumber(value: unknown, key: string) {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  throw new Error(`Preview Device moduleTransparency.${key} must be numeric.`);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
