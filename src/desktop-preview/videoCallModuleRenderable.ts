import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { embeddedComponentConfig } from "./componentPreviewDefaults.js";
import { componentClassToRenderable } from "./componentRenderableBoundary.js";
import { numberToken, placeChild, previewPayloadInBox, previewScreenBox, renderScale, scalePlacement, selectedColor } from "./componentRenderableCommon.js";
import { parseObject } from "./componentResolverCommon.js";
import { buttonComponentToRenderableAt } from "./buttonComponentRenderable.js";
import { callParticipantComponentToRenderable } from "./callParticipantComponentRenderable.js";
import { labelComponentToRenderableAt, measureLabelComponent } from "./labelComponentRenderable.js";
import { resolveVideoCallModule } from "./videoCallModuleResolver.js";
import type { VideoCallComponentSlot, VideoCallParticipant } from "./videoCallModuleContract.js";
import { wallpaperRenderable } from "./wallpaperRenderable.js";

export function videoCallModuleToRenderable(
  payload: DesignPreviewPayload,
): RenderableNode {
  const call = resolveVideoCallModule(payload);
  const scale = renderScale(payload);
  const screen = previewScreenBox(payload);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const status = call.visibility.showStatusBar ? chrome(payload, bases, "status_bar", call.statusBarSlot) : undefined;
  const navigation = call.visibility.showNavigationBar ? chrome(payload, bases, "navigation_bar", call.navigationBarSlot) : undefined;
  const statusHeight = status?.box?.height ?? 0;
  const navigationTop = navigation?.box?.y ?? screen.y + screen.height;
  const headerHeight = call.visibility.showHeader ? call.headerHeight * scale : 0;
  const controlsHeight = call.visibility.showControls ? call.controlsHeight * scale : 0;
  const padX = numberToken(payload, call.screenPadding.xToken) * scale;
  const padY = numberToken(payload, call.screenPadding.yToken) * scale;
  const content: RenderableBox = {
    x: screen.x + padX,
    y: screen.y + statusHeight + headerHeight + padY,
    width: Math.max(1, screen.width - padX * 2),
    height: Math.max(1, navigationTop - controlsHeight - padY - (screen.y + statusHeight + headerHeight + padY)),
  };
  const self = call.participants.find(item => item.isSelf);
  const main = call.visibility.showSelfView ? call.participants.filter(item => !item.isSelf) : call.participants;
  const participantBoxes = layoutParticipants(main, content, numberToken(payload, call.participantGapToken) * scale, call.layoutMode, call.activeParticipantId);
  const participantNodes = participantBoxes.map(({ item, box }) => callParticipantComponentToRenderable(previewPayloadInBox(payload, box), item.participant, box));
  if (call.visibility.showSelfView && self) {
    const requested = { width: call.selfViewSize.width * scale, height: call.selfViewSize.height * scale };
    const box = placeChild(content, requested, scalePlacement(call.selfViewPlacement, scale));
    participantNodes.push(callParticipantComponentToRenderable(previewPayloadInBox(payload, box), self.participant, box));
  }
  const children: RenderableNode[] = [
    call.useAppWallpaper ? wallpaperRenderable(payload, screen) ?? background(payload, screen, call.backgroundColorToken) : background(payload, screen, call.backgroundColorToken),
    ...participantNodes,
  ];
  if (call.visibility.showHeader) children.push(...headerNodes(payload, call, { x: screen.x, y: screen.y + statusHeight, width: screen.width, height: headerHeight }));
  if (call.visibility.showControls) children.push(...controlNodes(payload, call, { x: screen.x, y: navigationTop - controlsHeight, width: screen.width, height: controlsHeight }));
  if (status) children.push(status);
  if (navigation) children.push(navigation);
  return { id: call.id, type: "group", frame: 0, box: screen, style: { overflow: "hidden" }, children };
}

