import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { componentVariantConfig, mergeComponentDefaults, requireComponentVariantType } from "./componentPreviewDefaults.js";
import { parseObject, requiredBoolean, requiredComponentVariantSlot, requiredNumber, requiredNumberPair, requiredPlacement, requiredPossiblyEmptyString, requiredRecord, requiredString, requiredStringPair } from "./componentResolverCommon.js";
import { requiredObjectArray } from "./previewJsonHelpers.js";
import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import { literalLabelPreview, resolveLabelComponentFromRecords } from "./labelComponentResolver.js";
import { resolveMediaComponentFromRecords } from "./mediaComponentResolver.js";
import { requiredRows, requiredRuntimeRows, resolveRow } from "./moduleRowSectionResolver.js";
import { resolveContentRowComponent } from "./contentRowComponentResolver.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";
import type { VideoCallComponentSlot, VideoCallConnectionState, VideoCallModuleContract, VideoCallParticipantRole } from "./videoCallModuleContract.js";

export function resolveVideoCallModule(payload: DesignPreviewPayload): VideoCallModuleContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const owner = requiredRecord(config, "videoCall", "module.core.videoCall");
  const participantMediaConfig = slotConfig(bases, typedSlot(owner, bases, "participantMediaSlot", "media"), "media", "module.core.videoCall.participantMediaSlot");
  const participantAvatarConfig = slotConfig(bases, typedSlot(owner, bases, "participantAvatarSlot", "avatar"), "avatar", "module.core.videoCall.participantAvatarSlot");
  const participantStatusConfig = slotConfig(bases, typedSlot(owner, bases, "participantStatusLabelSlot", "label"), "label", "module.core.videoCall.participantStatusLabelSlot");
  const surfaceConfigs = {
    main: slotConfig(bases, typedSlot(owner, bases, "mainSurfaceSlot", "surface"), "surface", "module.core.videoCall.mainSurfaceSlot"),
    pip: slotConfig(bases, typedSlot(owner, bases, "pipSurfaceSlot", "surface"), "surface", "module.core.videoCall.pipSurfaceSlot"),
    grid: slotConfig(bases, typedSlot(owner, bases, "gridSurfaceSlot", "surface"), "surface", "module.core.videoCall.gridSurfaceSlot"),
  };
  const participantPadding = pair(owner, "participantContentPadding");
  const avatarSize = positive(requiredNumber(owner, "participantAvatarSize", "module.core.videoCall.participantAvatarSize"), "participantAvatarSize");
  const defaultStatusText = requiredString(owner, "defaultStatusText", "module.core.videoCall.defaultStatusText");
  const participants = requiredObjectArray(preview, "participants", "module.core.videoCall.runtime.participants")
    .filter((item, index) => requiredBoolean(item, "present", `module.core.videoCall.runtime.participants[${index}].present`))
    .map((item, index) => {
      const role = participantRole(requiredString(item, "role", `module.core.videoCall.runtime.participants[${index}].role`));
      const itemOwner = `module.core.videoCall.runtime.participants[${index}]`;
      const videoPresent = requiredBoolean(item, "videoPresent", `${itemOwner}.videoPresent`);
      const actor = requiredRecord(item, "actor", `${itemOwner}.actor`);
      const avatarConfig = structuredClone(participantAvatarConfig);
      requiredRecord(avatarConfig, "avatar", "module.core.videoCall.participantAvatar").defaultSize = avatarSize;
      const statusOverride = requiredPossiblyEmptyString(item, "statusTextOverride", `${itemOwner}.statusTextOverride`);
      return {
        id: requiredString(item, "id", `${itemOwner}.id`),
        role,
        videoPresent,
        connectionState: connectionState(requiredString(item, "connectionState", `${itemOwner}.connectionState`)),
        padding: participantPadding,
        avatarSize,
        showSurface: requiredBoolean(owner, "showParticipantSurface", "module.core.videoCall.showParticipantSurface"),
        showMedia: requiredBoolean(owner, "showParticipantMedia", "module.core.videoCall.showParticipantMedia"),
        showFallbackAvatar: requiredBoolean(owner, "showFallbackAvatar", "module.core.videoCall.showFallbackAvatar"),
        showFallbackStatus: requiredBoolean(owner, "showFallbackStatus", "module.core.videoCall.showFallbackStatus"),
        surface: resolveSurfaceComponentAtSize(surfaceConfigs[role], { width: 390, height: 844 }, `${itemOwner}.surface`),
        media: resolveMediaComponentFromRecords(payload, participantMediaConfig, {
          mediaType: requiredString(item, "mediaType", `${itemOwner}.mediaType`),
          mediaSource: requiredPossiblyEmptyString(item, "mediaSource", `${itemOwner}.mediaSource`),
          viewportSize: "390|844",
          mediaScale: requiredNumber(item, "mediaScale", `${itemOwner}.mediaScale`),
          mediaOffset: requiredString(item, "mediaOffset", `${itemOwner}.mediaOffset`),
          isPlaying: videoPresent,
          currentTimeSeconds: Math.max(0, payload.localFrame / Math.max(1, payload.frameRate)),
          durationSeconds: 0,
          isFullScreen: false,
          fullScreenTransition: false,
          fullframeOrientation: "portrait",
          controlsElapsedMs: 60000,
          motionElapsedMs: 0,
        }, bases, `${itemOwner}.media`),
        avatar: resolveAvatarComponentFromRecords(avatarConfig, {
          actor,
          sampleSubtext: "",
          showBadge: false,
          badgeContentMode: "icon",
          badgeIconToken: "system_check",
          badgeText: "",
          badgeSize: 20,
          badgeBackgroundPaletteColor: "blue",
          badgeContentPaletteColor: "gray_100",
        }, bases, `${itemOwner}.avatar`),
        statusLabel: resolveLabelComponentFromRecords(
          participantStatusConfig,
          literalLabelPreview(statusOverride.trim() || defaultStatusText),
          bases,
          `${itemOwner}.status`,
          { localFrame: payload.localFrame, frameRate: payload.frameRate },
        ),
      };
    });
  const headerRows = requiredRows(owner, "headerRows", "row", "module.core.videoCall");
  const headerRuntimeRows = requiredRuntimeRows(preview, "videoCallHeaderRows", "row", "module.core.videoCall");
  const footerRows = requiredRows(owner, "footerRows", "footerRow", "module.core.videoCall");
  const footerRuntimeRows = requiredRuntimeRows(preview, "videoCallFooterRows", "footerRow", "module.core.videoCall");
  const mainRows = requiredRows(owner, "mainRows", "mainRow", "module.core.videoCall", 3);
  const mainRuntimeRows = requiredRuntimeRows(preview, "videoCallMainRows", "mainRow", "module.core.videoCall", 3);
  const pipSize = requiredNumberPair(owner, "pipSize", "module.core.videoCall.pipSize");
  return {
    id: "module.core.videoCall",
    useAppWallpaper: requiredBoolean(owner, "useAppWallpaper", "module.core.videoCall.useAppWallpaper"),
    backgroundColorToken: requiredString(owner, "backgroundColorToken", "module.core.videoCall.backgroundColorToken"),
    showHeader: requiredBoolean(owner, "showHeader", "module.core.videoCall.showHeader"),
    headerLayoutMode: sectionLayoutMode(requiredString(owner, "headerLayoutMode", "module.core.videoCall.headerLayoutMode")),
    headerFloatHorizontalPaddingToken: requiredString(owner, "headerFloatHorizontalPaddingToken", "module.core.videoCall.headerFloatHorizontalPaddingToken"),
    headerFloatOffsetY: nonNegative(requiredNumber(owner, "headerFloatOffsetY", "module.core.videoCall.headerFloatOffsetY"), "headerFloatOffsetY"),
    headerHeight: nonNegative(requiredNumber(owner, "headerHeight", "module.core.videoCall.headerHeight"), "headerHeight"),
    headerSurfaceSlot: typedSlot(owner, bases, "headerSurfaceSlot", "surface"),
    headerRowGapToken: requiredString(owner, "headerRowGapToken", "module.core.videoCall.headerRowGapToken"),
    headerRows: [
      resolveRow(payload, "module.core.videoCall", "header", 1, headerRows[0], headerRuntimeRows[0], bases, "contentRow", resolveContentRowComponent),
      resolveRow(payload, "module.core.videoCall", "header", 2, headerRows[1], headerRuntimeRows[1], bases, "contentRow", resolveContentRowComponent),
    ],
    showFooter: requiredBoolean(owner, "showFooter", "module.core.videoCall.showFooter"),
    footerLayoutMode: sectionLayoutMode(requiredString(owner, "footerLayoutMode", "module.core.videoCall.footerLayoutMode")),
    footerFloatHorizontalPaddingToken: requiredString(owner, "footerFloatHorizontalPaddingToken", "module.core.videoCall.footerFloatHorizontalPaddingToken"),
    footerFloatOffsetY: nonNegative(requiredNumber(owner, "footerFloatOffsetY", "module.core.videoCall.footerFloatOffsetY"), "footerFloatOffsetY"),
    footerHeight: nonNegative(requiredNumber(owner, "footerHeight", "module.core.videoCall.footerHeight"), "footerHeight"),
    footerSurfaceSlot: typedSlot(owner, bases, "footerSurfaceSlot", "surface"),
    footerRowGapToken: requiredString(owner, "footerRowGapToken", "module.core.videoCall.footerRowGapToken"),
    footerRows: [
      resolveRow(payload, "module.core.videoCall", "footer", 1, footerRows[0], footerRuntimeRows[0], bases, "contentRow", resolveContentRowComponent),
      resolveRow(payload, "module.core.videoCall", "footer", 2, footerRows[1], footerRuntimeRows[1], bases, "contentRow", resolveContentRowComponent),
    ],
    showMainVideo: requiredBoolean(owner, "showMainVideo", "module.core.videoCall.showMainVideo"),
    mainPadding: pair(owner, "mainPadding"),
    mainRows: [
      resolveRow(payload, "module.core.videoCall", "main", 1, mainRows[0]!, mainRuntimeRows[0]!, bases, "contentRow", resolveContentRowComponent),
      resolveRow(payload, "module.core.videoCall", "main", 2, mainRows[1]!, mainRuntimeRows[1]!, bases, "contentRow", resolveContentRowComponent),
      resolveRow(payload, "module.core.videoCall", "main", 3, mainRows[2]!, mainRuntimeRows[2]!, bases, "contentRow", resolveContentRowComponent),
    ],
    showPip: requiredBoolean(owner, "showPip", "module.core.videoCall.showPip"),
    pipSize: { width: positive(pipSize.first, "pipSize.width"), height: positive(pipSize.second, "pipSize.height") },
    pipPlacement: requiredPlacement(owner, "pipPlacement", "module.core.videoCall.pipPlacement"),
    pipPadding: pair(owner, "pipPadding"),
    showGridParticipants: requiredBoolean(owner, "showGridParticipants", "module.core.videoCall.showGridParticipants"),
    gridPadding: pair(owner, "gridPadding"),
    gridGapToken: requiredString(owner, "gridGapToken", "module.core.videoCall.gridGapToken"),
    gridHeightMode: gridHeightMode(requiredString(owner, "gridHeightMode", "module.core.videoCall.gridHeightMode")),
    gridHeight: positive(requiredNumber(owner, "gridHeight", "module.core.videoCall.gridHeight"), "gridHeight"),
    gridRows: Math.max(1, Math.round(requiredNumber(owner, "gridRows", "module.core.videoCall.gridRows"))),
    showStatusBar: requiredBoolean(owner, "showStatusBar", "module.core.videoCall.showStatusBar"),
    showNavigationBar: requiredBoolean(owner, "showNavigationBar", "module.core.videoCall.showNavigationBar"),
    participants,
    statusBarSlot: typedSlot(owner, bases, "statusBarSlot", "status_bar"),
    navigationBarSlot: typedSlot(owner, bases, "navigationBarSlot", "navigation_bar"),
  };
}

