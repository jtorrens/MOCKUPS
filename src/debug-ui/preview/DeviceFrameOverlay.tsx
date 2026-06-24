import type { CSSProperties } from "react";

interface DeviceFrameOverlayProps {
  scale: number;
  visible: boolean;
}

export function DeviceFrameOverlay({ scale, visible }: DeviceFrameOverlayProps) {
  if (!visible) return null;

  return (
    <div
      aria-hidden="true"
      className="preview-phone-frame"
      style={
        {
          "--preview-frame-border": `${Math.max(1, 10 * scale)}px`,
          "--preview-frame-radius": `${56 * scale}px`,
          "--preview-frame-shadow-y": `${10 * scale}px`,
          "--preview-frame-shadow-blur": `${28 * scale}px`,
        } as CSSProperties
      }
    />
  );
}
