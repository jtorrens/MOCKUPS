import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { SystemCompositionModuleContract } from "./systemCompositionModuleContract.js";
import {
  parseObject,
  requiredBoolean,
  requiredRecord,
  requiredString,
} from "./componentResolverCommon.js";

export function resolveSystemCompositionModuleFrame(
  payload: DesignPreviewPayload,
): SystemCompositionModuleContract {
  const config = parseObject(payload.configJson);
  const composition = requiredRecord(config, "systemComposition", "module.systemComposition");
  const runtime = parseObject(payload.designPreviewJson);
  const statusBarSlot = requiredRecord(composition, "statusBarSlot", "module.system.composition.statusBarSlot");
  const navigationBarSlot = requiredRecord(composition, "navigationBarSlot", "module.system.composition.navigationBarSlot");
  const stackSlot = requiredRecord(composition, "stackSlot", "module.system.composition.stackSlot");
  const stackInputs = requiredRecord(composition, "stackInputs", "module.system.composition.stackInputs");
  return {
    id: "systemComposition",
    statusBarSlot: {
      variantReference: requiredString(statusBarSlot, "variantReference", "module.system.composition.statusBarSlot.variantReference"),
      overrides: requiredRecord(statusBarSlot, "overrides", "module.system.composition.statusBarSlot.overrides"),
    },
    navigationBarSlot: {
      variantReference: requiredString(navigationBarSlot, "variantReference", "module.system.composition.navigationBarSlot.variantReference"),
      overrides: requiredRecord(navigationBarSlot, "overrides", "module.system.composition.navigationBarSlot.overrides"),
    },
    stackSlot: {
      variantReference: requiredString(stackSlot, "variantReference", "module.system.composition.stackSlot.variantReference"),
      overrides: requiredRecord(stackSlot, "overrides", "module.system.composition.stackSlot.overrides"),
    },
    stackInputs,
    showStatusBar: requiredBoolean(
      runtime,
      "showStatusBar",
      "module.system.composition.runtime.showStatusBar",
    ),
    showNavigationBar: requiredBoolean(
      runtime,
      "showNavigationBar",
      "module.system.composition.runtime.showNavigationBar",
    ),
  };
}
