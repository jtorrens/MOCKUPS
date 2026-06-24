export interface PreviewFitInput {
  availableWidth: number;
  availableHeight: number;
  renderWidth: number;
  renderHeight: number;
  maxScale?: number;
}

export interface PreviewFit {
  height: number;
  scale: number;
  width: number;
}

function positiveOrFallback(value: number, fallback: number) {
  return Number.isFinite(value) && value > 0 ? value : fallback;
}

export function calculatePreviewFit({
  availableWidth,
  availableHeight,
  renderWidth,
  renderHeight,
  maxScale = 1,
}: PreviewFitInput): PreviewFit {
  const safeAvailableWidth = positiveOrFallback(availableWidth, 1);
  const safeAvailableHeight = positiveOrFallback(availableHeight, 1);
  const safeRenderWidth = positiveOrFallback(renderWidth, 1);
  const safeRenderHeight = positiveOrFallback(renderHeight, 1);
  const safeMaxScale = positiveOrFallback(maxScale, 1);
  const scale = Math.min(
    safeAvailableWidth / safeRenderWidth,
    safeAvailableHeight / safeRenderHeight,
    safeMaxScale,
  );

  return {
    height: Math.max(1, Math.round(safeRenderHeight * scale)),
    scale,
    width: Math.max(1, Math.round(safeRenderWidth * scale)),
  };
}
