import { RenderableReactAdapter } from "../../visual/adapters/react/RenderableReactAdapter.js";
import type { RenderableNode } from "../../visual/renderable/types.js";
import { DeviceFrameOverlay } from "./DeviceFrameOverlay.js";
import type { PreviewFit } from "./previewSizing.js";

interface RenderSurfaceProps {
  fit: PreviewFit;
  renderable: RenderableNode | null;
  showFrame: boolean;
}

function numberValue(value: unknown): number | undefined {
  return typeof value === "number" && Number.isFinite(value)
    ? value
    : undefined;
}

export function renderSurfaceMetrics(renderable: RenderableNode | null) {
  const width = renderable?.box?.width ?? 1290;
  const height = renderable?.box?.height ?? 2796;
  const cornerRadius =
    numberValue(renderable?.style?.cornerRadius) ??
    numberValue(renderable?.style?.borderRadius) ??
    0;
  return { cornerRadius, height, width };
}

export function RenderSurface({
  fit,
  renderable,
  showFrame,
}: RenderSurfaceProps) {
  const metrics = renderSurfaceMetrics(renderable);

  return (
    <div
      className="preview-viewport"
      style={{
        width: fit.width,
        height: fit.height,
      }}
    >
      {renderable ? (
        <div
          data-testid="renderable-preview"
          className="preview-scale"
          style={{
            width: metrics.width,
            height: metrics.height,
            transform: `scale(${fit.scale})`,
          }}
        >
          <RenderableReactAdapter tree={renderable} />
        </div>
      ) : (
        <div className="empty-state">
          No renderable output for this instance/frame.
        </div>
      )}
      <DeviceFrameOverlay
        cornerRadius={metrics.cornerRadius}
        scale={fit.scale}
        visible={Boolean(renderable) && showFrame}
      />
    </div>
  );
}
