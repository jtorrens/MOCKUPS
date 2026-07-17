import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type {
  NavigationBarDesignContract,
  NavigationBarItemContract,
  NavigationBarItemZone,
  NavigationBarZone,
} from "./navigationBarComponentContract.js";
import { asRecord, parseObject } from "./previewJsonHelpers.js";
import {
  requiredAlpha,
  requiredBoolean,
  requiredNumber,
  requiredString,
} from "./previewValueHelpers.js";

function navigationBarItems(value: unknown): NavigationBarItemContract[] {
  if (!Array.isArray(value)) {
    throw new Error("Missing navigation bar items");
  }

  const ids = new Set<string>();
  return value.map((raw, index) => {
    const item = asRecord(raw);
    const itemPath = `items.${index}`;
    const id = requiredString(item, "id", `${itemPath}.id`);
    if (ids.has(id)) {
      throw new Error(`Duplicate navigation bar item id ${id}`);
    }
    ids.add(id);
    const kind = requiredString(item, "kind", `${itemPath}.kind`);
    if (
      kind !== "generatedBack" &&
      kind !== "generatedHome" &&
      kind !== "generatedRecents"
    ) {
      throw new Error(`Unsupported navigation bar item kind ${kind}`);
    }
    const zoneValue = requiredString(item, "zone", `items.${index}.zone`);
    if (zoneValue !== "off" && zoneValue !== "left" && zoneValue !== "center" && zoneValue !== "right") {
      throw new Error(`Unsupported navigation bar zone ${zoneValue}`);
    }
    const zone: NavigationBarItemZone = zoneValue;

    return {
      id,
      label: requiredString(item, "label", `${itemPath}.label`),
      kind,
      zone,
      order: requiredInteger(item, "order", `${itemPath}.order`),
    };
  });
}

function requiredInteger(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const number = requiredNumber(value, key, path);
  if (!Number.isInteger(number)) throw new Error(`Missing integer value ${path}`);
  return number;
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
    if (item.zone === "off") continue;
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
  if (requiredInteger(config, "schemaVersion", "navigationBar.schemaVersion") !== 1) {
    throw new Error("Unsupported navigation bar schemaVersion");
  }
  const layout = asRecord(config.layout);
  const gesture = asRecord(config.gesture);
  const type = requiredString(config, "type", "navigationBar.type");
  if (type !== "buttons" && type !== "gestureBar") {
    throw new Error(`Unsupported navigation bar type ${type}`);
  }

  return {
    id: "navigationBar",
    type,
    foregroundColorToken: requiredString(
      config,
      "foregroundColorToken",
      "navigationBar.foregroundColorToken",
    ),
    backgroundColorToken: requiredString(
      config,
      "backgroundColorToken",
      "navigationBar.backgroundColorToken",
    ),
    backgroundAlpha: requiredAlpha(
      config,
      "backgroundAlpha",
      "navigationBar.backgroundAlpha",
    ),
    layout: {
      height: requiredNumber(layout, "height", "navigationBar.layout.height"),
      itemSize: requiredNumber(
        layout,
        "itemSize",
        "navigationBar.layout.itemSize",
      ),
      sidePaddingToken: requiredString(
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
