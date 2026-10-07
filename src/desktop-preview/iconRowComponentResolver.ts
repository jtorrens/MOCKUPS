import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { prepareRuntimeValues, prepareComponentInputValues, projectRuntimeValues, prepareComponentConfiguration } from "./runtimePreviewDocumentContract.js";
import {
  parseObject,
  requiredRecord,
  requiredString,
} from "./componentResolverCommon.js";
import { resolveButtonComponentFromRecords } from "./buttonComponentResolver.js";
import { buttonRuntimeValueBindings } from "./buttonComponentContract.js";
import type { IconRowDesignContract } from "./iconRowComponentContract.js";
import { iconRowRuntimeValueBindings, iconRowButtonValueLinks, iconRowSharedSizeLinks } from "./iconRowComponentContract.js";
import { requiredObjectArray } from "./previewJsonHelpers.js";
import { projectRuntimeCollectionItemInputs } from "./runtimeCollectionProjection.js";

export function resolveIconRowComponent(payload: DesignPreviewPayload): IconRowDesignContract {
  const config = parseObject(payload.configJson);
  const iconRow = requiredRecord(config, "iconRow", "component.iconRow");
  return resolveIconRowComponentFromRecords(
    config,
    {
      ...parseObject(payload.designPreviewJson),
      gap: requiredString(iconRow, "gap", "component.iconRow.gap"),
      orientation: requiredString(iconRow, "orientation", "component.iconRow.orientation"),
    },
    parseObject(payload.componentBaseConfigsJson),
    "component.iconRow",
  );
}

export function resolveConfiguredIconRowComponentFromRecords(
  config: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  id: string,
): IconRowDesignContract {
  const iconRow = requiredRecord(config, "iconRow", "component.iconRow");
  return resolveIconRowComponentFromRecords(
    config,
    {
      buttonInputs: requiredObjectArray(iconRow, "items", "component.iconRow").map((item) => ({
        ...prepareComponentInputValues(
          componentBaseConfigs,
          requiredString(item, "buttonVariantReference", "component.iconRow.item"),
          prepareComponentConfiguration(
            componentBaseConfigs,
            "button",
            requiredString(item, "buttonVariantReference", "component.iconRow.item"),
            requiredRecord(item, "buttonOverrides", "component.iconRow.item"),
          ),
          buttonRuntimeValueBindings,
          projectRuntimeValues(item, iconRowButtonValueLinks),
        ),
        id: requiredString(item, "id", "component.iconRow.item"),
      })),
      gap: requiredString(iconRow, "gap", "component.iconRow.gap"),
      orientation: requiredString(iconRow, "orientation", "component.iconRow.orientation"),
    },
    componentBaseConfigs,
    id,
  );
}

export function resolveIconRowComponentFromRecords(
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  id: string,
): IconRowDesignContract {
  const inheritedSizes = projectRuntimeValues(inputs, iconRowSharedSizeLinks);
  inputs = prepareRuntimeValues(config, inputs, iconRowRuntimeValueBindings);
  const iconRow = requiredRecord(config, "iconRow", "component.iconRow");
  const orientation = requiredString(inputs, "orientation", "component.iconRow.orientation");
  if (orientation !== "horizontal" && orientation !== "vertical") {
    throw new Error(`Unsupported icon row orientation ${orientation}`);
  }
  const itemSizingMode = requiredString(inputs, "itemSizingMode", "component.iconRow.itemSizingMode");
  if (itemSizingMode !== "content" && itemSizingMode !== "fillParent") {
    throw new Error(`Unsupported icon row item sizing mode ${itemSizingMode}`);
  }
  const structuralItems = requiredObjectArray(inputs, "structuralItems", "component.iconRow input");
  const runtimeItems = requiredObjectArray(
    inputs,
    "buttonInputs",
    "component.iconRow input",
  );
  const runtimeById = exactRuntimeItems(structuralItems, runtimeItems);
  const sizeSource = requiredString(iconRow, "sizeSource", "component.iconRow.sizeSource");
  if (sizeSource !== "shared" && sizeSource !== "perButton") throw new Error(`Unsupported icon row size source ${sizeSource}`);
  const contextualSizes = sizeSource === "shared"
    ? projectRuntimeValues(inputs, iconRowSharedSizeLinks)
    : inheritedSizes;
  const items = structuralItems.map((item, index) => {
    const itemId = requiredString(item, "id", `component.iconRow.items[${index}].id`);
    const runtime = runtimeById.get(itemId)!;
    const buttonVariantReference = requiredString(item, "buttonVariantReference", `component.iconRow.items[${index}].buttonVariantReference`);
    const baseButtonConfig = prepareComponentConfiguration(componentBaseConfigs, "button", buttonVariantReference, requiredRecord(item, "buttonOverrides", `component.iconRow.items[${index}].buttonOverrides`));
    return {
      id: itemId,
      button: resolveButtonComponentFromRecords(
        baseButtonConfig,
        projectRuntimeCollectionItemInputs(runtime, contextualSizes),
        componentBaseConfigs,
        `${id}.${itemId}`,
      ),
    };
  });
  return {
    id,
    orientation,
    itemSizingMode,
    gapToken: requiredString(inputs, "gap", "component.iconRow.gap"),
    items,
  };
}

function exactRuntimeItems(
  structuralItems: Record<string, unknown>[],
  runtimeItems: Record<string, unknown>[],
) {
  const structuralIds = structuralItems.map((item, index) =>
    requiredString(item, "id", `component.iconRow.items[${index}].id`));
  const runtimeById = new Map<string, Record<string, unknown>>();
  for (const [index, item] of runtimeItems.entries()) {
    const id = requiredString(item, "id", `component.iconRow.buttonInputs[${index}].id`);
    if (runtimeById.has(id)) {
      throw new Error(`component.iconRow Button Runtime '${id}' is duplicated`);
    }
    runtimeById.set(id, item);
  }
  const missing = structuralIds.filter((id) => !runtimeById.has(id));
  const unknown = [...runtimeById.keys()].filter((id) => !structuralIds.includes(id));
  if (missing.length || unknown.length) {
    throw new Error(
      "component.iconRow Button Runtime values must match the Variant items exactly"
      + `${missing.length ? `; missing: ${missing.join(", ")}` : ""}`
      + `${unknown.length ? `; unknown: ${unknown.join(", ")}` : ""}`,
    );
  }
  return runtimeById;
}
