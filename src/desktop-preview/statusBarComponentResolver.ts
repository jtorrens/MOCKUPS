import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { asRecord, parseObject } from "./previewJsonHelpers.js";
import {
  optionalNumber,
  optionalString,
  requiredAlpha,
  requiredBoolean,
  requiredNumber,
  requiredString,
} from "./previewValueHelpers.js";
import type {
  StatusBarDesignContract,
  StatusBarItemContract,
  StatusBarZone,
} from "./statusBarComponentContract.js";

function itemValue(value: unknown) {
  if (
    typeof value === "string" ||
    typeof value === "number" ||
    typeof value === "boolean"
  ) {
    return value;
  }
  return "";
}

function statusBarItems(value: unknown): StatusBarItemContract[] {
  if (!Array.isArray(value)) {
    throw new Error("Missing status bar items");
  }

  return value.map((raw, index) => {
    const item = asRecord(raw);
    const zone = requiredString(item, "zone", `items.${index}.zone`);
    if (zone !== "off" && zone !== "left" && zone !== "right") {
      throw new Error(`Unsupported status bar zone ${zone}`);
    }

    return {
      id: requiredString(item, "id", `items.${index}.id`),
      label: optionalString(item, "label"),
      kind: requiredString(item, "kind", `items.${index}.kind`),
      value: itemValue(item.value),
      token: optionalString(item, "token"),
      zone,
      order: optionalNumber(item, "order", index * 10),
      charging: item.charging === undefined
        ? false
        : requiredBoolean(item, "charging", `items.${index}.charging`),
    };
  });
}

function sortedVisibleStatusBarItems(value: unknown): Record<
  StatusBarZone,
  StatusBarItemContract[]
> {
  const zones = ["left", "right"] as const;
  const byZone: Record<StatusBarZone, StatusBarItemContract[]> = {
    left: [],
    right: [],
  };
  for (const item of statusBarItems(value)) {
    if (item.zone === "off") continue;
    if (item.kind === "text" && !String(item.value).trim()) continue;
    byZone[item.zone].push(item);
  }

  for (const zone of zones) {
    byZone[zone].sort((left, right) => left.order - right.order);
  }

  return byZone;
}

export function resolveStatusBarComponent(
  payload: DesignPreviewPayload,
): StatusBarDesignContract {
  const config = parseObject(payload.configJson);
  const layout = asRecord(config.layout);
  return {
    id: "statusBar",
    fontFamilyId: "theme.system",
    foregroundColorToken: requiredString(
      config,
      "foregroundColorToken",
      "statusBar.foregroundColorToken",
    ),
    backgroundColorToken: requiredString(
      config,
      "backgroundColorToken",
      "statusBar.backgroundColorToken",
    ),
    backgroundAlpha: requiredAlpha(
      config,
      "backgroundAlpha",
      "statusBar.backgroundAlpha",
    ),
    layout: {
      height: requiredNumber(layout, "height", "statusBar.layout.height"),
      itemSize: requiredNumber(layout, "itemSize", "statusBar.layout.itemSize"),
      gapToken: requiredString(layout, "gap", "statusBar.layout.gap"),
      sidePaddingToken: requiredString(
        layout,
        "sidePadding",
        "statusBar.layout.sidePadding",
      ),
    },
    zones: sortedVisibleStatusBarItems(config.items),
  };
}
