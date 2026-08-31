import { embeddedComponentConfig } from "./componentPreviewDefaults.js";
import { parseObject } from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  applyRuntimeInputForwarding,
  forwardedRuntimeInputPatch,
} from "./runtimeInputForwarding.js";

export function resolvedTextInputBarRuntimeConfig(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  slot: Record<string, unknown>,
  text: string,
  availableWidth: number,
  ownerPath: string,
) {
  const config = embeddedComponentConfig(
    componentBaseConfigs,
    slot,
    "textInputBar",
    ownerPath,
  );
  const resolved = applyRuntimeInputForwarding({
    ...payload,
    kind: "componentClass",
    componentType: "textInputBar",
    configJson: JSON.stringify(config),
    designPreviewJson: JSON.stringify({
      ...forwardedRuntimeInputPatch(
        config,
        "forwarded.component.textInputBar.textBox.inputs.sampleText",
        text,
      ),
      availableWidth,
    }),
  });
  return parseObject(resolved.configJson);
}
