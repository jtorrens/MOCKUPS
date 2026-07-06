import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  boundedCenterBox,
  colorForMode,
  renderScale,
  variants,
} from "./componentRenderableCommon.js";
import type { CursorDesignContract } from "./cursorComponentContract.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";

export function cursorComponentToRenderable(
  payload: DesignPreviewPayload,
  cursor: CursorDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const box = boundedCenterBox(
    payload,
    Math.max(1, cursor.width * scale),
    Math.max(1, cursor.height * scale),
  );

  return cursorComponentToRenderableAt(payload, cursor, box);
}

export function cursorComponentToRenderableAt(
  payload: DesignPreviewPayload,
  cursor: CursorDesignContract,
  box: RenderableBox,
): RenderableNode {
  return {
    id: cursor.id,
    type: "surface",
    frame: 0,
    box,
    style: {
      background: colorForMode(
        payload,
        cursor.colorToken,
        payload.themeMode || "light",
      ),
      borderRadius: Math.max(0.5, box.width / 2),
      opacity: 1,
      colorModes: Object.fromEntries(
        variants(payload).map((mode) => [
          mode,
          {
            background: colorForMode(payload, cursor.colorToken, mode),
          },
        ]),
      ),
    },
  };
}
