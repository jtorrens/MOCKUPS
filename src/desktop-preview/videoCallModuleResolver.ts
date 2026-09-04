import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { componentVariantConfig, mergeComponentDefaults, requireComponentVariantType } from "./componentPreviewDefaults.js";
import { parseObject, requiredBoolean, requiredComponentVariantSlot, requiredNumber, requiredNumberPair, requiredPlacement, requiredRecord, requiredString, requiredStringPair } from "./componentResolverCommon.js";
import { requiredObjectArray } from "./previewJsonHelpers.js";
import { resolveCallParticipantComponent } from "./callParticipantComponentResolver.js";
import { requiredRows, requiredRuntimeRows, resolveRow } from "./moduleRowSectionResolver.js";
import type { VideoCallComponentSlot, VideoCallModuleContract, VideoCallParticipantRole } from "./videoCallModuleContract.js";

export function resolveVideoCallModule(payload: DesignPreviewPayload): VideoCallModuleContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const owner = requiredRecord(config, "videoCall", "module.core.videoCall");
  const showNames = requiredBoolean(owner, "showParticipantNames", "module.core.videoCall.showParticipantNames");
  const showStatus = requiredBoolean(owner, "showParticipantStatus", "module.core.videoCall.showParticipantStatus");
  const participantConfigs = {
    main: participantConfig(owner, bases, "mainParticipantSlot", showNames, showStatus),
    pip: participantConfig(owner, bases, "pipParticipantSlot", showNames, showStatus),
    grid: participantConfig(owner, bases, "gridParticipantSlot", showNames, showStatus),
  };
  const participants = requiredObjectArray(preview, "participants", "module.core.videoCall.runtime.participants")
    .filter((item, index) => requiredBoolean(item, "present", `module.core.videoCall.runtime.participants[${index}].present`))
    .map((item, index) => {
      const role = participantRole(requiredString(item, "role", `module.core.videoCall.runtime.participants[${index}].role`));
      return {
        id: requiredString(item, "id", `module.core.videoCall.runtime.participants[${index}].id`),
        role,
        participant: resolveCallParticipantComponent({
          ...payload,
          componentType: "callParticipant",
          configJson: JSON.stringify(participantConfigs[role]),
          designPreviewJson: JSON.stringify({ ...item, viewportSize: "390|844" }),
        }),
      };
    });
  const headerRows = requiredRows(owner, "headerRows", "row", "module.core.videoCall");
  const headerRuntimeRows = requiredRuntimeRows(preview, "videoCallHeaderRows", "row", "module.core.videoCall");
  const footerRows = requiredRows(owner, "footerRows", "footerRow", "module.core.videoCall");
  const footerRuntimeRows = requiredRuntimeRows(preview, "videoCallFooterRows", "footerRow", "module.core.videoCall");
  const mainSize = requiredNumberPair(owner, "mainSize", "module.core.videoCall.mainSize");
  const pipSize = requiredNumberPair(owner, "pipSize", "module.core.videoCall.pipSize");
  return {
    id: "module.core.videoCall",
    useAppWallpaper: requiredBoolean(owner, "useAppWallpaper", "module.core.videoCall.useAppWallpaper"),
    conversationType: conversationType(requiredString(owner, "conversationType", "module.core.videoCall.conversationType")),
    backgroundColorToken: requiredString(owner, "backgroundColorToken", "module.core.videoCall.backgroundColorToken"),
    showHeader: requiredBoolean(owner, "showHeader", "module.core.videoCall.showHeader"),
    headerLayoutMode: sectionLayoutMode(requiredString(owner, "headerLayoutMode", "module.core.videoCall.headerLayoutMode")),
    headerFloatHorizontalPaddingToken: requiredString(owner, "headerFloatHorizontalPaddingToken", "module.core.videoCall.headerFloatHorizontalPaddingToken"),
    headerFloatOffsetY: nonNegative(requiredNumber(owner, "headerFloatOffsetY", "module.core.videoCall.headerFloatOffsetY"), "headerFloatOffsetY"),
    headerHeight: nonNegative(requiredNumber(owner, "headerHeight", "module.core.videoCall.headerHeight"), "headerHeight"),
    headerSurfaceSlot: typedSlot(owner, bases, "headerSurfaceSlot", "surface"),
    headerRowGapToken: requiredString(owner, "headerRowGapToken", "module.core.videoCall.headerRowGapToken"),
    headerRows: [
      resolveRow("module.core.videoCall", "header", 1, headerRows[0], headerRuntimeRows[0], bases),
      resolveRow("module.core.videoCall", "header", 2, headerRows[1], headerRuntimeRows[1], bases),
    ],
    showFooter: requiredBoolean(owner, "showFooter", "module.core.videoCall.showFooter"),
    footerLayoutMode: sectionLayoutMode(requiredString(owner, "footerLayoutMode", "module.core.videoCall.footerLayoutMode")),
    footerFloatHorizontalPaddingToken: requiredString(owner, "footerFloatHorizontalPaddingToken", "module.core.videoCall.footerFloatHorizontalPaddingToken"),
    footerFloatOffsetY: nonNegative(requiredNumber(owner, "footerFloatOffsetY", "module.core.videoCall.footerFloatOffsetY"), "footerFloatOffsetY"),
    footerHeight: nonNegative(requiredNumber(owner, "footerHeight", "module.core.videoCall.footerHeight"), "footerHeight"),
    footerSurfaceSlot: typedSlot(owner, bases, "footerSurfaceSlot", "surface"),
    footerRowGapToken: requiredString(owner, "footerRowGapToken", "module.core.videoCall.footerRowGapToken"),
    footerRows: [
      resolveRow("module.core.videoCall", "footer", 1, footerRows[0], footerRuntimeRows[0], bases),
      resolveRow("module.core.videoCall", "footer", 2, footerRows[1], footerRuntimeRows[1], bases),
    ],
    showMainVideo: requiredBoolean(owner, "showMainVideo", "module.core.videoCall.showMainVideo"),
    mainSizeMode: sizeMode(requiredString(owner, "mainSizeMode", "module.core.videoCall.mainSizeMode")),
    mainSize: { width: positive(mainSize.first, "mainSize.width"), height: positive(mainSize.second, "mainSize.height") },
    mainPlacement: requiredPlacement(owner, "mainPlacement", "module.core.videoCall.mainPlacement"),
    mainPadding: pair(owner, "mainPadding"),
    showPip: requiredBoolean(owner, "showPip", "module.core.videoCall.showPip"),
    pipSize: { width: positive(pipSize.first, "pipSize.width"), height: positive(pipSize.second, "pipSize.height") },
    pipPlacement: requiredPlacement(owner, "pipPlacement", "module.core.videoCall.pipPlacement"),
    pipPadding: pair(owner, "pipPadding"),
    showGridParticipants: requiredBoolean(owner, "showGridParticipants", "module.core.videoCall.showGridParticipants"),
    gridPadding: pair(owner, "gridPadding"),
    gridGapToken: requiredString(owner, "gridGapToken", "module.core.videoCall.gridGapToken"),
    gridRows: Math.max(1, Math.round(requiredNumber(owner, "gridRows", "module.core.videoCall.gridRows"))),
    showStatusBar: requiredBoolean(owner, "showStatusBar", "module.core.videoCall.showStatusBar"),
    showNavigationBar: requiredBoolean(owner, "showNavigationBar", "module.core.videoCall.showNavigationBar"),
    participants,
    statusBarSlot: typedSlot(owner, bases, "statusBarSlot", "status_bar"),
    navigationBarSlot: typedSlot(owner, bases, "navigationBarSlot", "navigation_bar"),
  };
}

