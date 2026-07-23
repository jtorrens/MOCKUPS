import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { componentVariantConfig, mergeComponentDefaults } from "./componentPreviewDefaults.js";
import { parseObject, requiredRecord, requiredString } from "./componentResolverCommon.js";
import { resolveButtonComponentFromRecords } from "./buttonComponentResolver.js";
import type { IconRowDesignContract } from "./iconRowComponentContract.js";
import { requiredObjectArray } from "./previewJsonHelpers.js";

export function resolveIconRowComponent(payload: DesignPreviewPayload): IconRowDesignContract {
  return resolveConfiguredIconRowComponentFromRecords(
    parseObject(payload.configJson),
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
      items: requiredObjectArray(iconRow, "items", "component.iconRow"),
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
  const iconRow = requiredRecord(config, "iconRow", "component.iconRow");
  const orientation = requiredString(inputs, "orientation", "component.iconRow.input.orientation");
  if (orientation !== "horizontal" && orientation !== "vertical") {
    throw new Error(`Unsupported icon row orientation ${orientation}`);
  }
  const rawItems = requiredObjectArray(inputs, "items", "component.iconRow input");
  const sizeSource = requiredString(iconRow, "sizeSource", "component.iconRow.sizeSource");
  if (sizeSource !== "shared" && sizeSource !== "perButton") throw new Error(`Unsupported icon row size source ${sizeSource}`);
  const inheritedIconSize = typeof inputs.iconSizeToken === "string" ? inputs.iconSizeToken : "";
  const inheritedTextSize = typeof inputs.textSizeToken === "string" ? inputs.textSizeToken : "";
  const sharedIconSize = inheritedIconSize || requiredString(iconRow, "iconSizeToken", "component.iconRow.iconSizeToken");
  const sharedTextSize = inheritedTextSize || requiredString(iconRow, "textSizeToken", "component.iconRow.textSizeToken");
  const items = rawItems.map((item, index) => {
    const itemId = requiredString(item, "id", `component.iconRow.items[${index}].id`);
    const buttonVariantReference = requiredString(item, "buttonVariantReference", `component.iconRow.items[${index}].buttonVariantReference`);
    const buttonConfig = mergeComponentDefaults(
      componentVariantConfig(componentBaseConfigs, "button", buttonVariantReference),
      requiredRecord(item, "buttonOverrides", `component.iconRow.items[${index}].buttonOverrides`),
    );
    return {
      id: itemId,
      button: resolveButtonComponentFromRecords(
        buttonConfig,
        {
          contentMode: requiredString(item, "contentMode", `component.iconRow.items[${index}].contentMode`),
          state: requiredString(item, "state", `component.iconRow.items[${index}].state`),
          iconToken: requiredString(item, "iconToken", `component.iconRow.items[${index}].iconToken`),
          iconSizeToken: sizeSource === "perButton" && !inheritedIconSize
            ? requiredString(item, "iconSizeToken", `component.iconRow.items[${index}].iconSizeToken`)
            : sharedIconSize,
          textSizeToken: sizeSource === "perButton" && !inheritedTextSize
            ? requiredString(item, "textSizeToken", `component.iconRow.items[${index}].textSizeToken`)
            : sharedTextSize,
          sampleText: typeof item.text === "string" ? item.text : "",
          pushTrigger: item.pushTrigger === true,
          pushElapsedMs: typeof item.pushElapsedMs === "number" ? item.pushElapsedMs : 0,
          showBadge: false,
          badgeIconToken: "system_check",
          badgeText: "1",
        },
        componentBaseConfigs,
        `${id}.${itemId}`,
      ),
    };
  });
  return {
    id,
    orientation,
    gapToken: requiredString(inputs, "gap", "component.iconRow.input.gap"),
    items,
  };
}
