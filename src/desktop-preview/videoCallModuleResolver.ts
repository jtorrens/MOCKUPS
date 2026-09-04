import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { componentVariantConfig, mergeComponentDefaults, requireComponentVariantType } from "./componentPreviewDefaults.js";
import { parseObject, requiredBoolean, requiredComponentVariantSlot, requiredNumber, requiredNumberPair, requiredPlacement, requiredRecord, requiredString, requiredStringPair } from "./componentResolverCommon.js";
import { requiredObjectArray } from "./previewJsonHelpers.js";
import { resolveCallParticipantComponent } from "./callParticipantComponentResolver.js";
import { resolveButtonComponentFromRecords } from "./buttonComponentResolver.js";
import { literalLabelPreview, resolveLabelComponentFromRecords } from "./labelComponentResolver.js";
import type { VideoCallComponentSlot, VideoCallModuleContract } from "./videoCallModuleContract.js";

export function resolveVideoCallModule(
  payload: DesignPreviewPayload,
): VideoCallModuleContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const owner = requiredRecord(config, "videoCall", "module.core.videoCall");
  const visibilityKeys = ["showStatusBar", "showHeader", "showBackButton", "showCallTitle", "showParticipantCount", "showDuration", "showAddParticipant", "showSelfView", "showParticipantNames", "showParticipantStatus", "showControls", "showNavigationBar", "showCameraControl", "showMicrophoneControl", "showSpeakerControl", "showMoreControl", "showEndCallControl"] as const;
  const visibility: Record<string, boolean> = {};
  for (const key of visibilityKeys) visibility[key] = requiredBoolean(owner, key, `module.core.videoCall.${key}`);
  const participantSlot = typedSlot(owner, bases, "participantSlot", "callParticipant");
  const participantBase = slotConfig(bases, participantSlot, "callParticipant", "module.core.videoCall.participantSlot");
  const participantOwner = requiredRecord(participantBase, "callParticipant", "module.core.videoCall.participantSlot");
  participantOwner.showName = visibility.showParticipantNames;
  participantOwner.showStatusWhenVideoAbsent = visibility.showParticipantStatus;
  participantOwner.showConnectionStatus = visibility.showParticipantStatus;
  const selfParticipantId = requiredString(preview, "selfParticipantId", "module.core.videoCall.runtime.selfParticipantId");
  const rawParticipants = requiredObjectArray(preview, "participants", "module.core.videoCall.runtime.participants");
  const participants = rawParticipants.filter((item, index) => requiredBoolean(item, "present", `module.core.videoCall.runtime.participants[${index}].present`)).map((item, index) => {
    const id = requiredString(item, "id", `module.core.videoCall.runtime.participants[${index}].id`);
    return { id, isSelf: id === selfParticipantId, participant: resolveCallParticipantComponent({ ...payload, componentType: "callParticipant", configJson: JSON.stringify(participantBase), designPreviewJson: JSON.stringify({ ...item, viewportSize: "390|844" }) }) };
  });
  if (visibility.showSelfView && selfParticipantId && !participants.some(item => item.id === selfParticipantId)) throw new Error(`Self participant '${selfParticipantId}' must reference a present participant`);
  const titleConfig = slotConfig(bases, typedSlot(owner, bases, "titleLabelSlot", "label"), "label", "module.core.videoCall.titleLabelSlot");
  const metaConfig = slotConfig(bases, typedSlot(owner, bases, "metaLabelSlot", "label"), "label", "module.core.videoCall.metaLabelSlot");
  const frame = { localFrame: payload.localFrame, frameRate: payload.frameRate };
  const label = (cfg: Record<string, unknown>, text: string, id: string) => resolveLabelComponentFromRecords(cfg, literalLabelPreview(text), bases, id, frame);
  const controlDefinitions = [
    ["camera", "cameraButtonSlot", visibility.showCameraControl, requiredBoolean(preview, "cameraEnabled", "module.core.videoCall.runtime.cameraEnabled") ? "media_video" : "media_video_off"],
    ["microphone", "microphoneButtonSlot", visibility.showMicrophoneControl, requiredBoolean(preview, "microphoneEnabled", "module.core.videoCall.runtime.microphoneEnabled") ? "media_mic" : "media_volume_off"],
    ["speaker", "speakerButtonSlot", visibility.showSpeakerControl, requiredBoolean(preview, "speakerEnabled", "module.core.videoCall.runtime.speakerEnabled") ? "media_volume" : "media_volume_off"],
    ["more", "moreButtonSlot", visibility.showMoreControl, "nav_more_horizontal"],
    ["endCall", "endCallButtonSlot", visibility.showEndCallControl, "phone_hangup"],
  ] as const;
  const controls = controlDefinitions.filter(([, , show]) => show).map(([id, slotKey, , icon]) => ({ id, button: resolveButton(owner, bases, slotKey, icon, `module.core.videoCall.${id}`) }));
  const selfSize = requiredNumberPair(owner, "selfViewSize", "module.core.videoCall.selfViewSize");
  return {
    id: "module.core.videoCall", useAppWallpaper: requiredBoolean(owner, "useAppWallpaper", "module.core.videoCall.useAppWallpaper"), visibility,
    layoutMode: layoutMode(requiredString(preview, "layoutMode", "module.core.videoCall.runtime.layoutMode")), activeParticipantId: requiredString(preview, "activeParticipantId", "module.core.videoCall.runtime.activeParticipantId"),
    screenPadding: pair(owner, "screenPadding"), participantGapToken: requiredString(owner, "participantGapToken", "module.core.videoCall.participantGapToken"),
    headerHeight: positive(requiredNumber(owner, "headerHeight", "module.core.videoCall.headerHeight"), "headerHeight"), controlsHeight: positive(requiredNumber(owner, "controlsHeight", "module.core.videoCall.controlsHeight"), "controlsHeight"),
    selfViewSize: { width: positive(selfSize.first, "selfViewSize.width"), height: positive(selfSize.second, "selfViewSize.height") }, selfViewPlacement: requiredPlacement(owner, "selfViewPlacement", "module.core.videoCall.selfViewPlacement"),
    backgroundColorToken: requiredString(owner, "backgroundColorToken", "module.core.videoCall.backgroundColorToken"),
    title: label(titleConfig, requiredString(preview, "callTitle", "module.core.videoCall.runtime.callTitle"), "module.core.videoCall.title"),
    participantCount: label(metaConfig, `${participants.length} participants`, "module.core.videoCall.participantCount"), duration: label(metaConfig, formatDuration(payload.localFrame, payload.frameRate), "module.core.videoCall.duration"),
    participants, controls,
    backButton: visibility.showBackButton ? resolveButton(owner, bases, "backButtonSlot", "nav_back", "module.core.videoCall.back") : undefined,
    addButton: visibility.showAddParticipant ? resolveButton(owner, bases, "addButtonSlot", "contact_group", "module.core.videoCall.add") : undefined,
    statusBarSlot: typedSlot(owner, bases, "statusBarSlot", "status_bar"), navigationBarSlot: typedSlot(owner, bases, "navigationBarSlot", "navigation_bar"),
  };
}

