import { resolveCollectionStackComponent } from "./collectionStackComponentResolver.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentVariantConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  parseObject,
  requiredNumber,
  requiredRecord,
  requiredString,
} from "./componentResolverCommon.js";
import { requiredObjectArray } from "./previewJsonHelpers.js";
import type { ListDesignContract } from "./listComponentContract.js";

const listItemKeys = new Set([
  "id",
  "present",
  "listItemInputs",
  "presenceTransition",
  "presenceElapsedMs",
]);

export function resolveListComponent(
  payload: DesignPreviewPayload,
): ListDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const list = requiredRecord(config, "list", "component.list");
  const stackSlot = requiredRecord(
    list,
    "collectionStackSlot",
    "component.list.collectionStackSlot",
  );
  const itemSlot = requiredRecord(
    list,
    "listItemSlot",
    "component.list.listItemSlot",
  );
  const stackConfig = mergeComponentDefaults(
    componentVariantConfig(
      bases,
      "collectionStack",
      requiredString(
        stackSlot,
        "variantReference",
        "component.list.collectionStackSlot.variantReference",
      ),
    ),
    requiredRecord(
      stackSlot,
      "overrides",
      "component.list.collectionStackSlot.overrides",
    ),
  );
  const itemVariantReference = requiredString(
    itemSlot,
    "variantReference",
    "component.list.listItemSlot.variantReference",
  );
  const itemOverrides = requiredRecord(
    itemSlot,
    "overrides",
    "component.list.listItemSlot.overrides",
  );
  const itemSize = {
    width: requiredNumber(preview, "itemWidth", "component.list.runtime.itemWidth"),
    height: requiredNumber(preview, "itemHeight", "component.list.runtime.itemHeight"),
  };
  if (itemSize.width <= 0 || itemSize.height <= 0) {
    throw new Error("component.list Runtime itemWidth and itemHeight must be greater than zero");
  }
  const items = requiredObjectArray(preview, "items", "component.list runtime")
    .map((item, index) => listStackItem(
      item,
      index,
      itemVariantReference,
      itemOverrides,
      list,
      itemSize,
    ));
  const stack = resolveCollectionStackComponent({
    ...payload,
    componentType: "collectionStack",
    configJson: JSON.stringify(stackConfig),
    designPreviewJson: JSON.stringify({
      distributionMode: "flow",
      sizingMode: requiredString(list, "sizingMode", "component.list.sizingMode"),
      startGapToken: requiredString(
        list,
        "startGapToken",
        "component.list.startGapToken",
      ),
      endGapToken: requiredString(list, "endGapToken", "component.list.endGapToken"),
      stackDirection: "down",
      stackOffsetToken: "theme.spacing.none",
      itemSizingMode: requiredString(
        list,
        "itemSizingMode",
        "component.list.itemSizingMode",
      ),
      scaleRatio: 1,
      opacityRatio: 1,
      items,
    }),
  });
  return { id: "component.list", stack };
}

function listStackItem(
  item: Record<string, unknown>,
  index: number,
  variantReference: string,
  overrides: Record<string, unknown>,
  list: Record<string, unknown>,
  itemSize: { width: number; height: number },
) {
  const path = `component.list.items[${index}]`;
  const unknown = Object.keys(item).filter((key) => !listItemKeys.has(key));
  if (unknown.length) {
    throw new Error(`${path} contains undeclared fields: ${unknown.join(", ")}`);
  }
  const id = requiredString(item, "id", `${path}.id`);
  const listItemInputs = requiredRecord(item, "listItemInputs", `${path}.listItemInputs`);
  return {
    id,
    variantReference,
    overrides,
    inputs: {
      ...listItemInputs,
      width: itemSize.width,
      height: itemSize.height,
      __runtimeFieldIds: {
        width: "width",
        height: "height",
        activeSet: "activeSet",
        state: "state",
      },
    },
    present: item.present,
    presenceMotion: requiredRecord(
      list,
      "itemPresenceMotion",
      "component.list.itemPresenceMotion",
    ),
    ...(item.presenceTransition === undefined
      ? {}
      : { presenceTransition: item.presenceTransition }),
    ...(item.presenceElapsedMs === undefined
      ? {}
      : { presenceElapsedMs: item.presenceElapsedMs }),
    alignment: requiredString(list, "itemAlignment", "component.list.itemAlignment"),
    gapBeforeMode: requiredString(
      list,
      "itemGapBeforeMode",
      "component.list.itemGapBeforeMode",
    ),
    gapBeforeToken: requiredString(
      list,
      "itemGapBeforeToken",
      "component.list.itemGapBeforeToken",
    ),
    gapBeforeWeight: requiredNumber(
      list,
      "itemGapBeforeWeight",
      "component.list.itemGapBeforeWeight",
    ),
  };
}
