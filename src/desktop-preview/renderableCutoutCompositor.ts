import type { RenderableBox, RenderableNode, RenderableOpacityMask } from "../visual/renderable/types.js";

/** Destination-out geometry erases preceding paint, including ancestor backgrounds.
 * Later siblings remain above the hole. Both adapters consume this same paint plan. */
export function compositeRenderableCutouts(root: RenderableNode): RenderableNode {
  if (!containsCutout(root)) return root;
  if (!root.box) throw new Error("Cutout compositing requires a boxed root");
  let serial = 0;
  function masked(nodes: RenderableNode[], stencils: RenderableNode[], box: RenderableBox): RenderableNode {
    return {
      id: `composite.mask.${serial++}`, type: "group", box,
      style: { compositeMaskImage: cutoutMaskImage(stencils, box) }, children: nodes,
    };
  }
  function visit(node: RenderableNode, extent: RenderableBox): { paint?: RenderableNode; stencil?: RenderableNode } {
    if (node.type === "cutout") return { stencil: node };
    if (!containsCutout(node)) return { paint: node };
    const localExtent = inverseExtent(extent, node);
    const ordered = [...(node.children ?? [])].sort((a, b) =>
      Number(a.style?.zIndex ?? 0) - Number(b.style?.zIndex ?? 0));
    const stencils: RenderableNode[] = [];
    const style = node.style ?? {};
    const { opacity, opacityMask, overflow, borderRadius, cornerRadius, zIndex, ...paintStyle } = style;
    const underlay: RenderableNode = {
      ...node, type: node.type === "group" ? "surface" : node.type,
      id: `${node.id}.paint`, transform: undefined, children: undefined,
      metadata: undefined, style: { ...paintStyle, borderRadius, cornerRadius },
    };
    let layers: RenderableNode[] = [];
    for (const child of ordered) {
      const result = visit(child, localExtent);
      if (result.stencil) {
        if (layers.length) layers = [masked(layers, [result.stencil], localExtent)];
        stencils.push(result.stencil);
      }
      if (result.paint) layers.push(result.paint);
    }
    const containerStyle = { opacity, opacityMask, overflow, borderRadius, cornerRadius, zIndex };
    const localStencil: RenderableNode = { id: `${node.id}.stencil`, type: "group", box: node.box,
      style: { overflow, borderRadius, cornerRadius }, children: stencils };
    return {
      paint: { ...node, type: "group", text: undefined, asset: undefined,
        style: { opacity, opacityMask, zIndex,
          color: style.color, fontFamily: style.fontFamily, fontSize: style.fontSize,
          fontWeight: style.fontWeight, fontStyle: style.fontStyle,
          lineHeight: style.lineHeight, textAlign: style.textAlign, textShadow: style.textShadow,
        }, children: [
          masked([underlay], [localStencil], localExtent),
          { id: `${node.id}.children`, type: "group", box: node.box,
            style: { overflow, borderRadius, cornerRadius }, children: layers },
        ] },
      stencil: { id: node.id, type: "group", box: node.box, transform: node.transform, style: containerStyle, children: stencils },
    };
  }
  return visit(root, root.box).paint ?? { ...root, type: "group", children: [] };
}

function containsCutout(node: RenderableNode): boolean {
  return node.type === "cutout" || (node.children?.some(containsCutout) ?? false);
}

function inverseExtent(box: RenderableBox, node: RenderableNode): RenderableBox {
  const t = node.transform;
  const scale = t?.scale ?? 1;
  if (scale === 0) return box;
  const cx = (node.box?.x ?? 0) + (node.box?.width ?? 0) / 2;
  const cy = (node.box?.y ?? 0) + (node.box?.height ?? 0) / 2;
  const angle = -(t?.rotation ?? 0) * Math.PI / 180;
  const points = [[box.x, box.y], [box.x + box.width, box.y],
    [box.x, box.y + box.height], [box.x + box.width, box.y + box.height]].map(([x, y]) => {
    const dx = x! - (t?.x ?? 0) - cx;
    const dy = y! - (t?.y ?? 0) - cy;
    return { x: cx + (dx * Math.cos(angle) - dy * Math.sin(angle)) / scale,
      y: cy + (dx * Math.sin(angle) + dy * Math.cos(angle)) / scale };
  });
  const x = Math.min(...points.map(p => p.x)), y = Math.min(...points.map(p => p.y));
  return { x, y, width: Math.max(...points.map(p => p.x)) - x,
    height: Math.max(...points.map(p => p.y)) - y };
}

function cutoutMaskImage(nodes: RenderableNode[], box: RenderableBox): string {
  let serial = 0;
  const rect = (b: RenderableBox, radius = 0) =>
    `<rect x="${b.x}" y="${b.y}" width="${b.width}" height="${b.height}" rx="${radius}"/>`;
  function stencil(node: RenderableNode): string {
    const b = node.box;
    const style = node.style ?? {};
    const radius = Number(style.borderRadius ?? style.cornerRadius ?? 0);
    let paint = node.type === "cutout" && b ? rect(b, radius) : (node.children ?? []).map(stencil).join("");
    if (b && style.overflow === "hidden") {
      const id = `clip${serial++}`;
      paint = `<defs><clipPath id="${id}">${rect(b, radius)}</clipPath></defs><g clip-path="url(#${id})">${paint}</g>`;
    }
    if (b && style.opacityMask) {
      const m = style.opacityMask as RenderableOpacityMask;
      const id = `fade${serial++}`;
      const axis = m.axis === "horizontal" ? `x1="${m.start}" x2="${m.end}" y1="0" y2="0"` : `y1="${m.start}" y2="${m.end}" x1="0" x2="0"`;
      paint = `<defs><linearGradient id="${id}" gradientUnits="userSpaceOnUse" ${axis}><stop stop-color="white" offset="0" stop-opacity="${m.beforeOpacity}"/><stop stop-color="white" offset="1" stop-opacity="${m.afterOpacity}"/></linearGradient><mask id="${id}m" maskUnits="userSpaceOnUse" x="${b.x}" y="${b.y}" width="${b.width}" height="${b.height}"><g fill="url(#${id})">${rect(b)}</g></mask></defs><g mask="url(#${id}m)">${paint}</g>`;
    }
    const t = node.transform;
    const cx = (b?.x ?? 0) + (b?.width ?? 0) / 2, cy = (b?.y ?? 0) + (b?.height ?? 0) / 2;
    return `<g opacity="${t?.opacity ?? style.opacity ?? 1}" transform="translate(${t?.x ?? 0} ${t?.y ?? 0}) translate(${cx} ${cy}) rotate(${t?.rotation ?? 0}) scale(${t?.scale ?? 1}) translate(${-cx} ${-cy})">${paint}</g>`;
  }
  const geometry = nodes.map(stencil).join("");
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${box.width}" height="${box.height}" viewBox="${box.x} ${box.y} ${box.width} ${box.height}"><defs><mask id="erase" maskUnits="userSpaceOnUse" x="${box.x}" y="${box.y}" width="${box.width}" height="${box.height}" style="mask-type:luminance"><g fill="white">${rect(box)}</g><g fill="black">${geometry}</g></mask></defs><g fill="white" mask="url(#erase)">${rect(box)}</g></svg>`;
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
}
