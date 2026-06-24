import { useState } from "react";
import type { DebugOptions, DebugPayload, DebugSelection } from "../api/client.js";
import { PreviewPanel } from "../preview/PreviewPanel.js";
import type { PreviewFit } from "../preview/previewSizing.js";
import { usePreviewFrameRender } from "../preview/usePreviewFrameRender.js";
import { PreviewOptionsCard } from "./PreviewOptionsCard.js";
import { PreviewOutputStack } from "./PreviewOutputStack.js";

interface AppPreviewPanelProps {
  options: DebugOptions;
  selection: DebugSelection;
  payload: DebugPayload | null;
  busy: boolean;
  onSelectionChange: (selection: DebugSelection) => void;
  error?: string;
}

export function AppPreviewPanel({
  options,
  selection,
  payload,
  busy,
  onSelectionChange,
  error,
}: AppPreviewPanelProps) {
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
