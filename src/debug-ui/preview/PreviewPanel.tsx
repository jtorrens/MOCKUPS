import { useEffect, useRef, useState } from "react";
import { RenderableReactAdapter } from "../../visual/adapters/react/RenderableReactAdapter.js";
import type { RenderableNode } from "../../visual/renderable/types.js";
import { DeviceFrameOverlay } from "./DeviceFrameOverlay.js";
import { calculatePreviewFit } from "./previewSizing.js";

interface PreviewPanelProps {
  renderable: RenderableNode | null;
  frame: number;
  showPhoneFrame: boolean;
}

function numberValue(value: unknown): number | undefined {
  return typeof value === "number" && Number.isFinite(value)
    ? value
    : undefined;
}

export function PreviewPanel({
  renderable,
  frame,
  showPhoneFrame,
}: PreviewPanelProps) {
  const viewportHostRef = useRef<HTMLDivElement | null>(null);
  const [availableSize, setAvailableSize] = useState({
    width: 322,
    height: 698,
  });
  const width = renderable?.box?.width ?? 1290;
  const height = renderable?.box?.height ?? 2796;
  const cornerRadius =
    numberValue(renderable?.style?.cornerRadius) ??
    numberValue(renderable?.style?.borderRadius) ??
    0;
  const fit = calculatePreviewFit({
    availableWidth: availableSize.width,
    availableHeight: availableSize.height,
    renderWidth: width,
    renderHeight: height,
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
            width: fit.width,
            height: fit.height,
          }}
        >
          {renderable ? (
            <div
              data-testid="renderable-preview"
              className="preview-scale"
              style={{
                width,
                height,
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
            cornerRadius={cornerRadius}
            scale={fit.scale}
            visible={Boolean(renderable) && showPhoneFrame}
          />
        </div>
      </div>
    </section>
  );
}
