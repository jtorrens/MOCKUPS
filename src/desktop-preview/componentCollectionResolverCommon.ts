import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { componentPresetConfig, mergeComponentDefaults } from "./componentPreviewDefaults.js";
import { asRecord, parseObject, requiredNumber, requiredRecord, requiredString } from "./componentResolverCommon.js";
import type {
  ComponentCollectionAlignment,
  ComponentCollectionGapMode,
  ComponentCollectionItemContract,
} from "./componentCollectionContract.js";

export function resolveComponentCollectionItems(
  payload: DesignPreviewPayload,
  preview: Record<string, unknown>,
  ownerPath: string,
): ComponentCollectionItemContract[] {
  if (!Array.isArray(preview.items)) throw new Error(`Missing ${ownerPath} runtime items`);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const presetTypes = requiredRecord(bases, "presetTypes", "componentBaseConfigs.presetTypes");
  return preview.items.map((rawItem, index) => {
    const item = asRecord(rawItem);
    const itemPath = `${ownerPath}.items[${index}]`;
    const rawId = requiredString(item, "id", `${itemPath}.id`);
    const presetReference = requiredString(item, "presetId", `${itemPath}.presetId`);
    const componentType = presetTypes[presetReference];
    if (typeof componentType !== "string" || !componentType) {
      throw new Error(`Missing component type for ${ownerPath} Variant ${presetReference}`);
    }
    const alignment = requiredString(item, "alignment", `${itemPath}.alignment`);
    if (alignment !== "start" && alignment !== "center" && alignment !== "end") {
      throw new Error(`Unsupported ${ownerPath} alignment ${alignment}`);
    }
    const gapBeforeMode = requiredString(item, "gapBeforeMode", `${itemPath}.gapBeforeMode`);
    if (gapBeforeMode !== "fixed" && gapBeforeMode !== "reflow") {
      throw new Error(`Unsupported ${ownerPath} gap-before mode ${gapBeforeMode}`);
    }
    return {
      id: rawId,
      componentType,
      presetReference,
      config: mergeComponentDefaults(
        componentPresetConfig(bases, componentType, presetReference),
        asRecord(item.overrides),
      ),
      alignment: alignment as ComponentCollectionAlignment,
      gapBeforeMode: gapBeforeMode as ComponentCollectionGapMode,
      gapBeforeToken: requiredString(item, "gapBeforeToken", `${itemPath}.gapBeforeToken`),
      gapBeforeWeight: Math.max(0, requiredNumber(item, "gapBeforeWeight", `${itemPath}.gapBeforeWeight`)),
      inputs: requiredRecord(item, "inputs", `${itemPath}.inputs`),
    };
  });
}
