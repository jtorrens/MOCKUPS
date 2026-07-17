import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { asRecord, parseObject } from "./previewJsonHelpers.js";
import {
  requiredAlpha,
  requiredBoolean,
  requiredNumber,
  requiredPossiblyEmptyString,
  requiredString,
} from "./previewValueHelpers.js";
import type {
  StatusBarDesignContract,
  StatusBarItemContract,
  StatusBarItemZone,
  StatusBarZone,
} from "./statusBarComponentContract.js";

function statusBarItems(value: unknown): StatusBarItemContract[] {
  if (!Array.isArray(value)) {
    throw new Error("Missing status bar items");
  }

  const ids = new Set<string>();
  return value.map((raw, index) => {
    const item = asRecord(raw);
    const itemPath = `items.${index}`;
    const id = requiredString(item, "id", `${itemPath}.id`);
    if (ids.has(id)) {
      throw new Error(`Duplicate status bar item id ${id}`);
    }
    ids.add(id);
    const label = requiredString(item, "label", `${itemPath}.label`);
    const kind = requiredString(item, "kind", `${itemPath}.kind`);
    const zoneValue = requiredString(item, "zone", `items.${index}.zone`);
    if (zoneValue !== "off" && zoneValue !== "left" && zoneValue !== "right") {
      throw new Error(`Unsupported status bar zone ${zoneValue}`);
    }
    const zone: StatusBarItemZone = zoneValue;

    const order = requiredInteger(item, "order", `${itemPath}.order`);
    const base = { id, label, zone, order };
    if (kind === "text") {
      return {
        ...base,
        kind,
        value: requiredPossiblyEmptyString(item, "value", `${itemPath}.value`),
      };
    }
    if (kind === "iconToken") {
      return {
        ...base,
        kind,
        token: requiredString(item, "token", `${itemPath}.token`),
      };
    }
    if (kind === "generatedSignal") {
      return {
        ...base,
        kind,
        value: requiredIntegerInRange(item, "value", `${itemPath}.value`, 0, 4),
      };
    }
    if (kind === "generatedBattery") {
      return {
        ...base,
        kind,
        value: requiredIntegerInRange(item, "value", `${itemPath}.value`, 0, 100),
        charging: requiredBoolean(item, "charging", `${itemPath}.charging`),
      };
    }
    throw new Error(`Unsupported status bar item kind ${kind}`);
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

function requiredIntegerInRange(
  value: Record<string, unknown>,
  key: string,
  path: string,
  minimum: number,
  maximum: number,
) {
  const number = requiredInteger(value, key, path);
  if (number < minimum || number > maximum) {
    throw new Error(`Out-of-range integer value ${path}`);
  }
  return number;
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
  if (requiredInteger(config, "schemaVersion", "statusBar.schemaVersion") !== 2) {
    throw new Error("Unsupported status bar schemaVersion");
  }
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
