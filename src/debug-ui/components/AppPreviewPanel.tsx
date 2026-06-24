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

interface AppPreviewPanelProps {
  options: DebugOptions;
  selection: DebugSelection;
  payload: DebugPayload | null;
  busy: boolean;
  onSelectionChange: (selection: DebugSelection) => void;
  error?: string;
}

function choosePreviewScreenForShot(
  options: DebugOptions,
  shotId: string | undefined,
) {
  return (
    options.screenInstances.find(
      (candidate) =>
        candidate.shotId === shotId && candidate.moduleId === "core.chat",
    ) ?? options.screenInstances.find((candidate) => candidate.shotId === shotId)
  );
}

function previewFrameForScreen(
  screen: NonNullable<ReturnType<typeof choosePreviewScreenForShot>>,
  fallbackFrame = 210,
) {
  return Math.max(
    screen.startFrame,
    Math.min(fallbackFrame, screen.endFrame - 1),
  );
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
  const episodes = options.episodes.filter(
    (episode) => episode.productionId === selection.productionId,
  );
  const selectedShot = options.shots.find((shot) => shot.id === selection.shotId);
  const selectedEpisodeId =
    selectedShot?.episodeId ?? episodes[0]?.id ?? "";
  const shots = options.shots.filter((shot) =>
    selectedEpisodeId
      ? shot.episodeId === selectedEpisodeId
      : shot.productionId === selection.productionId,
  );
  const screenInstances = options.screenInstances.filter(
    (instance) => instance.shotId === selection.shotId,
  );
  const selectedInstance = options.screenInstances.find(
    (instance) => instance.id === selection.screenInstanceId,
  );
  const previewTitle =
    selectedInstance?.moduleId?.replace(/^core\./, "") ??
    selectedInstance?.screenType ??
    "Preview";
  const previewContext = payload?.previewContext;
  const maxFrame = Math.max(0, (selectedShot?.durationFrames ?? 1) - 1);
  const renderSizeText = payload?.renderable
    ? `${payload.renderable.box?.width ?? 1290}×${payload.renderable.box?.height ?? 2796}`
    : "No render";
  const zoomText = previewFit ? `${previewFit.scale.toFixed(3)}×` : "—";
  const previewSummary = [
    previewTitle,
    previewContext?.deviceName,
    previewContext
      ? `${previewContext.themeName} · ${previewContext.themeMode}`
      : undefined,
    `f${selection.frame}`,
    `zoom ${zoomText}`,
    showPhoneFrame ? "frame on" : "frame off",
  ]
    .filter(Boolean)
    .join(" · ");

  function updateScreenInstance(screenInstanceId: string) {
    const instance = screenInstances.find(
      (candidate) => candidate.id === screenInstanceId,
    );
    if (!instance) return;
    onSelectionChange({
      ...selection,
      screenInstanceId: instance.id,
      frame: Math.max(
        instance.startFrame,
        Math.min(selection.frame, instance.endFrame - 1),
      ),
    });
  }

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
      <details
        className="preview-options-card"
        open={previewOptionsOpen}
        onToggle={(event) => setPreviewOptionsOpen(event.currentTarget.open)}
      >
        <summary className="preview-options-summary">
          <div>
            <h2>Preview</h2>
            <p>{previewSummary}</p>
          </div>
          <span className={`status-dot ${busy ? "busy" : ""}`}>
            {busy ? "Resolving" : "Live"}
          </span>
        </summary>
        <div className="preview-options-body">
          <div className="preview-header-meta" aria-label="Resolved preview context">
            <span title={selectedInstance?.moduleId}>
              Module: {previewTitle}
            </span>
            {previewContext ? (
              <>
                <span title={previewContext.deviceId}>
                  Device: {previewContext.deviceName}
                </span>
                <span title={previewContext.themeId}>
                  Theme: {previewContext.themeName} · {previewContext.themeMode}
                </span>
              </>
            ) : null}
            <span>Render: {renderSizeText}</span>
            <span>Zoom: {zoomText}</span>
          </div>

          <div className="preview-header-controls">
            <label className="preview-frame-toggle">
              <input
                type="checkbox"
                checked={showPhoneFrame}
                onChange={(event) => setShowPhoneFrame(event.target.checked)}
              />
              Frame
            </label>
            <button
              className="preview-render-button"
              type="button"
              disabled={renderBusy || !payload?.renderable}
              onClick={renderFramePng}
            >
              {renderBusy ? "Rendering…" : "Render PNG"}
            </button>
          </div>

          <div className="preview-select-grid">
            <label>
              Production
              <select
                data-testid="production-select"
                value={selection.productionId}
                onChange={(event) => {
                  const productionId = event.target.value;
                  const episode = options.episodes.find(
                    (candidate) => candidate.productionId === productionId,
                  );
                  const shot = options.shots.find(
                    (candidate) =>
                      candidate.episodeId === episode?.id ||
                      candidate.productionId === productionId,
                  );
                  const instance = choosePreviewScreenForShot(options, shot?.id);
                  if (shot && instance) {
                    onSelectionChange({
                      productionId,
                      shotId: shot.id,
                      screenInstanceId: instance.id,
                      frame: previewFrameForScreen(instance),
                    });
                  }
                }}
              >
                {options.productions.map((production) => (
                  <option key={production.id} value={production.id}>
                    {production.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Episode
              <select
                data-testid="episode-select"
                value={selectedEpisodeId}
                onChange={(event) => {
                  const episodeId = event.target.value;
                  const shot = options.shots.find(
                    (candidate) => candidate.episodeId === episodeId,
                  );
                  const instance = choosePreviewScreenForShot(options, shot?.id);
                  if (shot && instance) {
                    onSelectionChange({
                      productionId: shot.productionId,
                      shotId: shot.id,
                      screenInstanceId: instance.id,
                      frame: previewFrameForScreen(instance),
                    });
                  }
                }}
              >
                {episodes.map((episode) => (
                  <option key={episode.id} value={episode.id}>
                    {episode.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Shot
              <select
                data-testid="shot-select"
                value={selection.shotId}
                onChange={(event) => {
                  const shotId = event.target.value;
                  const instance = choosePreviewScreenForShot(options, shotId);
                  if (instance) {
                    onSelectionChange({
                      ...selection,
                      shotId,
                      screenInstanceId: instance.id,
                      frame: previewFrameForScreen(instance),
                    });
                  }
                }}
              >
                {shots.map((shot) => (
                  <option key={shot.id} value={shot.id}>
                    {shot.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Screen
              <select
                data-testid="screen-select"
                value={selection.screenInstanceId}
                onChange={(event) => updateScreenInstance(event.target.value)}
              >
                {screenInstances.map((instance) => (
                  <option key={instance.id} value={instance.id}>
                    {instance.moduleId?.replace(/^core\./, "") ?? instance.screenType}
                  </option>
                ))}
              </select>
            </label>
            <label className="preview-frame-field">
              Frame
              <div className="frame-inline">
                <input
                  aria-label="Frame slider"
                  type="range"
                  min={0}
                  max={maxFrame}
                  value={selection.frame}
                  onChange={(event) =>
                    onSelectionChange({
                      ...selection,
                      frame: Number(event.target.value),
                    })
                  }
                />
                <input
                  aria-label="Frame number"
                  data-testid="frame-input"
                  type="number"
                  min={0}
                  max={maxFrame}
                  value={selection.frame}
                  onChange={(event) =>
                    onSelectionChange({
                      ...selection,
                      frame: Number(event.target.value),
                    })
                  }
                />
              </div>
            </label>
          </div>
        </div>
      </details>

      {error ? (
        <div className="alert error" role="alert">
          {error}
        </div>
      ) : null}
      {renderError ? (
        <div className="alert error" role="alert">
          {renderError}
        </div>
      ) : null}
      {renderResult ? (
        <div className="alert success preview-render-result">
          <a href={renderResult.url} target="_blank" rel="noreferrer">
            Open PNG
          </a>
          <span>
            {renderResult.outputWidth}×{renderResult.outputHeight} · scale{" "}
            {renderResult.outputScale} ·{" "}
            {renderResult.includeFrame ? "with frame" : "no frame"}
          </span>
          <span title={renderResult.filePath}>
            {renderResult.relativeFilePath ?? renderResult.filePath}
          </span>
        </div>
      ) : null}
      {payload?.warnings.length ? (
        <div className="alert warning" data-testid="warnings">
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
