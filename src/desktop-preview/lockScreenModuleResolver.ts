import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { LockScreenModuleContract } from "./lockScreenModuleContract.js";
import {
  asRecord,
  parseObject,
  requiredString,
} from "./componentResolverCommon.js";

export function resolveLockScreenModuleFrame(
  payload: DesignPreviewPayload,
): LockScreenModuleContract {
  const lockScreen = asRecord(parseObject(payload.configJson).lockScreen);
  return {
    id: "lockScreen",
    statusBarVariant: requiredString(
      lockScreen,
      "statusBarVariant",
      "module.lockScreen.statusBarVariant",
    ),
    navigationBarVariant: requiredString(
      lockScreen,
      "navigationBarVariant",
      "module.lockScreen.navigationBarVariant",
    ),
  };
}
