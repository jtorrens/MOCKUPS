import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { componentVariantConfig, mergeComponentDefaults, requireComponentVariantType } from "./componentPreviewDefaults.js";
import { parseObject, requiredBoolean, requiredNumber, requiredNumberPair, requiredPlacement, requiredPossiblyEmptyString, requiredRecord, requiredString, requiredStringPair } from "./componentResolverCommon.js";
import { requiredComponentBoundaryMotion } from "./componentBoundaryMotion.js";
import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import { literalLabelPreview, resolveLabelComponentFromRecords } from "./labelComponentResolver.js";
import { resolveMediaComponentFromRecords } from "./mediaComponentResolver.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";
import type { CallParticipantConnectionState, CallParticipantDesignContract, CallParticipantMicrophoneState } from "./callParticipantComponentContract.js";

export function resolveCallParticipantComponent(
  payload: DesignPreviewPayload,
): CallParticipantDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const owner = requiredRecord(config, "callParticipant", "component.callParticipant");
  const rawSize = requiredNumberPair(preview, "viewportSize", "component.callParticipant.runtime.viewportSize");
  const size = { width: positive(rawSize.first, "width"), height: positive(rawSize.second, "height") };
  const padding = requiredStringPair(owner, "tilePadding", "component.callParticipant.tilePadding");
  const videoPresent = requiredBoolean(preview, "videoPresent", "component.callParticipant.runtime.videoPresent");
  const microphoneState = microphone(requiredString(preview, "microphoneState", "component.callParticipant.runtime.microphoneState"));
  const connectionState = connection(requiredString(preview, "connectionState", "component.callParticipant.runtime.connectionState"));
  const actor = requiredRecord(preview, "actor", "component.callParticipant.runtime.actor");
  const statusOverride = requiredPossiblyEmptyString(preview, "statusTextOverride", "component.callParticipant.runtime.statusTextOverride");
  const effectiveStatus = statusOverride.trim() || requiredString(owner, "defaultStatusText", "component.callParticipant.defaultStatusText");
  const surfaceConfig = slotConfig(bases, owner, "surfaceSlot", "surface");
  const mediaConfig = slotConfig(bases, owner, "mediaSlot", "media");
  const avatarConfig = slotConfig(bases, owner, "avatarSlot", "avatar");
  const statusLabelConfig = slotConfig(bases, owner, "statusLabelSlot", "label");
  const nameLabelConfig = slotConfig(bases, owner, "nameLabelSlot", "label");
  const avatarOwner = requiredRecord(avatarConfig, "avatar", "component.callParticipant.avatar");
  avatarOwner.defaultSize = requiredNumber(owner, "avatarSize", "component.callParticipant.avatarSize");
  const actorName = requiredString(actor, "displayName", "component.callParticipant.runtime.actor.displayName");

  return {
    id: "component.callParticipant",
    size,
    padding: { xToken: padding.first, yToken: padding.second },
    boundaryMotion: requiredComponentBoundaryMotion(config, "component.callParticipant"),
    showBackground: requiredBoolean(owner, "showBackground", "component.callParticipant.showBackground"),
    showMedia: requiredBoolean(owner, "showMedia", "component.callParticipant.showMedia"),
    showAvatarWhenVideoAbsent: requiredBoolean(owner, "showAvatarWhenVideoAbsent", "component.callParticipant.showAvatarWhenVideoAbsent"),
    showStatusWhenVideoAbsent: requiredBoolean(owner, "showStatusWhenVideoAbsent", "component.callParticipant.showStatusWhenVideoAbsent"),
    showName: requiredBoolean(owner, "showName", "component.callParticipant.showName"),
    showMicrophoneStatus: requiredBoolean(owner, "showMicrophoneStatus", "component.callParticipant.showMicrophoneStatus"),
    showConnectionStatus: requiredBoolean(owner, "showConnectionStatus", "component.callParticipant.showConnectionStatus"),
    showActiveSpeakerIndicator: requiredBoolean(owner, "showActiveSpeakerIndicator", "component.callParticipant.showActiveSpeakerIndicator"),
    videoPresent,
    microphoneState,
    connectionState,
    speaking: requiredBoolean(preview, "speaking", "component.callParticipant.runtime.speaking"),
    activeSpeakerColorToken: requiredString(owner, "activeSpeakerColorToken", "component.callParticipant.activeSpeakerColorToken"),
    activeSpeakerBorderWidth: Math.max(0, requiredNumber(owner, "activeSpeakerBorderWidth", "component.callParticipant.activeSpeakerBorderWidth")),
    microphoneIconToken: requiredString(owner, microphoneState === "muted" ? "microphoneMutedIconToken" : "microphoneOnIconToken", "component.callParticipant.microphoneIconToken"),
    connectionIconToken: connectionState === "weak"
      ? requiredString(owner, "connectionWeakIconToken", "component.callParticipant.connectionWeakIconToken")
      : connectionState === "lost" ? requiredString(owner, "connectionLostIconToken", "component.callParticipant.connectionLostIconToken") : undefined,
    surface: resolveSurfaceComponentAtSize(surfaceConfig, size, "component.callParticipant.surface"),
    media: resolveMediaComponentFromRecords(payload, mediaConfig, {
      mediaType: requiredString(preview, "mediaType", "component.callParticipant.runtime.mediaType"), mediaSource: requiredPossiblyEmptyString(preview, "mediaSource", "component.callParticipant.runtime.mediaSource"),
      viewportSize: `${size.width}|${size.height}`, mediaScale: requiredNumber(preview, "mediaScale", "component.callParticipant.runtime.mediaScale"), mediaOffset: requiredString(preview, "mediaOffset", "component.callParticipant.runtime.mediaOffset"),
      isPlaying: videoPresent, currentTimeSeconds: Math.max(0, payload.localFrame / Math.max(1, payload.frameRate)), durationSeconds: 0, isFullScreen: false, fullScreenTransition: false, fullframeOrientation: "portrait", controlsElapsedMs: 60000, motionElapsedMs: 0,
    }, bases, "component.callParticipant.media"),
    avatar: resolveAvatarComponentFromRecords(avatarConfig, { actor, sampleSubtext: "", showBadge: false, badgeContentMode: "icon", badgeIconToken: "system_check", badgeText: "", badgeSize: 20, badgeBackgroundPaletteColor: "blue", badgeContentPaletteColor: "gray_100" }, bases, "component.callParticipant.avatar"),
    statusLabel: resolveLabelComponentFromRecords(statusLabelConfig, literalLabelPreview(effectiveStatus), bases, "component.callParticipant.status", { localFrame: payload.localFrame, frameRate: payload.frameRate }),
    nameLabel: resolveLabelComponentFromRecords(nameLabelConfig, literalLabelPreview(actorName), bases, "component.callParticipant.name", { localFrame: payload.localFrame, frameRate: payload.frameRate }),
    statusPlacement: requiredPlacement(owner, "statusPlacement", "component.callParticipant.statusPlacement"),
    namePlacement: requiredPlacement(owner, "namePlacement", "component.callParticipant.namePlacement"),
    microphonePlacement: requiredPlacement(owner, "microphonePlacement", "component.callParticipant.microphonePlacement"),
    connectionPlacement: requiredPlacement(owner, "connectionPlacement", "component.callParticipant.connectionPlacement"),
  };
}

function slotConfig(bases: Record<string, unknown>, owner: Record<string, unknown>, key: string, type: string) {
  const path = `component.callParticipant.${key}`;
  const slot = requiredRecord(owner, key, path);
  requireComponentVariantType(bases, slot, type, path);
  return mergeComponentDefaults(componentVariantConfig(bases, type, requiredString(slot, "variantReference", `${path}.variantReference`)), requiredRecord(slot, "overrides", `${path}.overrides`));
}
function microphone(value: string): CallParticipantMicrophoneState { if (value === "on" || value === "muted") return value; throw new Error(`Unsupported microphone state ${value}`); }
function connection(value: string): CallParticipantConnectionState { if (value === "connecting" || value === "connected" || value === "weak" || value === "lost") return value; throw new Error(`Unsupported connection state ${value}`); }
function positive(value: number, name: string) { if (!Number.isFinite(value) || value <= 0) throw new Error(`Participant ${name} must be greater than zero`); return value; }
