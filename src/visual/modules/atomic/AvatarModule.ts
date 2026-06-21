import type { VisualModule } from "../types.js";

export interface AvatarModuleInput {
  id: string;
  uri: string;
  size: number;
  label?: string;
  frame: number;
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
      style: { shape: "circle", overflow: "clip" },
      asset: { type: "image", uri: input.uri },
      metadata: { label: input.label ?? null },
    };
  },
};
