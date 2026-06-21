import { Composition } from "remotion";
import { ChatScreenPreview } from "./ChatScreenPreview.js";
import {
  CHAT_PREVIEW_DURATION_FRAMES,
  CHAT_PREVIEW_FPS,
  CHAT_PREVIEW_HEIGHT,
  CHAT_PREVIEW_WIDTH,
} from "./buildRenderableForFrame.js";

export const RemotionRoot = () => {
  return (
    <Composition
      id="ChatScreenPreview"
      component={ChatScreenPreview}
      durationInFrames={CHAT_PREVIEW_DURATION_FRAMES}
      fps={CHAT_PREVIEW_FPS}
      width={CHAT_PREVIEW_WIDTH}
      height={CHAT_PREVIEW_HEIGHT}
    />
  );
};
