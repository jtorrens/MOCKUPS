import type { RenderableBox } from "../visual/renderable/types.js";
import {
  desktopPreviewComponents,
  isDesktopPreviewComponentClass,
} from "./desktopPreviewComponents.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { authoringVariantPayload } from "./previewAuthoringTarget.js";
import { prepareEmbeddedRuntimePayload, requirePreparedRuntimePayload, type PreparedRuntimePreviewPayload } from "./runtimePreviewDocumentContract.js";

export function embeddedComponentPayload(
  payload: DesignPreviewPayload,
  type: string,
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
): PreparedRuntimePreviewPayload {
  requirePreparedRuntimePayload(payload);
  return prepareEmbeddedRuntimePayload(payload, type, config, inputs);
}

export function embeddedVariantComponentPayload(
  payload: DesignPreviewPayload,
  type: string,
  variantReference: string,
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
): DesignPreviewPayload {
  if (!payload.authoringOwnerId) {
    return embeddedComponentPayload(payload, type, config, inputs);
  }
  if (!isDesktopPreviewComponentClass(type)) {
    throw new Error(`Unsupported embedded Component type '${type}'.`);
  }
  return embeddedComponentPayload(
    authoringVariantPayload(
      payload,
      variantReference,
      desktopPreviewComponents[type].recordClassId,
    ),
    type,
    config,
    inputs,
  );
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
