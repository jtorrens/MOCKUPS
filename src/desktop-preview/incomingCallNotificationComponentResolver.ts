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
  requiredPlacement,
  requiredRecord,
  requiredString,
  requiredStringPair,
} from "./componentResolverCommon.js";
import { requiredComponentBoundaryMotion } from "./componentBoundaryMotion.js";
import { requiredObjectArray } from "./previewJsonHelpers.js";
import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import { resolveIconRowComponentFromRecords } from "./iconRowComponentResolver.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";
import type { IncomingCallNotificationDesignContract } from "./incomingCallNotificationComponentContract.js";

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
  const iconRowConfig = componentSlotConfig(
    bases,
    owner,
    "iconRowSlot",
    "iconRow",
  );

  return {
    id: "component.incomingCallNotification",
    size,
    padding: { xToken: rawPadding.first, yToken: rawPadding.second },
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
    avatarPlacement: requiredPlacement(
      owner,
      "avatarPlacement",
      "component.incomingCallNotification.avatarPlacement",
    ),
    iconRow: resolveIconRowComponentFromRecords(
      iconRowConfig,
      exactChildRuntime(preview, "iconRowRuntime", "iconRow"),
      bases,
      "component.incomingCallNotification.iconRow",
    ),
    iconRowPlacement: requiredPlacement(
      owner,
      "iconRowPlacement",
      "component.incomingCallNotification.iconRowPlacement",
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
