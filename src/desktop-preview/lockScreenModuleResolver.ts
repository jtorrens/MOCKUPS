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
  const statusBarSlot = requiredRecord(lockScreen, "statusBarSlot", "module.lockScreen.statusBarSlot");
  const navigationBarSlot = requiredRecord(lockScreen, "navigationBarSlot", "module.lockScreen.navigationBarSlot");
  const stackSlot = requiredRecord(lockScreen, "stackSlot", "module.lockScreen.stackSlot");
  const stackInputs = requiredRecord(lockScreen, "stackInputs", "module.lockScreen.stackInputs");
  return {
    id: "lockScreen",
    statusBarSlot: {
      variantReference: requiredString(statusBarSlot, "variantReference", "module.lockScreen.statusBarSlot.variantReference"),
      overrides: requiredRecord(statusBarSlot, "overrides", "module.lockScreen.statusBarSlot.overrides"),
    },
    navigationBarSlot: {
      variantReference: requiredString(navigationBarSlot, "variantReference", "module.lockScreen.navigationBarSlot.variantReference"),
      overrides: requiredRecord(navigationBarSlot, "overrides", "module.lockScreen.navigationBarSlot.overrides"),
    },
    stackSlot: {
      variantReference: requiredString(stackSlot, "variantReference", "module.lockScreen.stackSlot.variantReference"),
      overrides: requiredRecord(stackSlot, "overrides", "module.lockScreen.stackSlot.overrides"),
    },
    stackInputs,
    showStatusBar: requiredBoolean(
      runtime,
      "showStatusBar",
      "module.lockScreen.runtime.showStatusBar",
    ),
    showNavigationBar: requiredBoolean(
      runtime,
      "showNavigationBar",
      "module.lockScreen.runtime.showNavigationBar",
    ),
  };
}
