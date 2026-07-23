import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentVariantConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  parseObject,
  requiredNumber,
  requiredNumberPair,
  requiredPlacement,
  requiredRecord,
  requiredString,
} from "./componentResolverCommon.js";
import { resolveParameterAnimation } from "./parameterAnimationResolver.js";
import { optionalObject, requiredObjectArray } from "./previewJsonHelpers.js";
import type {
  ListItemDesignContract,
  ListItemElement,
  ListItemState,
} from "./listItemComponentContract.js";
import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import {
  literalLabelPreview,
  resolveLabelComponentFromRecords,
  staticLabelFrameContext,
} from "./labelComponentResolver.js";
import { resolveIconRowComponentFromRecords } from "./iconRowComponentResolver.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";

const elementTypes = new Set(["avatar", "label", "iconRow"]);
const contentSetKeys = new Set([
  "id",
  "name",
  "state",
  "actorId",
  "actor",
  "text",
  "subtext",
  "textColorToken",
  "subtextColorToken",
  "iconRowValues",
]);

export function resolveListItemComponent(
  payload: DesignPreviewPayload,
): ListItemDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const listItem = requiredRecord(config, "listItem", "component.listItem");
  const sizePair = requiredNumberPair(listItem, "size", "component.listItem.size");
  const size = { width: sizePair.first, height: sizePair.second };
  const rawElements = requiredObjectArray(
    listItem,
    "elements",
    "component.listItem",
  );
  validateElementModel(rawElements);
  const contentSets = requiredObjectArray(
    preview,
    "contentSets",
    "component.listItem runtime",
  );
  validateContentSets(contentSets);
  const baseSelectedSetId = requiredString(
    preview,
    "selectedSetId",
    "component.listItem.runtime.selectedSetId",
  );
  const animation = optionalObject(
    parseObject(payload.instanceJson),
    "animation",
    "Preview instance envelope",
  );
  const selectedSetId = resolveParameterAnimation(
    animation,
    "selectedSetId",
    "",
    payload.localFrame,
    baseSelectedSetId,
  ).value;
  if (typeof selectedSetId !== "string") {
    throw new Error("component.listItem selectedSetId animation must resolve a string");
  }
  const selectedSets = contentSets.filter((set) => set.id === selectedSetId);
  if (selectedSets.length !== 1) {
    throw new Error(
      `component.listItem selectedSetId '${selectedSetId}' must match exactly one content set`,
    );
  }
  const selectedSet = selectedSets[0]!;
  const baseState = listItemState(
    requiredString(selectedSet, "state", `component.listItem.contentSets.${selectedSetId}.state`),
  );
  const state = listItemState(resolveParameterAnimation(
    animation,
    "state",
    selectedSetId,
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

  return {
    id: "component.listItem",
    size,
    state,
    selectedSetId,
    surface: resolveSurfaceComponentAtSize(
      surfaceConfig,
      size,
      `component.listItem.${state}.surface`,
    ),
    elementsOpacity,
    elements: rawElements.map((element, index) => resolveElement(
      element,
      index,
      selectedSet,
      bases,
    )),
  };
}

function resolveElement(
  element: Record<string, unknown>,
  index: number,
  contentSet: Record<string, unknown>,
  bases: Record<string, unknown>,
): ListItemElement {
  const path = `component.listItem.elements[${index}]`;
  const id = requiredString(element, "id", `${path}.id`);
  const componentType = requiredString(element, "componentType", `${path}.componentType`);
  const sizePair = requiredNumberPair(element, "size", `${path}.size`);
  const size = { width: sizePair.first, height: sizePair.second };
  const placement = requiredPlacement(element, "placement", `${path}.placement`);
  const componentSlot = requiredRecord(element, "componentSlot", `${path}.componentSlot`);
  requireSlotType(bases, componentSlot, componentType, `${path}.componentSlot`);
  const childConfig = slotConfig(
    bases,
    componentSlot,
    componentType,
    `${path}.componentSlot`,
  );
  if (componentType === "avatar") {
    return {
      id,
      componentType,
      size,
      placement,
      component: resolveAvatarComponentFromRecords(
        childConfig,
        {
          actor: requiredRecord(contentSet, "actor", "component.listItem.runtime.actor"),
          sampleSubtext: "",
          showBadge: false,
          badgeContentMode: "text",
          badgeIconToken: "system_check",
          badgeText: "1",
          badgeSize: 20,
          badgeBackgroundPaletteColor: "blue",
          badgeContentPaletteColor: "gray_100",
        },
        bases,
        `component.listItem.${id}`,
      ),
    };
  }
  if (componentType === "label") {
    const textColorToken = requiredString(
      contentSet,
      "textColorToken",
      "component.listItem.runtime.textColorToken",
    );
    const subtextColorToken = requiredString(
      contentSet,
      "subtextColorToken",
      "component.listItem.runtime.subtextColorToken",
    );
    return {
      id,
      componentType,
      size,
      placement,
      textColorToken,
      subtextColorToken,
      component: resolveLabelComponentFromRecords(
        childConfig,
        literalLabelPreview(
          requiredString(contentSet, "text", "component.listItem.runtime.text"),
          requiredString(contentSet, "subtext", "component.listItem.runtime.subtext"),
        ),
        bases,
        `component.listItem.${id}`,
        staticLabelFrameContext,
      ),
    };
  }
  const iconRow = requiredRecord(
    childConfig,
    "iconRow",
    `component.listItem.${id}.iconRow`,
  );
  const structuralItems = requiredObjectArray(
    iconRow,
    "items",
    `component.listItem.${id}.iconRow`,
  );
  const runtimeValues = requiredObjectArray(
    contentSet,
    "iconRowValues",
    "component.listItem.runtime",
  );
  const valuesById = exactValuesById(structuralItems, runtimeValues);
  return {
    id,
    componentType: "iconRow",
    size,
    placement,
    component: resolveIconRowComponentFromRecords(
      childConfig,
      {
        items: structuralItems.map((item) => ({
          ...item,
          ...valuesById.get(requiredString(item, "id", `${path}.items.id`)),
        })),
        gap: requiredString(iconRow, "gap", `component.listItem.${id}.iconRow.gap`),
        orientation: requiredString(
          iconRow,
          "orientation",
          `component.listItem.${id}.iconRow.orientation`,
        ),
      },
      bases,
      `component.listItem.${id}`,
    ),
  };
}

function requireSlotType(
  bases: Record<string, unknown>,
  slot: Record<string, unknown>,
  expectedType: string,
  path: string,
) {
  const reference = requiredString(slot, "variantReference", `${path}.variantReference`);
  const variantTypes = requiredRecord(
    bases,
    "variantTypes",
    "componentBaseConfigs.variantTypes",
  );
  const actualType = variantTypes[reference];
  if (actualType !== expectedType) {
    throw new Error(
      `${path} Variant '${reference}' must resolve to Component '${expectedType}'`,
    );
  }
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

function exactValuesById(
  structuralItems: Record<string, unknown>[],
  runtimeValues: Record<string, unknown>[],
) {
  const values = new Map<string, Record<string, unknown>>();
  for (const [index, value] of runtimeValues.entries()) {
    const id = requiredString(value, "id", `component.listItem.runtime.iconRowValues[${index}].id`);
    if (values.has(id)) {
      throw new Error(`component.listItem runtime Icon Row value '${id}' is duplicated`);
    }
    values.set(id, value);
  }
  const structuralIds = structuralItems.map((item, index) =>
    requiredString(item, "id", `component.listItem Icon Row item[${index}].id`));
  const missing = structuralIds.filter((id) => !values.has(id));
  const unknown = [...values.keys()].filter((id) => !structuralIds.includes(id));
  if (missing.length || unknown.length) {
    throw new Error(
      `component.listItem Icon Row runtime values must match the Variant slots exactly`
      + `${missing.length ? `; missing: ${missing.join(", ")}` : ""}`
      + `${unknown.length ? `; unknown: ${unknown.join(", ")}` : ""}`,
    );
  }
  return values;
}

function validateElementModel(elements: Record<string, unknown>[]) {
  const ids = new Set<string>();
  const types = new Set<string>();
  for (const [index, element] of elements.entries()) {
    const path = `component.listItem.elements[${index}]`;
    const id = requiredString(element, "id", `${path}.id`);
    const componentType = requiredString(element, "componentType", `${path}.componentType`);
    if (!elementTypes.has(componentType)) {
      throw new Error(`${path}.componentType '${componentType}' is not supported`);
    }
    if (ids.has(id)) throw new Error(`component.listItem element id '${id}' is duplicated`);
    if (types.has(componentType)) {
      throw new Error(`component.listItem may contain at most one '${componentType}' element`);
    }
    ids.add(id);
    types.add(componentType);
  }
}

function validateContentSets(contentSets: Record<string, unknown>[]) {
  if (contentSets.length === 0) {
    throw new Error("component.listItem requires at least one Runtime content set");
  }
  const ids = new Set<string>();
  for (const [index, contentSet] of contentSets.entries()) {
    const path = `component.listItem.contentSets[${index}]`;
    const unknown = Object.keys(contentSet).filter((key) => !contentSetKeys.has(key));
    if (unknown.length) {
      throw new Error(`${path} contains undeclared fields: ${unknown.join(", ")}`);
    }
    const id = requiredString(contentSet, "id", `${path}.id`);
    requiredString(contentSet, "name", `${path}.name`);
    if (ids.has(id)) throw new Error(`component.listItem content set id '${id}' is duplicated`);
    ids.add(id);
  }
}

function listItemState(value: unknown): ListItemState {
  if (value === "normal" || value === "pressed" || value === "inactive") return value;
  throw new Error(`Unsupported List Item state ${String(value)}`);
}
