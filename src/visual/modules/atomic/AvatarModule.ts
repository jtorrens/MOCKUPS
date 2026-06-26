import type { VisualModule } from "../types.js";

export interface AvatarModuleInput {
  id: string;
  uri: string;
  size: number;
  label?: string;
  frame: number;
  cornerRadius?: number;
  borderWidth?: number;
  borderColor?: string;
  shadow?: Record<string, unknown>;
  imageScale?: number;
  imageOffsetX?: number;
  imageOffsetY?: number;
  imageBaseSize?: number;
}

export const AvatarModule: VisualModule<AvatarModuleInput> = {
  type: "avatar",
  version: 1,
  render(input) {
    return {
      id: input.id,
      type: "avatar",
      role: "contact_avatar",
      frame: input.frame,
      box: { x: 0, y: 0, width: input.size, height: input.size },
      style: {
        shape: "circle",
        overflow: "clip",
        ...(input.cornerRadius !== undefined
          ? { borderRadius: input.cornerRadius }
          : {}),
        ...(input.borderWidth !== undefined
          ? { borderWidth: input.borderWidth }
          : {}),
        ...(input.borderColor ? { borderColor: input.borderColor } : {}),
        ...(input.shadow ? { shadow: input.shadow } : {}),
      },
      asset: { type: "image", uri: input.uri },
      metadata: {
        label: input.label ?? null,
        ...(input.imageScale !== undefined ? { imageScale: input.imageScale } : {}),
        ...(input.imageOffsetX !== undefined
          ? { imageOffsetX: input.imageOffsetX }
          : {}),
        ...(input.imageOffsetY !== undefined
          ? { imageOffsetY: input.imageOffsetY }
          : {}),
        ...(input.imageBaseSize !== undefined
          ? { imageBaseSize: input.imageBaseSize }
          : {}),
      },
    };
  },
};