function typedSlot(owner: Record<string, unknown>, bases: Record<string, unknown>, key: string, type: string): VideoCallComponentSlot { const slot = requiredComponentVariantSlot(owner, key, `module.core.videoCall.${key}`); requireComponentVariantType(bases, slot, type, `module.core.videoCall.${key}`); return slot; }
function slotConfig(bases: Record<string, unknown>, slot: VideoCallComponentSlot, type: string, path: string) { return mergeComponentDefaults(componentVariantConfig(bases, type, requiredString(slot, "variantReference", `${path}.variantReference`)), requiredRecord(slot, "overrides", `${path}.overrides`)); }
function resolveButton(owner: Record<string, unknown>, bases: Record<string, unknown>, key: string, iconToken: string, id: string) { const slot = typedSlot(owner, bases, key, "button"); return resolveButtonComponentFromRecords(slotConfig(bases, slot, "button", `module.core.videoCall.${key}`), { state: "normal", sampleText: "", iconToken, iconSizeToken: "theme.iconSizes.l", textSizeToken: "theme.typography.sizes.s", pushTrigger: false, showBadge: false, badgeContentMode: "icon", badgeIconToken: "system_check", badgeText: "", badgeSize: 20, badgeBackgroundPaletteColor: "blue", badgeContentPaletteColor: "gray_100" }, bases, id); }
function pair(owner: Record<string, unknown>, key: string) { const value = requiredStringPair(owner, key, `module.core.videoCall.${key}`); return { xToken: value.first, yToken: value.second }; }
function layoutMode(value: string): VideoCallModuleContract["layoutMode"] { if (value === "automatic" || value === "spotlight" || value === "grid") return value; throw new Error(`Unsupported video call layout ${value}`); }
function positive(value: number, path: string) { if (!Number.isFinite(value) || value <= 0) throw new Error(`${path} must be positive`); return value; }
function formatDuration(frame: number, frameRate: number) { const seconds = Math.max(0, Math.floor(frame / Math.max(1, frameRate))); return `${Math.floor(seconds / 60).toString().padStart(2, "0")}:${(seconds % 60).toString().padStart(2, "0")}`; }
