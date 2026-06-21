import type {
  DebugOptions,
  DebugSelection,
} from "../api/client.js";

interface SelectionPanelProps {
  options: DebugOptions;
  selection: DebugSelection;
  onChange: (selection: DebugSelection) => void;
  busy: boolean;
}

export function SelectionPanel({
  options,
  selection,
  onChange,
  busy,
}: SelectionPanelProps) {
  const shots = options.shots.filter(
    (shot) => shot.productionId === selection.productionId,
  );
  const instances = options.screenInstances.filter(
    (instance) => instance.shotId === selection.shotId,
  );
  const selectedShot = shots.find((shot) => shot.id === selection.shotId);

  return (
    <section className="panel selection-panel">
      <div className="panel-heading">
        <div>
          <span className="eyebrow">Source routing</span>
          <h2>Selection</h2>
        </div>
        <span className={`status-dot ${busy ? "busy" : ""}`}>
          {busy ? "Resolving" : "SQLite live"}
        </span>
      </div>
      <div className="selection-grid">
        <label>
          Production
          <select
            data-testid="production-select"
            value={selection.productionId}
            onChange={(event) => {
              const productionId = event.target.value;
              const shot = options.shots.find(
                (candidate) => candidate.productionId === productionId,
              );
              const instance = options.screenInstances.find(
                (candidate) => candidate.shotId === shot?.id,
              );
              if (shot && instance) {
                onChange({
                  productionId,
                  shotId: shot.id,
                  screenInstanceId: instance.id,
                  frame: instance.startFrame,
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
          Shot
          <select
            data-testid="shot-select"
            value={selection.shotId}
            onChange={(event) => {
              const shotId = event.target.value;
              const instance = options.screenInstances.find(
                (candidate) => candidate.shotId === shotId,
              );
              if (instance) {
                onChange({
                  ...selection,
                  shotId,
                  screenInstanceId: instance.id,
                  frame: instance.startFrame,
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
              const instance = instances.find(
                (candidate) => candidate.id === event.target.value,
              );
              if (instance) {
                onChange({
                  ...selection,
                  screenInstanceId: instance.id,
                  frame: instance.startFrame,
                });
              }
            }}
          >
            {instances.map((instance) => (
              <option key={instance.id} value={instance.id}>
                {instance.screenType} · {instance.moduleId ?? "legacy"}
              </option>
            ))}
          </select>
        </label>
        <label className="frame-control">
          Frame
          <div>
            <input
              aria-label="Frame slider"
              type="range"
              min={0}
              max={Math.max(0, (selectedShot?.durationFrames ?? 1) - 1)}
              value={selection.frame}
              onChange={(event) =>
                onChange({ ...selection, frame: Number(event.target.value) })
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
                onChange({ ...selection, frame: Number(event.target.value) })
              }
            />
          </div>
        </label>
      </div>
    </section>
  );
}
