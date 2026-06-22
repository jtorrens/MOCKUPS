import type { DebugOptions, DebugPayload, DebugSelection } from "../api/client.js";
import { PreviewPanel } from "./PreviewPanel.js";

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
      (candidate) => candidate.shotId === shotId && candidate.moduleId === "core.chat",
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

  return (
    <aside className="right-preview-shell">
      <section className="panel preview-context">
        <div className="panel-heading">
          <div>
            <span className="eyebrow">Persistent output</span>
            <h2>Preview context</h2>
          </div>
          <span className={`status-dot ${busy ? "busy" : ""}`}>
            {busy ? "Resolving" : "Live"}
          </span>
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
            Screen instance
            <select
              data-testid="screen-select"
              value={selection.screenInstanceId}
              onChange={(event) => {
                const instance = screenInstances.find(
                  (candidate) => candidate.id === event.target.value,
                );
                if (instance) {
                  onSelectionChange({
                    ...selection,
                    screenInstanceId: instance.id,
                    frame: Math.max(
                      instance.startFrame,
                      Math.min(selection.frame, instance.endFrame - 1),
                    ),
                  });
                }
              }}
            >
              {screenInstances.map((instance) => (
                <option key={instance.id} value={instance.id}>
                  {instance.screenType} · {instance.moduleId ?? "legacy"}
                </option>
              ))}
            </select>
          </label>
          <label>
            Frame
            <div className="frame-inline">
              <input
                aria-label="Frame slider"
                type="range"
                min={0}
                max={Math.max(0, (selectedShot?.durationFrames ?? 1) - 1)}
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
                max={Math.max(0, (selectedShot?.durationFrames ?? 1) - 1)}
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
      </section>

      {error ? (
        <div className="alert error" role="alert">
          {error}
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
      />
    </aside>
  );
}