function layoutParticipants(items: VideoCallParticipant[], box: RenderableBox, gap: number, mode: "automatic" | "spotlight" | "grid", activeId: string) {
  if (items.length === 0) return [];
  if (items.length === 1) return [{ item: items[0]!, box }];
  const spotlight = mode === "spotlight" || (mode === "automatic" && items.length > 2 && activeId.length > 0);
  if (spotlight) {
    const activeIndex = items.findIndex(item => item.id === activeId);
    if (activeIndex < 0) throw new Error(`Active participant '${activeId}' must reference a present non-self participant`);
    const active = items[activeIndex]!;
    const others = items.filter((_, index) => index !== activeIndex);
    const topHeight = Math.max(1, box.height * 0.6 - gap / 2);
    const bottom = { x: box.x, y: box.y + topHeight + gap, width: box.width, height: Math.max(1, box.height - topHeight - gap) };
    return [{ item: active, box: { x: box.x, y: box.y, width: box.width, height: topHeight } }, ...grid(others, bottom, gap)];
  }
  return grid(items, box, gap);
}

function grid(items: VideoCallParticipant[], box: RenderableBox, gap: number) {
  const columns = items.length === 1 ? 1 : 2;
  const rows = Math.ceil(items.length / columns);
  const width = Math.max(1, (box.width - gap * (columns - 1)) / columns);
  const height = Math.max(1, (box.height - gap * (rows - 1)) / rows);
  return items.map((item, index) => ({ item, box: { x: box.x + (index % columns) * (width + gap), y: box.y + Math.floor(index / columns) * (height + gap), width, height } }));
}

function headerNodes(payload: DesignPreviewPayload, call: ReturnType<typeof resolveVideoCallModule>, box: RenderableBox) {
  const nodes: RenderableNode[] = [];
  if (call.backButton) nodes.push(buttonComponentToRenderableAt(payload, call.backButton, { x: box.x + 12, y: box.y + (box.height - 44) / 2, width: 44, height: 44 }));
  if (call.addButton) nodes.push(buttonComponentToRenderableAt(payload, call.addButton, { x: box.x + box.width - 56, y: box.y + (box.height - 44) / 2, width: 44, height: 44 }));
  const centerX = box.x + box.width / 2;
  if (call.visibility.showCallTitle) { const size = measureLabelComponent(call.title, payload); nodes.push(labelComponentToRenderableAt(payload, call.title, { x: centerX - size.width / 2, y: box.y + 10, width: size.width, height: size.height })); }
  const meta = [call.visibility.showParticipantCount ? call.participantCount : undefined, call.visibility.showDuration ? call.duration : undefined].filter(Boolean) as typeof call.participantCount[];
  let x = centerX - meta.reduce((sum, label) => sum + measureLabelComponent(label, payload).width, 0) / 2 - Math.max(0, meta.length - 1) * 6;
  for (const label of meta) { const size = measureLabelComponent(label, payload); nodes.push(labelComponentToRenderableAt(payload, label, { x, y: box.y + box.height - size.height - 8, width: size.width, height: size.height })); x += size.width + 12; }
  return nodes;
}

function controlNodes(payload: DesignPreviewPayload, call: ReturnType<typeof resolveVideoCallModule>, box: RenderableBox) {
  const gap = 12 * renderScale(payload);
  const sizes = call.controls.map(control => ({ control, size: control.button.size.width * renderScale(payload) }));
  const total = sizes.reduce((sum, item) => sum + item.size, 0) + gap * Math.max(0, sizes.length - 1);
  let x = box.x + (box.width - total) / 2;
  return sizes.map(({ control, size }) => { const node = buttonComponentToRenderableAt(payload, control.button, { x, y: box.y + (box.height - size) / 2, width: size, height: size }); x += size + gap; return node; });
}

function chrome(payload: DesignPreviewPayload, bases: Record<string, unknown>, type: string, slot: VideoCallComponentSlot) {
  return componentClassToRenderable({ ...payload, componentType: type, configJson: JSON.stringify(embeddedComponentConfig(bases, slot, type, `module.core.videoCall.${type}`)), designPreviewJson: "{}" });
}

function background(payload: DesignPreviewPayload, box: RenderableBox, token: string): RenderableNode { return { id: "module.core.videoCall.background", type: "surface", frame: 0, box, style: { background: selectedColor(payload, token) }, metadata: { paintRole: "moduleBackground" } }; }
