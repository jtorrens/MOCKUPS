import { useEffect, useRef, useState } from "react";
import type { RenderableNode } from "../../visual/renderable/types.js";
import { RenderSurface, renderSurfaceMetrics } from "./RenderSurface.js";
import { calculatePreviewFit, type PreviewFit } from "./previewSizing.js";

interface PreviewPanelProps {
  renderable: RenderableNode | null;
  frame: number;
  onFitChange?: (fit: PreviewFit) => void;
  showPhoneFrame: boolean;
}

export function PreviewPanel({
  renderable,
  frame,
  onFitChange,
  showPhoneFrame,
}: PreviewPanelProps) {
  const viewportHostRef = useRef<HTMLDivElement | null>(null);
  const [availableSize, setAvailableSize] = useState({
    width: 322,
    height: 698,
  });
  const metrics = renderSurfaceMetrics(renderable);
  const fit = calculatePreviewFit({
    availableWidth: availableSize.width,
    availableHeight: availableSize.height,
    renderWidth: metrics.width,
    renderHeight: metrics.height,
  });

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

  useEffect(() => {
    onFitChange?.(fit);
  }, [fit.height, fit.scale, fit.width, onFitChange]);

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
        <RenderSurface
          fit={fit}
          renderable={renderable}
          showFrame={showPhoneFrame}
        />
      </div>
    </section>
  );
}
