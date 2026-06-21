import { AbsoluteFill, useCurrentFrame } from "remotion";
import { buildRenderableForFrame } from "./buildRenderableForFrame.js";
import { RemotionRenderableAdapter } from "./RemotionRenderableAdapter.js";

export const ChatScreenPreview = () => {
  const frame = useCurrentFrame();
  const tree = buildRenderableForFrame(frame);

  return (
    <AbsoluteFill style={{ backgroundColor: "#111111" }}>
      <RemotionRenderableAdapter tree={tree} />
    </AbsoluteFill>
  );
};
