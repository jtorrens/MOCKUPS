import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentVariantConfig,
  mergeComponentDefaults,
  requireComponentVariantType,
} from "./componentPreviewDefaults.js";
import {
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredNumberPair,
  requiredRecord,
  requiredString,
  requiredStringPair,
} from "./componentResolverCommon.js";
import { requiredComponentBoundaryMotion } from "./componentBoundaryMotion.js";
import { requiredObjectArray } from "./previewJsonHelpers.js";
import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import {
  resolveLabelComponentFromRecords,
  staticLabelFrameContext,
} from "./labelComponentResolver.js";
import { resolveIconRowComponentFromRecords } from "./iconRowComponentResolver.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";
import type {
  IncomingCallNotificationDesignContract,
  IncomingCallNotificationLayout,
} from "./incomingCallNotificationComponentContract.js";

export function resolveIncomingCallNotificationComponent(
  payload: DesignPreviewPayload,
): IncomingCallNotificationDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const owner = requiredRecord(
    config,
    "incomingCallNotification",
    "component.incomingCallNotification",
  );
  const rawSize = requiredNumberPair(
    owner,
    "size",
    "component.incomingCallNotification.size",
  );
  const size = {
    width: positive(rawSize.first, "component.incomingCallNotification.size.width"),
    height: positive(rawSize.second, "component.incomingCallNotification.size.height"),
  };
  const rawPadding = requiredStringPair(
    owner,
    "padding",
    "component.incomingCallNotification.padding",
  );

  const surfaceConfig = componentSlotConfig(
    bases,
    owner,
    "surfaceSlot",
    "surface",
  );
  const avatarConfig = componentSlotConfig(
    bases,
    owner,
    "avatarSlot",
    "avatar",
  );
  const labelConfig = componentSlotConfig(
    bases,
    owner,
    "labelSlot",
    "label",
  );
  const iconRowConfig = componentSlotConfig(
    bases,
    owner,
    "iconRowSlot",
    "iconRow",
  );

  return {
    id: "component.incomingCallNotification",
    layout: layout(requiredString(
      owner,
      "layout",
      "component.incomingCallNotification.layout",
    )),
    size,
    padding: { xToken: rawPadding.first, yToken: rawPadding.second },
    contentGapToken: requiredString(
      owner,
      "contentGapToken",
      "component.incomingCallNotification.contentGapToken",
    ),
    sectionGapToken: requiredString(
      owner,
      "sectionGapToken",
      "component.incomingCallNotification.sectionGapToken",
    ),
    avatarSize: positive(
      requiredNumber(
        owner,
        "avatarSize",
        "component.incomingCallNotification.avatarSize",
      ),
      "component.incomingCallNotification.avatarSize",
    ),
    present: requiredBoolean(
      preview,
      "present",
      "component.incomingCallNotification.runtime.present",
    ),
    presenceTransition: requiredBoolean(
      preview,
      "presenceTransition",
      "component.incomingCallNotification.runtime.presenceTransition",
    ),
    presenceElapsedMs: nonNegative(
      requiredNumber(
        preview,
        "presenceElapsedMs",
        "component.incomingCallNotification.runtime.presenceElapsedMs",
      ),
      "component.incomingCallNotification.runtime.presenceElapsedMs",
    ),
    boundaryMotion: requiredComponentBoundaryMotion(
      config,
      "component.incomingCallNotification",
    ),
    surface: resolveSurfaceComponentAtSize(
      surfaceConfig,
      size,
      "component.incomingCallNotification.surface",
    ),
    avatar: resolveAvatarComponentFromRecords(
      avatarConfig,
      exactChildRuntime(preview, "avatarRuntime", "avatar"),
      bases,
      "component.incomingCallNotification.avatar",
    ),
    label: resolveLabelComponentFromRecords(
      labelConfig,
      exactChildRuntime(preview, "labelRuntime", "label"),
      bases,
      "component.incomingCallNotification.label",
      staticLabelFrameContext,
    ),
    iconRow: resolveIconRowComponentFromRecords(
      iconRowConfig,
      exactChildRuntime(preview, "iconRowRuntime", "actions"),
      bases,
      "component.incomingCallNotification.actions",
    ),
  };
}

function componentSlotConfig(
  bases: Record<string, unknown>,
  owner: Record<string, unknown>,
  slotKey: string,
  componentType: string,
) {
  const path = `component.incomingCallNotification.${slotKey}`;
  const slot = requiredRecord(owner, slotKey, path);
  requireComponentVariantType(bases, slot, componentType, path);
  return mergeComponentDefaults(
    componentVariantConfig(
      bases,
      componentType,
      requiredString(slot, "variantReference", `${path}.variantReference`),
    ),
    requiredRecord(slot, "overrides", `${path}.overrides`),
  );
}

function exactChildRuntime(
  preview: Record<string, unknown>,
  collectionKey: string,
  expectedId: string,
) {
  const items = requiredObjectArray(
    preview,
    collectionKey,
    `component.incomingCallNotification.runtime.${collectionKey}`,
  );
  if (items.length !== 1) {
    throw new Error(
      `component.incomingCallNotification runtime '${collectionKey}' requires exactly one item`,
    );
  }
  const item = items[0]!;
  const id = requiredString(
    item,
    "id",
    `component.incomingCallNotification.runtime.${collectionKey}[0].id`,
  );
  if (id !== expectedId) {
    throw new Error(
      `component.incomingCallNotification runtime '${collectionKey}' requires id '${expectedId}'`,
    );
  }
  return requiredRecord(
    item,
    "runtimeInputs",
    `component.incomingCallNotification.runtime.${collectionKey}[0].runtimeInputs`,
  );
}

function layout(value: string): IncomingCallNotificationLayout {
  if (value === "compact" || value === "stackedActions") return value;
  throw new Error(`Unsupported Incoming Call Notification layout ${value}`);
}

function positive(value: number, path: string) {
  if (!Number.isFinite(value) || value <= 0) {
    throw new Error(`${path} must be greater than zero`);
  }
  return value;
}

function nonNegative(value: number, path: string) {
  if (!Number.isFinite(value) || value < 0) {
    throw new Error(`${path} must be zero or greater`);
  }
  return value;
}