function typedSlot(owner: Record<string, unknown>, bases: Record<string, unknown>, key: string, type: string): VideoCallComponentSlot { const slot = requiredComponentVariantSlot(owner, key, `module.core.videoCall.${key}`); requireComponentVariantType(bases, slot, type, `module.core.videoCall.${key}`); return slot; }
function slotConfig(bases: Record<string, unknown>, slot: VideoCallComponentSlot, type: string, path: string) { return mergeComponentDefaults(componentVariantConfig(bases, type, requiredString(slot, "variantReference", `${path}.variantReference`)), requiredRecord(slot, "overrides", `${path}.overrides`)); }
function pair(owner: Record<string, unknown>, key: string) { const value = requiredStringPair(owner, key, `module.core.videoCall.${key}`); return { xToken: value.first, yToken: value.second }; }
function participantRole(value: string): VideoCallParticipantRole { if (value === "main" || value === "pip" || value === "grid") return value; throw new Error(`Unsupported participant role '${value}'`); }
function connectionState(value: string): VideoCallConnectionState { if (value === "connecting" || value === "connected" || value === "weak" || value === "lost") return value; throw new Error(`Unsupported connection state '${value}'`); }
function sectionLayoutMode(value: string): "stack" | "float" { if (value === "stack" || value === "float") return value; throw new Error(`Unsupported section layout mode '${value}'`); }
function gridHeightMode(value: string): VideoCallModuleContract["gridHeightMode"] { if (value === "fixed" || value === "fill") return value; throw new Error(`Unsupported grid height mode '${value}'`); }
function positive(value: number, path: string) { if (!Number.isFinite(value) || value <= 0) throw new Error(`${path} must be positive`); return value; }
function nonNegative(value: number, path: string) { if (!Number.isFinite(value) || value < 0) throw new Error(`${path} must be non-negative`); return value; }
