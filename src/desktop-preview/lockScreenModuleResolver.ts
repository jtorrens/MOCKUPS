import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { LockScreenModuleContract } from "./lockScreenModuleContract.js";
import {
  parseObject,
  requiredBoolean,
  requiredRecord,
  requiredString,
} from "./componentResolverCommon.js";

export function resolveLockScreenModuleFrame(
  payload: DesignPreviewPayload,
): LockScreenModuleContract {
  const config = parseObject(payload.configJson);
  const lockScreen = requiredRecord(config, "lockScreen", "module.lockScreen");
  const runtime = parseObject(payload.designPreviewJson);
  const statusBarSlot = requiredRecord(lockScreen, "statusBarSlot", "module.core.lockScreen.statusBarSlot");
  const navigationBarSlot = requiredRecord(lockScreen, "navigationBarSlot", "module.core.lockScreen.navigationBarSlot");
  const stackSlot = requiredRecord(lockScreen, "stackSlot", "module.core.lockScreen.stackSlot");
  const stackInputs = requiredRecord(lockScreen, "stackInputs", "module.core.lockScreen.stackInputs");
  return {
    id: "lockScreen",
    statusBarSlot: {
      variantReference: requiredString(statusBarSlot, "variantReference", "module.core.lockScreen.statusBarSlot.variantReference"),
      overrides: requiredRecord(statusBarSlot, "overrides", "module.core.lockScreen.statusBarSlot.overrides"),
    },
    navigationBarSlot: {
      variantReference: requiredString(navigationBarSlot, "variantReference", "module.core.lockScreen.navigationBarSlot.variantReference"),
      overrides: requiredRecord(navigationBarSlot, "overrides", "module.core.lockScreen.navigationBarSlot.overrides"),
    },
    stackSlot: {
      variantReference: requiredString(stackSlot, "variantReference", "module.core.lockScreen.stackSlot.variantReference"),
      overrides: requiredRecord(stackSlot, "overrides", "module.core.lockScreen.stackSlot.overrides"),
    },
    stackInputs,
    showStatusBar: requiredBoolean(
      runtime,
      "showStatusBar",
      "module.core.lockScreen.runtime.showStatusBar",
    ),
    showNavigationBar: requiredBoolean(
      runtime,
      "showNavigationBar",
      "module.core.lockScreen.runtime.showNavigationBar",
    ),
  };
}
