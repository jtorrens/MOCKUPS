import { useState } from "react";
import "./preview.css";
import { PreviewNavigationCard } from "./PreviewNavigationCard.js";
import { PreviewOptionsCard } from "./PreviewOptionsCard.js";
import { PreviewOutputStack } from "./PreviewOutputStack.js";
import { PreviewPanel } from "./PreviewPanel.js";
import type { PreviewFit } from "./previewSizing.js";
import type { RightPreviewShellProps } from "./types.js";
import { usePreviewFrameRender } from "./usePreviewFrameRender.js";

export function RightPreviewShell({
  options,
  selection,
  payload,
  onSelectionChange,
  error,
}: RightPreviewShellProps) {
  const [showPhoneFrame, setShowPhoneFrame] = useState(true);
  const [previewOptionsOpen, setPreviewOptionsOpen] = useState(true);
  const [previewFit, setPreviewFit] = useState<PreviewFit | null>(null);
  const { renderBusy, renderError, renderFramePng, renderResult } =
    usePreviewFrameRender(selection, showPhoneFrame);

  return (
    <aside className="right-preview-shell">
      <PreviewOptionsCard
        onFrameToggle={setShowPhoneFrame}
        onOpenChange={setPreviewOptionsOpen}
        onRenderPng={renderFramePng}
        onSelectionChange={onSelectionChange}
        open={previewOptionsOpen}
        options={options}
        payload={payload}
        previewFit={previewFit}
        renderBusy={renderBusy}
        selection={selection}
        showFrame={showPhoneFrame}
      />

      <PreviewNavigationCard
        onSelectionChange={onSelectionChange}
        options={options}
        selection={selection}
      />

      <PreviewOutputStack
        error={error}
        renderError={renderError}
        renderResult={renderResult}
        warnings={payload?.warnings}
      />

      <PreviewPanel
        renderable={payload?.renderable ?? null}
        onFitChange={setPreviewFit}
        showPhoneFrame={showPhoneFrame}
      />
    </aside>
  );
}
