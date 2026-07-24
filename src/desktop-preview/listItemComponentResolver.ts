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
import { resolveParameterAnimation } from "./parameterAnimationResolver.js";
import { optionalObject, requiredObjectArray } from "./previewJsonHelpers.js";
import type {
  ListItemDesignContract,
  ListItemElement,
  ListItemState,
  ListItemVerticalAlignment,
} from "./listItemComponentContract.js";
import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import {
  resolveLabelComponentFromRecords,
  staticLabelFrameContext,
} from "./labelComponentResolver.js";
import { resolveIconRowComponentFromRecords } from "./iconRowComponentResolver.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";

const componentTypes = ["avatar", "label", "iconRow"] as const;
type ListItemComponentType = (typeof componentTypes)[number];

export function resolveListItemComponent(
  payload: DesignPreviewPayload,
): ListItemDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const listItem = requiredRecord(config, "listItem", "component.listItem");
  const size = {
    width: positiveNumber(
      requiredNumber(preview, "width", "component.listItem.runtime.width"),
      "component.listItem.runtime.width",
    ),
    height: positiveNumber(
      requiredNumber(preview, "height", "component.listItem.runtime.height"),
      "component.listItem.runtime.height",
    ),
  };
  const contentSetCount = positiveInteger(
    requiredNumber(listItem, "contentSetCount", "component.listItem.contentSetCount"),
    "component.listItem.contentSetCount",
  );
  const padding = requiredStringPair(
    listItem,
    "padding",
    "component.listItem.padding",
  );
  const gapToken = requiredString(
    listItem,
    "gapToken",
    "component.listItem.gapToken",
  );
  const contentSets = requiredObjectArray(
    preview,
    "contentSets",
    "component.listItem runtime",
  );
  if (contentSets.length !== contentSetCount) {
    throw new Error(
      `component.listItem requires ${contentSetCount} Runtime content sets but received ${contentSets.length}`,
    );
  }

  const animation = optionalObject(
    parseObject(payload.instanceJson),
    "animation",
    "Preview instance envelope",
  );
  const baseActiveSet = positiveInteger(
    requiredNumber(preview, "activeSet", "component.listItem.runtime.activeSet"),
    "component.listItem.runtime.activeSet",
  );
  const activeSetValue = resolveParameterAnimation(
    animation,
    "activeSet",
    "",
    payload.localFrame,
    baseActiveSet,
  ).value;
  if (typeof activeSetValue !== "number") {
    throw new Error("component.listItem activeSet animation must resolve a number");
  }
  const activeSet = positiveInteger(activeSetValue, "component.listItem.runtime.activeSet");
  if (activeSet > contentSetCount) {
    throw new Error(
      `component.listItem activeSet ${activeSet} exceeds contentSetCount ${contentSetCount}`,
    );
  }
  const selectedSet = contentSets[activeSet - 1]!;
  const contentSetId = requiredString(
    selectedSet,
    "id",
    `component.listItem.contentSets[${activeSet - 1}]`,
  );

  const baseState = listItemState(requiredString(
    preview,
    "state",
    "component.listItem.runtime.state",
  ));
  const state = listItemState(resolveParameterAnimation(
    animation,
    "state",
    "",
    payload.localFrame,
    baseState,
  ).value);
  const states = requiredRecord(listItem, "states", "component.listItem.states");
  const stateStyle = requiredRecord(
    states,
    state,
    `component.listItem.states.${state}`,
  );
  const surfaceSlot = requiredRecord(
    stateStyle,
    "surfaceSlot",
    `component.listItem.states.${state}.surfaceSlot`,
  );
  const surfaceConfig = slotConfig(
    bases,
    surfaceSlot,
    "surface",
    `component.listItem.states.${state}.surfaceSlot`,
  );
  const elementsOpacity = requiredNumber(
    stateStyle,
    "elementsOpacity",
    `component.listItem.states.${state}.elementsOpacity`,
  );
  if (elementsOpacity < 0 || elementsOpacity > 1) {
    throw new Error(`component.listItem.states.${state}.elementsOpacity must be between 0 and 1`);
  }

  const components = requiredRecord(
    listItem,
    "components",
    "component.listItem.components",
  );
  const orders = new Set<number>();
  const elements = componentTypes
    .map((componentType) => {
      const slot = requiredRecord(
        components,
        componentType,
        `component.listItem.components.${componentType}`,
      );
      const order = positiveInteger(
        requiredNumber(slot, "order", `component.listItem.components.${componentType}.order`),
        `component.listItem.components.${componentType}.order`,
      );
      if (!orders.add(order)) {
        throw new Error(`component.listItem component order ${order} is duplicated`);
      }
      return { componentType, slot, order };
    })
    .filter(({ slot }) =>
      requiredBoolean(slot, "visible", "component.listItem component visibility"))
    .sort((left, right) => left.order - right.order)
    .map(({ componentType, slot }) =>
      resolveComponent(
        componentType,
        slot,
        contentSetId,
        preview,
        bases,
      ));

  return {
    id: "component.listItem",
    size,
    state,
    activeSet,
    padding: { xToken: padding.first, yToken: padding.second },
    gapToken,
    surface: resolveSurfaceComponentAtSize(
      surfaceConfig,
      size,
      `component.listItem.${state}.surface`,
    ),
    elementsOpacity,
    elements,
  };
}