function participantConfig(owner: Record<string, unknown>, bases: Record<string, unknown>, key: string, showNames: boolean, showStatus: boolean) {
  const resolved = slotConfig(bases, typedSlot(owner, bases, key, "callParticipant"), "callParticipant", `module.core.videoCall.${key}`);
  const participant = requiredRecord(resolved, "callParticipant", `module.core.videoCall.${key}`);
  participant.showName = showNames;
  participant.showStatusWhenVideoAbsent = showStatus;
  participant.showConnectionStatus = showStatus;
  return resolved;
}
function typedSlot(owner: Record<string, unknown>, bases: Record<string, unknown>, key: string, type: string): VideoCallComponentSlot { const slot = requiredComponentVariantSlot(owner, key, `module.core.videoCall.${key}`); requireComponentVariantType(bases, slot, type, `module.core.videoCall.${key}`); return slot; }
function slotConfig(bases: Record<string, unknown>, slot: VideoCallComponentSlot, type: string, path: string) { return mergeComponentDefaults(componentVariantConfig(bases, type, requiredString(slot, "variantReference", `${path}.variantReference`)), requiredRecord(slot, "overrides", `${path}.overrides`)); }
function pair(owner: Record<string, unknown>, key: string) { const value = requiredStringPair(owner, key, `module.core.videoCall.${key}`); return { xToken: value.first, yToken: value.second }; }
function participantRole(value: string): VideoCallParticipantRole { if (value === "main" || value === "pip" || value === "grid") return value; throw new Error(`Unsupported participant role '${value}'`); }
function conversationType(value: string): VideoCallModuleContract["conversationType"] { if (value === "individual" || value === "group") return value; throw new Error(`Unsupported conversation type '${value}'`); }
function sectionLayoutMode(value: string): "stack" | "float" { if (value === "stack" || value === "float") return value; throw new Error(`Unsupported section layout mode '${value}'`); }
function sizeMode(value: string): VideoCallModuleContract["mainSizeMode"] { if (value === "fill" || value === "fixed") return value; throw new Error(`Unsupported main size mode '${value}'`); }
function positive(value: number, path: string) { if (!Number.isFinite(value) || value <= 0) throw new Error(`${path} must be positive`); return value; }
function nonNegative(value: number, path: string) { if (!Number.isFinite(value) || value < 0) throw new Error(`${path} must be non-negative`); return value; }
