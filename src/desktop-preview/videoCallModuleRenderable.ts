import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { embeddedComponentConfig } from "./componentPreviewDefaults.js";
import { componentClassToRenderable } from "./componentRenderableBoundary.js";
import { numberToken, placeChild, previewPayloadInBox, previewScreenBox, renderScale, scalePlacement, selectedColor } from "./componentRenderableCommon.js";
import { parseObject } from "./componentResolverCommon.js";
import { callParticipantComponentToRenderable } from "./callParticipantComponentRenderable.js";
import { rowsSectionNode } from "./moduleRowSectionRenderable.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";
import { resolveVideoCallModule } from "./videoCallModuleResolver.js";
import type { VideoCallComponentSlot, VideoCallParticipant } from "./videoCallModuleContract.js";
import { wallpaperRenderable } from "./wallpaperRenderable.js";

export function videoCallModuleToRenderable(payload: DesignPreviewPayload): RenderableNode {
  const call = resolveVideoCallModule(payload);
  const scale = renderScale(payload);
  const screen = previewScreenBox(payload);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const status = call.showStatusBar ? chrome(payload, bases, "status_bar", call.statusBarSlot) : undefined;
  const navigation = call.showNavigationBar ? chrome(payload, bases, "navigation_bar", call.navigationBarSlot) : undefined;
  const contentTop = screen.y + (status?.box?.height ?? 0);
  const contentBottom = screen.y + screen.height - (navigation?.box?.height ?? 0);
  const headerFloats = call.headerLayoutMode === "float";
  const footerFloats = call.footerLayoutMode === "float";
  const header = call.showHeader ? rowsSectionNode(payload, bases, {
    ownerId: call.id, section: "header", rows: call.headerRows, rowGapToken: call.headerRowGapToken,
    height: call.headerHeight,
    renderSurface: (box) => sectionSurfaceNode(payload, bases, call.headerSurfaceSlot, "module.core.videoCall.headerSurfaceSlot", box),
    edge: "top", contentEdge: contentTop,
    horizontalInset: headerFloats ? numberToken(payload, call.headerFloatHorizontalPaddingToken) * scale : 0,
    edgeOffset: headerFloats ? call.headerFloatOffsetY * scale : 0,
    bleedToScreenEdge: !headerFloats,
    contentAlignment: "center",
  }) : undefined;
  const footer = call.showFooter ? rowsSectionNode(payload, bases, {
    ownerId: call.id, section: "footer", rows: call.footerRows, rowGapToken: call.footerRowGapToken,
    height: call.footerHeight,
    renderSurface: (box) => sectionSurfaceNode(payload, bases, call.footerSurfaceSlot, "module.core.videoCall.footerSurfaceSlot", box),
    edge: "bottom", contentEdge: contentBottom,
    horizontalInset: footerFloats ? numberToken(payload, call.footerFloatHorizontalPaddingToken) * scale : 0,
    edgeOffset: footerFloats ? call.footerFloatOffsetY * scale : 0,
    bleedToScreenEdge: !footerFloats,
    contentAlignment: "center",
  }) : undefined;
  const bodyTop = header?.box && !headerFloats
    ? header.box.y + header.box.height
    : contentTop;
  const bodyBottom = footer?.box && !footerFloats
    ? footer.box.y
    : contentBottom;
  const body: RenderableBox = {
    x: screen.x,
    y: bodyTop,
    width: screen.width,
    height: Math.max(1, bodyBottom - bodyTop),
  };
  const gridItems = call.participants.filter((item) => item.role === "grid");
  const gridBoxes = grid(gridItems, inset(payload, body, call.gridPadding), numberToken(payload, call.gridGapToken) * scale, call.gridColumns);
  const gridById = new Map(gridBoxes.map(({ item, box }) => [item.id, box]));
  const mainContent = inset(payload, body, call.mainPadding);
  const mainBox = call.mainSizeMode === "fill"
    ? mainContent
    : placeChild(mainContent, { width: call.mainSize.width * scale, height: call.mainSize.height * scale }, scalePlacement(call.mainPlacement, scale));
  const pipContent = inset(payload, body, call.pipPadding);
  const pipBox = placeChild(pipContent, { width: call.pipSize.width * scale, height: call.pipSize.height * scale }, scalePlacement(call.pipPlacement, scale));
  const participants = call.participants.flatMap((item) => {
    const box = item.role === "main" && call.showMainVideo ? mainBox
      : item.role === "pip" && call.showPip ? pipBox
      : item.role === "grid" && call.showGridParticipants ? gridById.get(item.id)
      : undefined;
    return box ? [callParticipantComponentToRenderable(previewPayloadInBox(payload, box), item.participant, box)] : [];
  });
  const children: RenderableNode[] = [
    call.useAppWallpaper
      ? wallpaperRenderable(payload, screen) ?? background(payload, screen, call.backgroundColorToken)
      : background(payload, screen, call.backgroundColorToken),
    ...participants,
  ];
  if (header) children.push(header);
  if (footer) children.push(footer);
  if (status) children.push(status);
  if (navigation) children.push(navigation);
  return { id: call.id, type: "group", frame: 0, box: screen, style: { overflow: "hidden" }, children };
}

function grid(items: VideoCallParticipant[], box: RenderableBox, gap: number, requestedColumns: number) {
  if (items.length === 0) return [];
  const columns = Math.max(1, Math.min(requestedColumns, items.length));
  const rows = Math.ceil(items.length / columns);
  const width = Math.max(1, (box.width - gap * (columns - 1)) / columns);
  const height = Math.max(1, (box.height - gap * (rows - 1)) / rows);
  return items.map((item, index) => ({ item, box: { x: box.x + (index % columns) * (width + gap), y: box.y + Math.floor(index / columns) * (height + gap), width, height } }));
}

function inset(payload: DesignPreviewPayload, box: RenderableBox, padding: { xToken: string; yToken: string }): RenderableBox {
  const scale = renderScale(payload);
  const x = numberToken(payload, padding.xToken) * scale;
  const y = numberToken(payload, padding.yToken) * scale;
  return { x: box.x + x, y: box.y + y, width: Math.max(1, box.width - x * 2), height: Math.max(1, box.height - y * 2) };
}
function chrome(payload: DesignPreviewPayload, bases: Record<string, unknown>, type: string, slot: VideoCallComponentSlot) { return componentClassToRenderable({ ...payload, componentType: type, configJson: JSON.stringify(embeddedComponentConfig(bases, slot, type, `module.core.videoCall.${type}`)), designPreviewJson: "{}" }); }
function sectionSurfaceNode(payload: DesignPreviewPayload, bases: Record<string, unknown>, slot: VideoCallComponentSlot, owner: string, box: RenderableBox) {
  const scale = renderScale(payload);
  const surface = resolveSurfaceComponentAtSize(
    embeddedComponentConfig(bases, slot, "surface", owner),
    { width: box.width / scale, height: box.height / scale },
    owner,
  );
  return surfaceComponentToRenderableAt(payload, surface, box);
}
function background(payload: DesignPreviewPayload, box: RenderableBox, token: string): RenderableNode { return { id: "module.core.videoCall.background", type: "surface", frame: 0, box, style: { background: selectedColor(payload, token) }, metadata: { paintRole: "moduleBackground" } }; }
