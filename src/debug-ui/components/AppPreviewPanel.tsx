import { useState } from "react";
import {
  renderPreviewFrame,
  type DebugOptions,
  type DebugPayload,
  type DebugSelection,
  type RenderFrameResult,
} from "../api/client.js";
import { PreviewPanel } from "../preview/PreviewPanel.js";
import type { PreviewFit } from "../preview/previewSizing.js";
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
  const [renderBusy, setRenderBusy] = useState(false);
  const [renderResult, setRenderResult] = useState<RenderFrameResult | null>(null);
  const [renderError, setRenderError] = useState("");

  function renderFramePng() {
    setRenderBusy(true);
    setRenderError("");
    void renderPreviewFrame({
      ...selection,
      includeFrame: showPhoneFrame,
    })
      .then((result) => {
        const separator = result.url.includes("?") ? "&" : "?";
        setRenderResult({
          ...result,
          url: `${result.url}${separator}t=${Date.now()}`,
        });
      })
      .catch((error: Error) => {
        setRenderResult(null);
        setRenderError(error.message);
      })
      .finally(() => setRenderBusy(false));
  }

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
