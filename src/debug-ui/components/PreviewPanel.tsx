import { useEffect, useRef, useState } from "react";
import { RemotionRenderableAdapter } from "../../remotion/RemotionRenderableAdapter.js";
import type { RenderableNode } from "../../visual/renderable/types.js";

interface PreviewPanelProps {
  renderable: RenderableNode | null;
  frame: number;
}

export function PreviewPanel({ renderable, frame }: PreviewPanelProps) {
  const viewportHostRef = useRef<HTMLDivElement | null>(null);
  const [availableSize, setAvailableSize] = useState({
    width: 322,
    height: 698,
  });
  const width = renderable?.box?.width ?? 1290;
  const height = renderable?.box?.height ?? 2796;
  const scale = Math.min(
    availableSize.width / width,
    availableSize.height / height,
    1,
  );
  const previewWidth = Math.max(1, Math.round(width * scale));
  const previewHeight = Math.max(1, Math.round(height * scale));

  useEffect(() => {
    const element = viewportHostRef.current;
    if (!element) return;
    const observer = new ResizeObserver(([entry]) => {
      setAvailableSize({
        width: Math.max(240, Math.floor(entry.contentRect.width)),
        height: Math.max(240, Math.floor(entry.contentRect.height)),
      });
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  return (
    <section className="panel preview-panel">
      <div className="panel-heading">
        <div>
          <span className="eyebrow">RenderableNode → Remotion adapter</span>
          <h2>Preview</h2>
        </div>
        <span className="frame-badge">Frame {frame}</span>
      </div>
      <div className="preview-viewport-host" ref={viewportHostRef}>
        <div
          className="preview-viewport"
          style={{
            width: previewWidth,
            height: previewHeight,
          }}
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
      </div>
    </section>
  );
}
