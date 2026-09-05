import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { numberToken, previewScreenBox } from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { ModuleRow } from "./moduleRowSectionContract.js";
import { renderScale, translateRenderableNode } from "./previewGeometryHelpers.js";

interface RenderedRow { node: RenderableNode; height: number }

export function rowsSectionNode<TContent>(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  options: {
    ownerId: string; section: string; rows: [ModuleRow<TContent>, ModuleRow<TContent>]; rowGapToken: string;
    height: number; renderSurface: (box: RenderableBox) => RenderableNode; edge: "top" | "bottom";
    contentEdge: number; horizontalInset?: number; edgeOffset?: number; bleedToScreenEdge?: boolean;
    contentAlignment?: "bottom" | "center";
    rowHasContent: (content: TContent) => boolean;
    renderRow: (payload: DesignPreviewPayload, content: TContent, box: RenderableBox) => RenderableNode;
  },
): RenderableNode {
  const screen = previewScreenBox(payload);
  const scale = renderScale(payload);
  const horizontalInset = Math.max(0, options.horizontalInset ?? 0);
  const sectionX = screen.x + horizontalInset;
  const sectionWidth = Math.max(1, screen.width - horizontalInset * 2);
  const gap = numberToken(payload, options.rowGapToken) * scale;
  const renderedRows = options.rows
    .filter((row) => row.visible && options.rowHasContent(row.content))
    .map((row) => renderRow(payload, componentBaseConfigs, options.ownerId, options.section, row, 0, sectionX, sectionWidth, options.renderRow));
  const rowsHeight = renderedRows.reduce((height, row) => height + row.height, 0)
    + Math.max(0, renderedRows.length - 1) * gap;
  const sectionHeight = Math.max(options.height * scale, rowsHeight);
  const edgeOffset = Math.max(0, options.edgeOffset ?? 0);
  const sectionY = options.edge === "top" ? options.contentEdge + edgeOffset : options.contentEdge - sectionHeight - edgeOffset;
  const rowsY = options.contentAlignment === "bottom" ? sectionY + sectionHeight - rowsHeight : sectionY + (sectionHeight - rowsHeight) * 0.5;
  let nextRowY = rowsY;
  const rowNodes = renderedRows.map((row) => {
    const node = translateRenderableNode(row.node, { x: 0, y: nextRowY });
    nextRowY += row.height + gap;
    return node;
  });
  const surfaceBox = options.bleedToScreenEdge === false
    ? { x: sectionX, y: sectionY, width: sectionWidth, height: sectionHeight }
    : { x: screen.x, y: options.edge === "top" ? screen.y : sectionY, width: screen.width, height: options.edge === "top" ? Math.max(0, sectionY + sectionHeight - screen.y) : Math.max(0, screen.y + screen.height - sectionY) };
  return {
    id: `${options.ownerId}.${options.section}`, type: "group", frame: 0,
    box: { x: sectionX, y: sectionY, width: sectionWidth, height: sectionHeight }, style: { overflow: "visible" },
    children: [options.renderSurface(surfaceBox), ...rowNodes],
  };
}

export function alignedRowsOverlayNode<TContent>(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  options: {
    ownerId: string;
    section: string;
    rows: [ModuleRow<TContent>, ModuleRow<TContent>, ModuleRow<TContent>];
    box: RenderableBox;
    rowHasContent: (content: TContent) => boolean;
    renderRow: (payload: DesignPreviewPayload, content: TContent, box: RenderableBox) => RenderableNode;
  },
): RenderableNode {
  const placements = ["top", "center", "bottom"] as const;
  const rendered = options.rows.flatMap((row, index) => row.visible && options.rowHasContent(row.content)
    ? [{ row: renderRow(payload, componentBaseConfigs, options.ownerId, options.section, row, 0, options.box.x, options.box.width, options.renderRow), placement: placements[index] }]
    : []);
  return {
    id: `${options.ownerId}.${options.section}`, type: "group", frame: 0, box: options.box, style: { overflow: "visible" },
    children: rendered.map(({ row, placement }) => {
      const y = placement === "top" ? options.box.y : placement === "center" ? options.box.y + (options.box.height - row.height) * 0.5 : options.box.y + options.box.height - row.height;
      return translateRenderableNode(row.node, { x: 0, y });
    }),
  };
}

function renderRow<TContent>(
  payload: DesignPreviewPayload,
  _componentBaseConfigs: Record<string, unknown>,
  ownerId: string,
  section: string,
  row: ModuleRow<TContent>,
  y: number,
  sectionX: number,
  sectionWidth: number,
  render: (payload: DesignPreviewPayload, content: TContent, box: RenderableBox) => RenderableNode,
): RenderedRow {
  const node = render(payload, row.content, { x: sectionX, y, width: sectionWidth, height: 1 });
  return { node: { ...node, id: `${ownerId}.${section}.${row.id}` }, height: node.box?.height ?? Math.max(1, renderScale(payload)) };
}
