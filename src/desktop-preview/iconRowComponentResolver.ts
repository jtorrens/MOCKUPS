import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { componentPresetConfig, mergeComponentDefaults } from "./componentPreviewDefaults.js";
import { asRecord, parseObject, requiredString } from "./componentResolverCommon.js";
import { resolveButtonComponentFromRecords } from "./buttonComponentResolver.js";
import type { IconRowDesignContract } from "./iconRowComponentContract.js";

export function resolveIconRowComponent(payload: DesignPreviewPayload): IconRowDesignContract {
  return resolveIconRowComponentFromRecords(
    parseObject(payload.configJson),
    parseObject(payload.designPreviewJson),
    parseObject(payload.componentBaseConfigsJson),
    "component.iconRow",
  );
}

export function resolveIconRowComponentFromRecords(
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  id: string,
): IconRowDesignContract {
  const iconRow = asRecord(config.iconRow);
  const orientation = requiredString(inputs, "orientation", "component.iconRow.input.orientation");
  if (orientation !== "horizontal" && orientation !== "vertical") {
    throw new Error(`Unsupported icon row orientation ${orientation}`);
  }
  const rawItems = inputs.items;
  if (!Array.isArray(rawItems)) {
    throw new Error("Missing icon row runtime collection component.iconRow.input.items");
  }
  const sizeSource = requiredString(iconRow, "sizeSource", "component.iconRow.sizeSource");
  if (sizeSource !== "shared" && sizeSource !== "perButton") throw new Error(`Unsupported icon row size source ${sizeSource}`);
  const inheritedIconSize = typeof inputs.iconSizeToken === "string" ? inputs.iconSizeToken : "";
  const inheritedTextSize = typeof inputs.textSizeToken === "string" ? inputs.textSizeToken : "";
  const sharedIconSize = inheritedIconSize || requiredString(iconRow, "iconSizeToken", "component.iconRow.iconSizeToken");
  const sharedTextSize = inheritedTextSize || requiredString(iconRow, "textSizeToken", "component.iconRow.textSizeToken");
  const items = rawItems.map((rawItem, index) => {
    const item = asRecord(rawItem);
    const itemId = requiredString(item, "id", `component.iconRow.items[${index}].id`);
    const buttonPresetId = requiredString(item, "buttonPresetId", `component.iconRow.items[${index}].buttonPresetId`);
    const buttonConfig = mergeComponentDefaults(
      componentPresetConfig(componentBaseConfigs, "button", buttonPresetId),
      asRecord(item.buttonOverrides),
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
