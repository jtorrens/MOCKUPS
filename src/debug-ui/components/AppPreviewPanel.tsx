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

      {error ? (
        <div className="preview-message-card error" role="alert">
          <strong>Preview error</strong>
          {error}
        </div>
      ) : null}
      {renderError ? (
        <div className="preview-message-card error" role="alert">
          <strong>PNG render error</strong>
          {renderError}
        </div>
      ) : null}
      {renderResult ? (
        <div className="preview-output-card">
          <div>
            <strong>PNG rendered</strong>
            <span>
              {renderResult.relativeFilePath ?? renderResult.filePath}
            </span>
          </div>
          <p>
            {renderResult.outputWidth}×{renderResult.outputHeight} · scale{" "}
            {renderResult.outputScale} ·{" "}
            {renderResult.includeFrame ? "with frame" : "no frame"}
          </p>
          <a href={renderResult.url} target="_blank" rel="noreferrer">
            Open PNG
          </a>
        </div>
      ) : null}
      {payload?.warnings.length ? (
        <div className="preview-message-card warning" data-testid="warnings">
          <strong>Preview warnings</strong>
          {payload.warnings.map((warning) => (
            <div key={warning}>{warning}</div>
          ))}
        </div>
      ) : null}

      <PreviewPanel
        renderable={payload?.renderable ?? null}
        frame={selection.frame}
        onFitChange={setPreviewFit}
        showPhoneFrame={showPhoneFrame}
      />
    </aside>
  );
}
