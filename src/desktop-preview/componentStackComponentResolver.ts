import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { componentPresetConfig, mergeComponentDefaults } from "./componentPreviewDefaults.js";
import { asRecord, parseObject, requiredNumber, requiredRecord, requiredString } from "./componentResolverCommon.js";
import type {
  ComponentStackAlignment,
  ComponentStackDesignContract,
  ComponentStackGapMode,
  ComponentStackSizingMode,
} from "./componentStackComponentContract.js";

export function resolveComponentStackComponent(payload: DesignPreviewPayload): ComponentStackDesignContract {
  const preview = parseObject(payload.designPreviewJson);
  const sizingMode = requiredString(preview, "sizingMode", "componentStack.runtime.sizingMode");
  if (sizingMode !== "fill" && sizingMode !== "content") {
    throw new Error(`Unsupported component stack sizing mode ${sizingMode}`);
  }
  if (!Array.isArray(preview.items)) throw new Error("Missing component stack runtime items");
  const bases = parseObject(payload.componentBaseConfigsJson);
  const presetTypes = requiredRecord(bases, "presetTypes", "componentBaseConfigs.presetTypes");
  return {
    id: "componentStack",
    sizingMode: sizingMode as ComponentStackSizingMode,
    startGapToken: requiredString(preview, "startGapToken", "componentStack.runtime.startGapToken"),
    endGapToken: requiredString(preview, "endGapToken", "componentStack.runtime.endGapToken"),
    items: preview.items.map((rawItem, index) => {
      const item = asRecord(rawItem);
      const rawId = requiredString(item, "id", `componentStack.items[${index}].id`);
      const presetReference = requiredString(item, "presetId", `componentStack.items[${index}].presetId`);
      const componentType = presetTypes[presetReference];
      if (typeof componentType !== "string" || !componentType) {
        throw new Error(`Missing component type for component stack Variant ${presetReference}`);
      }
      const alignment = requiredString(item, "alignment", `componentStack.items[${index}].alignment`);
      if (alignment !== "start" && alignment !== "center" && alignment !== "end") {
        throw new Error(`Unsupported component stack alignment ${alignment}`);
      }
      const gapBeforeMode = requiredString(item, "gapBeforeMode", `componentStack.items[${index}].gapBeforeMode`);
      if (gapBeforeMode !== "fixed" && gapBeforeMode !== "reflow") {
        throw new Error(`Unsupported component stack gap-before mode ${gapBeforeMode}`);
      }
      return {
        id: rawId,
        componentType,
        presetReference,
        config: mergeComponentDefaults(
          componentPresetConfig(bases, componentType, presetReference),
          asRecord(item.overrides),
        ),
        alignment: alignment as ComponentStackAlignment,
        gapBeforeMode: gapBeforeMode as ComponentStackGapMode,
        gapBeforeToken: requiredString(item, "gapBeforeToken", `componentStack.items[${index}].gapBeforeToken`),
        gapBeforeWeight: Math.max(0, requiredNumber(item, "gapBeforeWeight", `componentStack.items[${index}].gapBeforeWeight`)),
        inputs: requiredRecord(item, "inputs", `componentStack.items[${index}].inputs`),
      };
    }),
  };
}
