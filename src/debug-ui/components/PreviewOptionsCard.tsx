import type { DebugOptions, DebugPayload, DebugSelection } from "../api/client.js";
import type { PreviewFit } from "../preview/previewSizing.js";

interface PreviewOptionsCardProps {
  busy: boolean;
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

export function PreviewOptionsCard({
  busy,
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
    showFrame ? "frame on" : "frame off",
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

  return (
    <details
      className="preview-options-card"
      open={open}
      onToggle={(event) => onOpenChange(event.currentTarget.open)}
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
  );
}