function resolveComponent(
  componentType: ListItemComponentType,
  slot: Record<string, unknown>,
  contentSetId: string,
  preview: Record<string, unknown>,
  bases: Record<string, unknown>,
): ListItemElement {
  const path = `component.listItem.components.${componentType}`;
  const componentSlot = requiredRecord(slot, "componentSlot", `${path}.componentSlot`);
  requireComponentVariantType(
    bases,
    componentSlot,
    componentType,
    `${path}.componentSlot`,
  );
  const childConfig = slotConfig(
    bases,
    componentSlot,
    componentType,
    `${path}.componentSlot`,
  );
  const runtime = exactChildRuntime(preview, componentType, contentSetId);
  const verticalAlignment = listItemVerticalAlignment(
    requiredString(slot, "verticalAlignment", `${path}.verticalAlignment`),
  );

  if (componentType === "avatar") {
    const sizeMode = requiredString(slot, "sizeMode", `${path}.sizeMode`);
    if (sizeMode !== "auto" && sizeMode !== "fixed") {
      throw new Error(`${path}.sizeMode must be 'auto' or 'fixed'`);
    }
    return {
      id: "avatar",
      componentType,
      sizeMode,
      fixedSize: positiveNumber(
        requiredNumber(slot, "fixedSize", `${path}.fixedSize`),
        `${path}.fixedSize`,
      ),
      verticalAlignment,
      component: resolveAvatarComponentFromRecords(
        childConfig,
        runtime,
        bases,
        "component.listItem.avatar",
      ),
    };
  }
  if (componentType === "label") {
    const sizeMode = requiredString(slot, "sizeMode", `${path}.sizeMode`);
    if (sizeMode !== "fill" && sizeMode !== "fixed") {
      throw new Error(`${path}.sizeMode must be 'fill' or 'fixed'`);
    }
    const fixedSize = requiredNumberPair(slot, "fixedSize", `${path}.fixedSize`);
    return {
      id: "label",
      componentType,
      sizeMode,
      fixedSize: {
        width: positiveNumber(fixedSize.first, `${path}.fixedSize.width`),
        height: positiveNumber(fixedSize.second, `${path}.fixedSize.height`),
      },
      verticalAlignment,
      component: resolveLabelComponentFromRecords(
        childConfig,
        runtime,
        bases,
        "component.listItem.label",
        staticLabelFrameContext,
      ),
    };
  }
  const sizeMode = requiredString(slot, "sizeMode", `${path}.sizeMode`);
  if (sizeMode !== "content" && sizeMode !== "fixed") {
    throw new Error(`${path}.sizeMode must be 'content' or 'fixed'`);
  }
  const fixedSize = requiredNumberPair(slot, "fixedSize", `${path}.fixedSize`);
  return {
    id: "iconRow",
    componentType,
    sizeMode,
    fixedSize: {
      width: positiveNumber(fixedSize.first, `${path}.fixedSize.width`),
      height: positiveNumber(fixedSize.second, `${path}.fixedSize.height`),
    },
    verticalAlignment,
    component: resolveIconRowComponentFromRecords(
      childConfig,
      runtime,
      bases,
      "component.listItem.iconRow",
    ),
  };
}

function exactChildRuntime(
  preview: Record<string, unknown>,
  componentType: ListItemComponentType,
  contentSetId: string,
) {
  const collectionKey = `${componentType}Content`;
  const items = requiredObjectArray(
    preview,
    collectionKey,
    `component.listItem runtime ${collectionKey}`,
  );
  const matches = items.filter((item) =>
    requiredString(item, "contentSetId", `component.listItem runtime ${collectionKey}`)
      === contentSetId);
  if (matches.length !== 1) {
    throw new Error(
      `component.listItem content set '${contentSetId}' requires exactly one '${componentType}' Runtime contract`,
    );
  }
  return requiredRecord(
    matches[0]!,
    "runtimeInputs",
    `component.listItem runtime ${collectionKey} '${contentSetId}'`,
  );
}

function slotConfig(
  bases: Record<string, unknown>,
  slot: Record<string, unknown>,
  componentType: string,
  path: string,
) {
  return mergeComponentDefaults(
    componentVariantConfig(
      bases,
      componentType,
      requiredString(slot, "variantReference", `${path}.variantReference`),
    ),
    requiredRecord(slot, "overrides", `${path}.overrides`),
  );
}

function positiveInteger(value: number, path: string) {
  if (!Number.isInteger(value) || value < 1) {
    throw new Error(`${path} must be a positive integer`);
  }
  return value;
}

function positiveNumber(value: number, path: string) {
  if (!Number.isFinite(value) || value <= 0) {
    throw new Error(`${path} must be greater than zero`);
  }
  return value;
}

function listItemState(value: unknown): ListItemState {
  if (value === "normal" || value === "pressed" || value === "inactive") return value;
  throw new Error(`Unsupported List Item state ${String(value)}`);
}

function listItemVerticalAlignment(value: string): ListItemVerticalAlignment {
  if (value === "start" || value === "center" || value === "end") return value;
  throw new Error(`Unsupported List Item vertical alignment ${value}`);
}
