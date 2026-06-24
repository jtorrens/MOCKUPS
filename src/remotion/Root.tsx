import { Composition } from "remotion";
import { ChatScreenPreview } from "./ChatScreenPreview.js";
import {
  CHAT_PREVIEW_DURATION_FRAMES,
  CHAT_PREVIEW_FPS,
  CHAT_PREVIEW_HEIGHT,
  CHAT_PREVIEW_WIDTH,
} from "./buildRenderableForFrame.js";
import { RenderableNodeSchema } from "../visual/renderable/schema.js";

function renderableDimensions(value: unknown) {
  const result = RenderableNodeSchema.safeParse(value);
  return {
    width: result.success
      ? Math.max(1, Math.round(result.data.box?.width ?? CHAT_PREVIEW_WIDTH))
      : CHAT_PREVIEW_WIDTH,
    height: result.success
      ? Math.max(1, Math.round(result.data.box?.height ?? CHAT_PREVIEW_HEIGHT))
      : CHAT_PREVIEW_HEIGHT,
  };
}

export const RemotionRoot = () => {
  return (
    <Composition
      id="ChatScreenPreview"
      component={ChatScreenPreview}
      durationInFrames={CHAT_PREVIEW_DURATION_FRAMES}
      fps={CHAT_PREVIEW_FPS}
      width={CHAT_PREVIEW_WIDTH}
      height={CHAT_PREVIEW_HEIGHT}
      calculateMetadata={({ props }) => renderableDimensions(props.renderable)}
    />
  );
};
