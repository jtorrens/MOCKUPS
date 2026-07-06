import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { asRecord, parseObject } from "./previewJsonHelpers.js";
import {
  optionalNumber,
  optionalString,
  requiredBoolean,
  requiredNumber,
  requiredString,
} from "./previewValueHelpers.js";

export interface SystemBarItemContract {
  id: string;
  label: string;
  kind: string;
  value: string | number | boolean;
  token: string;
  zone: string;
  order: number;
  charging: boolean;
}

export type StatusBarZone = "left" | "right";
export type NavigationBarZone = "left" | "center" | "right";

export interface StatusBarDesignContract {
  id: "statusBar";
  layout: {
    height: number;
    itemSize: number;
    gap: number;
    sidePadding: number;
  };
  zones: Record<StatusBarZone, SystemBarItemContract[]>;
}

export interface NavigationBarDesignContract {
  id: "navigationBar";
  type: "buttons" | "gestureBar";
  layout: {
    height: number;
    itemSize: number;
    sidePadding: number;
    strokeWidth: number;
    cornerRadius: number;
    filled: boolean;
  };
  gesture: {
    width: number;
    height: number;
    cornerRadius: number;
  };
  zones: Record<NavigationBarZone, SystemBarItemContract[]>;
}

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

function items(value: unknown): SystemBarItemContract[] {
  if (!Array.isArray(value)) {
    throw new Error("Missing system bar value items");
  }

  return value.map((raw, index) => {
    const item = asRecord(raw);
    return {
      id: requiredString(item, "id", `items.${index}.id`),
      label: optionalString(item, "label"),
      kind: requiredString(item, "kind", `items.${index}.kind`),
      value: itemValue(item.value),
      token: optionalString(item, "token"),
      zone: requiredString(item, "zone", `items.${index}.zone`),
      order: optionalNumber(item, "order", index * 10),
      charging: item.charging === undefined
        ? false
        : requiredBoolean(item, "charging", `items.${index}.charging`),
    };
  });
}

function sortedVisibleItems(
  value: unknown,
  zones: readonly string[],
  dropEmptyText: boolean,
): Record<string, SystemBarItemContract[]> {
  const visible = items(value)
    .filter((item) => zones.includes(item.zone))
    .filter((item) => !dropEmptyText || item.kind !== "text" || String(item.value).trim());
  const byZone = Object.fromEntries(zones.map((zone) => [zone, [] as SystemBarItemContract[]]));
  for (const item of visible) {
    byZone[item.zone]?.push(item);
  }

  for (const zone of zones) {
    byZone[zone]?.sort((left, right) => left.order - right.order);
  }

  return byZone;
}

export function resolveStatusBar(
  payload: DesignPreviewPayload,
): StatusBarDesignContract {
  const config = parseObject(payload.configJson);
  const layout = asRecord(config.layout);
  return {
    id: "statusBar",
    layout: {
      height: requiredNumber(layout, "height", "statusBar.layout.height"),
      itemSize: requiredNumber(layout, "itemSize", "statusBar.layout.itemSize"),
      gap: requiredNumber(layout, "gap", "statusBar.layout.gap"),
      sidePadding: requiredNumber(
        layout,
        "sidePadding",
        "statusBar.layout.sidePadding",
      ),
    },
    zones: sortedVisibleItems(config.items, ["left", "right"], true) as Record<
      StatusBarZone,
      SystemBarItemContract[]
    >,
  };
}

export function resolveNavigationBar(
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
    zones: sortedVisibleItems(config.items, ["left", "center", "right"], false) as Record<
      NavigationBarZone,
      SystemBarItemContract[]
    >,
  };
}
