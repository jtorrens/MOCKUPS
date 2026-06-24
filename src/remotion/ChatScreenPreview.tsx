import { AbsoluteFill, useCurrentFrame } from "remotion";
import { RenderableReactAdapter } from "../visual/adapters/react/RenderableReactAdapter.js";
import { RenderableNodeSchema } from "../visual/renderable/schema.js";
import type { RenderableNode } from "../visual/renderable/types.js";
import { buildRenderableForFrame } from "./buildRenderableForFrame.js";

interface ChatScreenPreviewProps {
  includeFrame?: boolean;
  renderable?: RenderableNode;
}

function numberValue(value: unknown): number | undefined {
  return typeof value === "number" && Number.isFinite(value)
    ? value
    : undefined;
}

export const ChatScreenPreview = ({
  includeFrame = false,
  renderable,
}: ChatScreenPreviewProps) => {
  const frame = useCurrentFrame();
  const tree = renderable
    ? RenderableNodeSchema.parse(renderable)
    : buildRenderableForFrame(frame);
  const cornerRadius =
    numberValue(tree.style?.cornerRadius) ??
    numberValue(tree.style?.borderRadius) ??
    0;

  return (
    <AbsoluteFill style={{ backgroundColor: "transparent" }}>
      <RenderableReactAdapter tree={tree} />
      {includeFrame ? (
        <AbsoluteFill
          style={{
            border: "10px solid #111111",
            borderRadius: cornerRadius,
            boxShadow:
              "0 10px 28px rgba(17, 24, 39, 0.18), 0 2px 8px rgba(17, 24, 39, 0.1), inset 0 0 0 1px rgba(255, 255, 255, 0.28)",
            boxSizing: "border-box",
            pointerEvents: "none",
          }}
        />
      ) : null}
    </AbsoluteFill>
  );
};
