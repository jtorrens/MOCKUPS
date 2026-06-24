import { useState } from "react";
import type { DebugOptions, DebugPayload, DebugSelection } from "../api/client.js";
import { PreviewOptionsCard } from "./PreviewOptionsCard.js";
import { PreviewOutputStack } from "./PreviewOutputStack.js";
import { PreviewPanel } from "./PreviewPanel.js";
import type { PreviewFit } from "./previewSizing.js";
import { usePreviewFrameRender } from "./usePreviewFrameRender.js";

export interface RightPreviewShellProps {
  options: DebugOptions;
  selection: DebugSelection;
  payload: DebugPayload | null;
  busy: boolean;
  onSelectionChange: (selection: DebugSelection) => void;
  error?: string;
}

export function RightPreviewShell({
  options,
  selection,
  payload,
  busy,
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
        busy={busy}
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

      <PreviewOutputStack
        error={error}
        renderError={renderError}
        renderResult={renderResult}
        warnings={payload?.warnings}
      />

      <PreviewPanel
        renderable={payload?.renderable ?? null}
        frame={selection.frame}
        onFitChange={setPreviewFit}
        showPhoneFrame={showPhoneFrame}
      />
    </aside>
  );
}
