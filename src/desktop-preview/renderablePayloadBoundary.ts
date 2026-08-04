import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { parseObject } from "./previewJsonHelpers.js";
import { applyRuntimeInputForwarding } from "./runtimeInputForwarding.js";
import { requiredDeviceModuleTransparency } from "./deviceModuleTransparency.js";

const requiredObjectDocuments = [
  ["configJson", "component config"],
  ["designPreviewJson", "design Preview payload"],
  ["runtimeContractJson", "runtime contract"],
  ["componentBaseConfigsJson", "component base configs"],
  ["appConfigJson", "App config"],
  ["instanceJson", "instance envelope"],
  ["themeTokensJson", "Theme tokens"],
] as const satisfies readonly (readonly [keyof DesignPreviewPayload, string])[];

export function resolveRenderablePayload(
  payload: DesignPreviewPayload,
): DesignPreviewPayload {
  for (const [key, label] of requiredObjectDocuments) {
    parseObject(payload[key] as string | undefined, label);
  }
  if (payload.iconMappingJson !== undefined) {
    parseObject(payload.iconMappingJson, "icon mapping");
  }
  requiredDeviceModuleTransparency(payload.previewFrame.moduleTransparency);
  return applyRuntimeInputForwarding(payload);
}
