import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { CallParticipantDesignContract } from "./callParticipantComponentContract.js";
import { avatarComponentToRenderableAt } from "./avatarComponentRenderable.js";
import { boundedCenterBox, iconTokenStyle, numberToken, placeChild, renderScale, scalePlacement, selectedColor } from "./componentRenderableCommon.js";
import { labelComponentToRenderableAt, measureLabelComponent } from "./labelComponentRenderable.js";
import { mediaComponentToRenderableAt } from "./mediaComponentRenderable.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";

export function callParticipantComponentToRenderable(
  payload: DesignPreviewPayload,
  participant: CallParticipantDesignContract,
  assignedBox?: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const box = assignedBox ?? boundedCenterBox(payload, participant.size.width * scale, participant.size.height * scale);
  const insetX = numberToken(payload, participant.padding.xToken) * scale;
  const insetY = numberToken(payload, participant.padding.yToken) * scale;
  const content = { x: box.x + insetX, y: box.y + insetY, width: Math.max(1, box.width - insetX * 2), height: Math.max(1, box.height - insetY * 2) };
  const children: RenderableNode[] = [];
  if (participant.showBackground) children.push(surfaceComponentToRenderableAt(payload, participant.surface, box));
  if (participant.showMedia && participant.videoPresent) children.push(mediaComponentToRenderableAt(payload, participant.media, content));
  if (!participant.videoPresent && participant.showAvatarWhenVideoAbsent) {
    const size = Math.min(participant.avatar.size * scale, content.width, content.height);
    children.push(avatarComponentToRenderableAt(payload, participant.avatar, { x: content.x + (content.width - size) / 2, y: content.y + (content.height - size) / 2 - 20 * scale, width: size, height: size }));
  }
  if (!participant.videoPresent && participant.showStatusWhenVideoAbsent) children.push(placedLabel(payload, participant.statusLabel, content, participant.statusPlacement));
  if (participant.showName) children.push(placedLabel(payload, participant.nameLabel, content, participant.namePlacement));
  if (participant.showMicrophoneStatus) children.push(iconNode(payload, `${participant.id}.microphone`, participant.microphoneIconToken, content, participant.microphonePlacement, 22 * scale));
  if (participant.showConnectionStatus && participant.connectionIconToken) children.push(iconNode(payload, `${participant.id}.connection`, participant.connectionIconToken, content, participant.connectionPlacement, 22 * scale));
  if (participant.showActiveSpeakerIndicator && participant.speaking && participant.activeSpeakerBorderWidth > 0) {
    children.push({ id: `${participant.id}.activeSpeaker`, type: "surface", frame: 0, box, style: { background: "transparent", borderColor: selectedColor(payload, participant.activeSpeakerColorToken), borderWidth: participant.activeSpeakerBorderWidth * scale, borderRadius: numberToken(payload, participant.surface.surface.cornerRadiusToken) * scale } });
  }
  return { id: participant.id, type: "group", frame: 0, box, style: { overflow: "hidden" }, children };
}

function placedLabel(payload: DesignPreviewPayload, label: CallParticipantDesignContract["statusLabel"], parent: RenderableBox, placement: CallParticipantDesignContract["statusPlacement"]) {
  const size = measureLabelComponent(label, payload, { maximumWidth: parent.width });
  return labelComponentToRenderableAt(payload, label, placeChild(parent, size, scalePlacement(placement, renderScale(payload))));
}

function iconNode(payload: DesignPreviewPayload, id: string, token: string, parent: RenderableBox, placement: CallParticipantDesignContract["microphonePlacement"], size: number): RenderableNode {
  const box = placeChild(parent, { width: size, height: size }, scalePlacement(placement, renderScale(payload)));
  return { id, type: "icon", frame: 0, box, text: token, style: { ...iconTokenStyle(payload, token, selectedColor(payload, "theme.colors.textPrimary")), background: "rgba(0,0,0,0.45)", borderRadius: size / 2, padding: size * 0.18 } };
}
