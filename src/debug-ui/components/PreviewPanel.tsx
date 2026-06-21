import { RemotionRenderableAdapter } from "../../remotion/RemotionRenderableAdapter.js";
import type { RenderableNode } from "../../visual/renderable/types.js";

interface PreviewPanelProps {
  renderable: RenderableNode | null;
  frame: number;
}

export function PreviewPanel({ renderable, frame }: PreviewPanelProps) {
  const width = renderable?.box?.width ?? 1290;
  const height = renderable?.box?.height ?? 2796;
  const previewWidth = 322;
  const scale = previewWidth / width;

  return (
    <section className="panel preview-panel">
      <div className="panel-heading">
        <div>
          <span className="eyebrow">RenderableNode → Remotion adapter</span>
          <h2>Preview</h2>
        </div>
        <span className="frame-badge">Frame {frame}</span>
      </div>
      <div
        className="preview-viewport"
        style={{ height: Math.round(height * scale) }}
      >
        {renderable ? (
          <div
            data-testid="renderable-preview"
            className="preview-scale"
            style={{
              width,
              height,
              transform: `scale(${scale})`,
            }}
          >
            <RemotionRenderableAdapter tree={renderable} />
          </div>
        ) : (
          <div className="empty-state">
            No renderable output for this instance/frame.
          </div>
        )}
      </div>
    </section>
  );
}
