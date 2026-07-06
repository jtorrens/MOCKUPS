import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type {
  NavigationBarDesignContract,
  NavigationBarItemContract,
  NavigationBarZone,
} from "./navigationBarComponentContract.js";
import { asRecord, parseObject } from "./previewJsonHelpers.js";
import {
  optionalNumber,
  optionalString,
  requiredBoolean,
  requiredNumber,
  requiredString,
} from "./previewValueHelpers.js";

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

function navigationBarItems(value: unknown): NavigationBarItemContract[] {
  if (!Array.isArray(value)) {
    throw new Error("Missing navigation bar items");
  }

  return value.map((raw, index) => {
    const item = asRecord(raw);
    const zone = requiredString(item, "zone", `items.${index}.zone`);
    if (zone !== "left" && zone !== "center" && zone !== "right") {
      throw new Error(`Unsupported navigation bar zone ${zone}`);
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

function sortedNavigationBarItems(value: unknown): Record<
  NavigationBarZone,
  NavigationBarItemContract[]
> {
  const zones = ["left", "center", "right"] as const;
  const byZone: Record<NavigationBarZone, NavigationBarItemContract[]> = {
    left: [],
    center: [],
    right: [],
  };
  for (const item of navigationBarItems(value)) {
    byZone[item.zone].push(item);
  }

  for (const zone of zones) {
    byZone[zone].sort((left, right) => left.order - right.order);
  }

  return byZone;
}

export function resolveNavigationBarComponent(
  payload: DesignPreviewPayload,
): NavigationBarDesignContract {
  const config = parseObject(payload.configJson);
  const layout = asRecord(config.layout);
  const gesture = asRecord(config.gesture);
  const type = requiredString(config, "type", "navigationBar.type");
  if (type !== "buttons" && type !== "gestureBar") {
    throw new Error(`Unsupported navigation bar type ${type}`);
  }

  return {
    id: "navigationBar",
    type,
    layout: {
      height: requiredNumber(layout, "height", "navigationBar.layout.height"),
      itemSize: requiredNumber(
        layout,
        "itemSize",
        "navigationBar.layout.itemSize",
      ),
      sidePadding: requiredNumber(
        layout,
        "sidePadding",
        "navigationBar.layout.sidePadding",
      ),
      strokeWidth: requiredNumber(
        layout,
        "strokeWidth",
        "navigationBar.layout.strokeWidth",
      ),
      cornerRadius: requiredNumber(
        layout,
        "cornerRadius",
        "navigationBar.layout.cornerRadius",
      ),
      filled: requiredBoolean(layout, "filled", "navigationBar.layout.filled"),
    },
    gesture: {
      width: requiredNumber(gesture, "width", "navigationBar.gesture.width"),
      height: requiredNumber(gesture, "height", "navigationBar.gesture.height"),
      cornerRadius: requiredNumber(
        gesture,
        "cornerRadius",
        "navigationBar.gesture.cornerRadius",
      ),
    },
    zones: sortedNavigationBarItems(config.items),
  };
}
