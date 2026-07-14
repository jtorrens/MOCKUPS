import type { DesignPreviewPayload } from "./designPreviewPayload.js";

export function embeddedComponentPayload(
  payload: DesignPreviewPayload,
  type: string,
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
): DesignPreviewPayload {
  return {
    ...payload,
    componentType: type,
    configJson: JSON.stringify(config),
    designPreviewJson: JSON.stringify(inputs),
  };
}
