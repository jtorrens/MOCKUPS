import type { RenderableNode } from "../visual/renderable/types.js";

export function extractRootOverlays(node: RenderableNode): {
  node: RenderableNode;
  overlays: RenderableNode[];
} {
  const overlays: RenderableNode[] = [];

  function visit(current: RenderableNode): RenderableNode | undefined {
    if (current.style?.rootOverlay === true) {
      overlays.push(stripRootOverlayMarker(current));
      return undefined;
    }

    const nextChildren = current.children
      ?.map(visit)
      .filter((child): child is RenderableNode => child !== undefined);
    return {
      ...current,
      children: nextChildren,
    };
  }

  return {
    node: visit(node) ?? emptyGroupNode(node.id),
    overlays,
  };
}

function stripRootOverlayMarker(node: RenderableNode): RenderableNode {
  const { rootOverlay, ...style } = node.style ?? {};
  void rootOverlay;
  return {
    ...node,
    style,
  };
}

function emptyGroupNode(id: string): RenderableNode {
  return {
    id: `${id}.empty`,
    type: "group",
    frame: 0,
    style: {
      overflow: "visible",
    },
  };
}
