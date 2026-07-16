import type { RenderableBox } from "../visual/renderable/types.js";
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

export function previewPayloadInBox(
  payload: DesignPreviewPayload,
  box: RenderableBox,
): DesignPreviewPayload {
  return {
    ...payload,
    rootPreviewFrame: payload.rootPreviewFrame ?? payload.previewFrame,
    previewFrame: {
      ...payload.previewFrame,
      screenX: box.x,
      screenY: box.y,
      screenWidth: box.width,
      screenHeight: box.height,
    },
  };
}
