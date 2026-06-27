import type { DebugOptions, DebugPayload, DebugSelection } from "../api/client.js";
import type { PreviewFit } from "./previewSizing.js";
import {
  buildPreviewOptionsViewModel,
  choosePreviewScreenForShot,
  previewFrameForScreen,
} from "./previewSelection.js";

interface PreviewOptionsCardProps {
  onFrameToggle: (showFrame: boolean) => void;
  onOpenChange: (open: boolean) => void;
  onRenderPng: () => void;
  onSelectionChange: (selection: DebugSelection) => void;
  open: boolean;
  options: DebugOptions;
  payload: DebugPayload | null;
  previewFit: PreviewFit | null;
  renderBusy: boolean;
  selection: DebugSelection;
  showFrame: boolean;
}

export function PreviewOptionsCard({
  onFrameToggle,
  onOpenChange,
  onRenderPng,
  onSelectionChange,
  open,
  options,
  payload,
  previewFit,
  renderBusy,
  selection,
  showFrame,
}: PreviewOptionsCardProps) {
  const {
    episodes,
    previewContext,
    previewSummary,
    previewTitle,
    renderSizeText,
    screenInstances,
    selectedEpisodeId,
    selectedInstance,
    shots,
    zoomText,
  } = buildPreviewOptionsViewModel({
    options,
    payload,
    previewFit,
    selection,
    showFrame,
  });
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

  return (
    <details
      className="preview-options-card"
      open={open}
      onToggle={(event) => onOpenChange(event.currentTarget.open)}
    >
      <summary className="preview-options-summary">
        <span className="preview-options-icon ui-glyph" aria-hidden="true">
          ▣
        </span>
        <div>
          <h2>Preview</h2>
          <p>{previewSummary}</p>
        </div>
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
              checked={showFrame}
              onChange={(event) => onFrameToggle(event.target.checked)}
            />
            Frame
          </label>
          <button
            className="preview-render-button"
            type="button"
            disabled={renderBusy || !payload?.renderable}
            onClick={onRenderPng}
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
                    frame: previewFrameForScreen(instance, selection.frame),
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
                    frame: previewFrameForScreen(instance, selection.frame),
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
                    frame: previewFrameForScreen(instance, selection.frame),
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
        </div>
      </div>
    </details>
  );
}
