import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { componentVariantConfig, mergeComponentDefaults } from "./componentPreviewDefaults.js";
import {
  parseObject,
  requiredNumber,
  requiredPossiblyEmptyString,
  requiredRecord,
  requiredString,
} from "./componentResolverCommon.js";
import { resolveButtonComponentFromRecords } from "./buttonComponentResolver.js";
import type { IconRowDesignContract } from "./iconRowComponentContract.js";
import { requiredObjectArray } from "./previewJsonHelpers.js";

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
      buttonInputs: iconRowButtonRuntimeDefaults(
        requiredObjectArray(iconRow, "items", "component.iconRow"),
      ),
      gap: requiredString(iconRow, "gap", "component.iconRow.gap"),
      orientation: requiredString(iconRow, "orientation", "component.iconRow.orientation"),
    },
    componentBaseConfigs,
    id,
  );
}

export function iconRowButtonRuntimeDefaults(
  items: Record<string, unknown>[],
) {
  return items.map((item, index) => ({
    id: requiredString(item, "id", `component.iconRow.items[${index}].id`),
    state: requiredString(item, "state", `component.iconRow.items[${index}].state`),
    sampleText: typeof item.sampleText === "string"
      ? item.sampleText
      : typeof item.text === "string" ? item.text : "",
    iconToken: requiredString(item, "iconToken", `component.iconRow.items[${index}].iconToken`),
    iconSizeToken: requiredString(item, "iconSizeToken", `component.iconRow.items[${index}].iconSizeToken`),
    textSizeToken: requiredString(item, "textSizeToken", `component.iconRow.items[${index}].textSizeToken`),
    pushTrigger: item.pushTrigger === true,
    pushElapsedMs: typeof item.pushElapsedMs === "number" ? item.pushElapsedMs : 0,
    iconColorToken: typeof item.iconColorToken === "string" ? item.iconColorToken : "theme.colors.icon",
    textColorToken: typeof item.textColorToken === "string" ? item.textColorToken : "theme.colors.textPrimary",
    showBadge: item.showBadge === true,
    badgeContentMode: typeof item.badgeContentMode === "string" ? item.badgeContentMode : "text",
    badgeIconToken: typeof item.badgeIconToken === "string" ? item.badgeIconToken : "system_check",
    badgeText: typeof item.badgeText === "string" ? item.badgeText : "1",
    badgeSize: typeof item.badgeSize === "number" ? item.badgeSize : 20,
    badgeBackgroundPaletteColor: typeof item.badgeBackgroundPaletteColor === "string"
      ? item.badgeBackgroundPaletteColor
      : "blue",
    badgeContentPaletteColor: typeof item.badgeContentPaletteColor === "string"
      ? item.badgeContentPaletteColor
      : "gray_100",
  }));
}

export function resolveIconRowComponentFromRecords(
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  id: string,
): IconRowDesignContract {
  const iconRow = requiredRecord(config, "iconRow", "component.iconRow");
  const orientation = typeof inputs.orientation === "string"
    ? inputs.orientation
    : requiredString(iconRow, "orientation", "component.iconRow.orientation");
  if (orientation !== "horizontal" && orientation !== "vertical") {
    throw new Error(`Unsupported icon row orientation ${orientation}`);
  }
  const structuralItems = Array.isArray(inputs.structuralItems)
    ? requiredObjectArray(inputs, "structuralItems", "component.iconRow input")
    : requiredObjectArray(iconRow, "items", "component.iconRow");
  const runtimeItems = requiredObjectArray(
    inputs,
    "buttonInputs",
    "component.iconRow input",
  );
  const runtimeById = exactRuntimeItems(structuralItems, runtimeItems);
  const sizeSource = requiredString(iconRow, "sizeSource", "component.iconRow.sizeSource");
  if (sizeSource !== "shared" && sizeSource !== "perButton") throw new Error(`Unsupported icon row size source ${sizeSource}`);
  const inheritedIconSize = typeof inputs.iconSizeToken === "string" ? inputs.iconSizeToken : "";
  const inheritedTextSize = typeof inputs.textSizeToken === "string" ? inputs.textSizeToken : "";
  const sharedIconSize = inheritedIconSize || requiredString(iconRow, "iconSizeToken", "component.iconRow.iconSizeToken");
  const sharedTextSize = inheritedTextSize || requiredString(iconRow, "textSizeToken", "component.iconRow.textSizeToken");
  const items = structuralItems.map((item, index) => {
    const itemId = requiredString(item, "id", `component.iconRow.items[${index}].id`);
    const runtime = runtimeById.get(itemId)!;
    const buttonVariantReference = requiredString(item, "buttonVariantReference", `component.iconRow.items[${index}].buttonVariantReference`);
    const baseButtonConfig = mergeComponentDefaults(
      componentVariantConfig(componentBaseConfigs, "button", buttonVariantReference),
      requiredRecord(item, "buttonOverrides", `component.iconRow.items[${index}].buttonOverrides`),
    );
    const state = requiredString(runtime, "state", `component.iconRow.buttonInputs[${index}].state`);
    const showBadge = runtime.showBadge === true;
    return {
      id: itemId,
      button: resolveButtonComponentFromRecords(
        baseButtonConfig,
        {
          state,
          iconToken: requiredString(runtime, "iconToken", `component.iconRow.buttonInputs[${index}].iconToken`),
          iconSizeToken: sizeSource === "perButton" && !inheritedIconSize
            ? requiredString(runtime, "iconSizeToken", `component.iconRow.buttonInputs[${index}].iconSizeToken`)
            : sharedIconSize,
          textSizeToken: sizeSource === "perButton" && !inheritedTextSize
            ? requiredString(runtime, "textSizeToken", `component.iconRow.buttonInputs[${index}].textSizeToken`)
            : sharedTextSize,
          sampleText: requiredPossiblyEmptyString(
            runtime,
            "sampleText",
            `component.iconRow.buttonInputs[${index}].sampleText`,
          ),
          pushTrigger: runtime.pushTrigger === true,
          pushElapsedMs: typeof runtime.pushElapsedMs === "number" ? runtime.pushElapsedMs : 0,
          showBadge,
          badgeContentMode: showBadge
            ? requiredString(runtime, "badgeContentMode", `component.iconRow.buttonInputs[${index}].badgeContentMode`)
            : "text",
          badgeIconToken: showBadge
            ? requiredString(runtime, "badgeIconToken", `component.iconRow.buttonInputs[${index}].badgeIconToken`)
            : "system_check",
          badgeText: showBadge
            ? requiredString(runtime, "badgeText", `component.iconRow.buttonInputs[${index}].badgeText`)
            : "1",
          badgeSize: showBadge
            ? requiredNumber(runtime, "badgeSize", `component.iconRow.buttonInputs[${index}].badgeSize`)
            : 20,
          badgeBackgroundPaletteColor: showBadge
            ? requiredString(runtime, "badgeBackgroundPaletteColor", `component.iconRow.buttonInputs[${index}].badgeBackgroundPaletteColor`)
            : "blue",
          badgeContentPaletteColor: showBadge
            ? requiredString(runtime, "badgeContentPaletteColor", `component.iconRow.buttonInputs[${index}].badgeContentPaletteColor`)
            : "gray_100",
        },
        componentBaseConfigs,
        `${id}.${itemId}`,
      ),
    };
  });
  return {
    id,
    orientation,
    gapToken: typeof inputs.gap === "string"
      ? inputs.gap
      : requiredString(iconRow, "gap", "component.iconRow.gap"),
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
