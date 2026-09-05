import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { avatarComponentToRenderableAt } from "./avatarComponentRenderable.js";
import { embeddedComponentConfig } from "./componentPreviewDefaults.js";
import { componentClassToRenderable } from "./componentRenderableBoundary.js";
import { numberToken, placeChild, previewPayloadInBox, previewScreenBox, renderScale, scalePlacement, selectedColor } from "./componentRenderableCommon.js";
import { parseObject } from "./componentResolverCommon.js";
import { labelComponentToRenderableAt, measureLabelComponent } from "./labelComponentRenderable.js";
import { mediaComponentToRenderableAt } from "./mediaComponentRenderable.js";
import { alignedRowsOverlayNode, rowsSectionNode } from "./moduleRowSectionRenderable.js";
import { contentRowComponentToRenderable, contentRowHasRenderableContent } from "./contentRowComponentRenderable.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";
import { resolveVideoCallModule } from "./videoCallModuleResolver.js";
import type { VideoCallComponentSlot, VideoCallModuleContract, VideoCallParticipant } from "./videoCallModuleContract.js";
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
    rowHasContent: contentRowHasRenderableContent,
    renderRow: contentRowComponentToRenderable,
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
    rowHasContent: contentRowHasRenderableContent,
    renderRow: contentRowComponentToRenderable,
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
  const gap = numberToken(payload, call.gridGapToken) * scale;
  const layoutById = participantLayout(call, payload, body, gap);
  const pipContent = inset(payload, body, call.pipPadding);
  const pipBox = placeChild(pipContent, { width: call.pipSize.width * scale, height: call.pipSize.height * scale }, scalePlacement(call.pipPlacement, scale));
  const participants = call.participants.flatMap((item) => {
    const box = item.role === "pip" && call.showPip ? pipBox : layoutById.get(item.id);
    return box ? [participantNode(previewPayloadInBox(payload, box), bases, call, item, box)] : [];
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

function participantNode(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  call: VideoCallModuleContract,
  participant: VideoCallParticipant,
  box: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const content = inset(payload, box, participant.padding);
  const children: RenderableNode[] = [];
  if (participant.showSurface) children.push(surfaceComponentToRenderableAt(payload, participant.surface, box));
  if (participant.showMedia && participant.videoPresent) children.push(mediaComponentToRenderableAt(payload, participant.media, content));
  if (!participant.videoPresent) {
    const avatarSize = participant.showFallbackAvatar
      ? Math.min(participant.avatarSize * scale, content.width, content.height)
      : 0;
    const statusSize = participant.showFallbackStatus
      ? measureLabelComponent(participant.statusLabel, payload, { maximumWidth: content.width })
      : { width: 0, height: 0 };
    const gap = avatarSize > 0 && statusSize.height > 0
      ? numberToken(payload, participant.padding.yToken) * scale
      : 0;
    const fallbackHeight = avatarSize + gap + statusSize.height;
    const fallbackY = content.y + (content.height - fallbackHeight) * 0.5;
    if (avatarSize > 0) children.push(avatarComponentToRenderableAt(payload, participant.avatar, {
      x: content.x + (content.width - avatarSize) * 0.5,
      y: fallbackY,
      width: avatarSize,
      height: avatarSize,
    }));
    if (statusSize.height > 0) children.push(labelComponentToRenderableAt(payload, participant.statusLabel, {
      x: content.x + (content.width - statusSize.width) * 0.5,
      y: fallbackY + avatarSize + gap,
      width: statusSize.width,
      height: statusSize.height,
    }));
  }
  if (participant.role === "main") children.push(alignedRowsOverlayNode(payload, componentBaseConfigs, {
    ownerId: `${call.id}.${participant.id}`,
    section: "mainRows",
    rows: call.mainRows,
    box,
    rowHasContent: contentRowHasRenderableContent,
    renderRow: contentRowComponentToRenderable,
  }));
  return { id: participant.id, type: "group", frame: 0, box, style: { overflow: "hidden" }, children };
}

function participantLayout(call: ReturnType<typeof resolveVideoCallModule>, payload: DesignPreviewPayload, body: RenderableBox, gap: number) {
  if (call.gridHeightMode === "fill") {
    const items = call.participants.filter((item) =>
      (item.role === "main" && call.showMainVideo)
      || (item.role === "grid" && call.showGridParticipants));
    return new Map(grid(items, inset(payload, body, call.gridPadding), gap, call.gridRows).map(({ item, box }) => [item.id, box]));
  }

  const gridItems = call.showGridParticipants
    ? call.participants.filter((item) => item.role === "grid")
    : [];
  const gridHeight = Math.min(body.height, call.gridHeight * renderScale(payload));
  const gridRegion: RenderableBox = {
    x: body.x,
    y: body.y + body.height - gridHeight,
    width: body.width,
    height: gridHeight,
  };
  const mainRegion: RenderableBox = gridItems.length > 0
    ? { x: body.x, y: body.y, width: body.width, height: Math.max(1, gridRegion.y - gap - body.y) }
    : body;
  const byId = new Map<string, RenderableBox>();
  if (call.showMainVideo) {
    const mainBox = inset(payload, mainRegion, call.mainPadding);
    for (const item of call.participants) if (item.role === "main") byId.set(item.id, mainBox);
  }
  for (const { item, box } of grid(gridItems, inset(payload, gridRegion, call.gridPadding), gap, call.gridRows)) byId.set(item.id, box);
  return byId;
}

function grid(items: VideoCallParticipant[], box: RenderableBox, gap: number, requestedRows: number) {
  if (items.length === 0) return [];
  const rows = Math.max(1, Math.min(requestedRows, items.length));
  const columns = Math.ceil(items.length / rows);
  const height = Math.max(1, (box.height - gap * (rows - 1)) / rows);
  const placed: Array<{ item: VideoCallParticipant; box: RenderableBox }> = [];
  let itemIndex = 0;
  for (let row = 0; row < rows; row += 1) {
    const count = Math.min(columns, items.length - itemIndex);
    const width = Math.max(1, (box.width - gap * (count - 1)) / count);
    for (let column = 0; column < count; column += 1) {
      placed.push({
        item: items[itemIndex]!,
        box: {
          x: box.x + column * (width + gap),
          y: box.y + row * (height + gap),
          width,
          height,
        },
      });
      itemIndex += 1;
    }
  }
  return placed;
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
