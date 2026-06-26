import { useEffect, useMemo, useRef, useState } from "react";
import type { DebugOptions, DebugSelection } from "../api/client.js";
import { previewFrameForScreen } from "./previewSelection.js";

interface PreviewNavigationCardProps {
  onSelectionChange: (selection: DebugSelection) => void;
  options: DebugOptions;
  selection: DebugSelection;
}

function clampFrame(frame: number, maxFrame: number) {
  return Math.max(0, Math.min(maxFrame, Math.floor(frame)));
}

export function PreviewNavigationCard({
  onSelectionChange,
  options,
  selection,
}: PreviewNavigationCardProps) {
  const [playing, setPlaying] = useState(false);
  const [achievedFps, setAchievedFps] = useState(0);
  const [open, setOpen] = useState(true);
  const latestSelectionRef = useRef(selection);
  const onSelectionChangeRef = useRef(onSelectionChange);
  const tickStatsRef = useRef({ count: 0, startedAt: performance.now() });

  const selectedShot = options.shots.find((shot) => shot.id === selection.shotId);
  const maxFrame = Math.max(0, (selectedShot?.durationFrames ?? 1) - 1);
  const targetFps = Math.max(1, Number(selectedShot?.fps ?? 25));
  const screenInstances = useMemo(
    () =>
      options.screenInstances
        .filter((instance) => instance.shotId === selection.shotId)
        .sort(
          (left, right) =>
            left.startFrame - right.startFrame ||
            left.layerOrder - right.layerOrder ||
            left.id.localeCompare(right.id),
        ),
    [options.screenInstances, selection.shotId],
  );
  const selectedInstance = screenInstances.find(
    (instance) => instance.id === selection.screenInstanceId,
  );
  const selectedIndex = screenInstances.findIndex(
    (instance) => instance.id === selection.screenInstanceId,
  );
  const relativeModuleFrame = selectedInstance
    ? Math.max(0, selection.frame - selectedInstance.startFrame)
    : 0;
  const screenStartFrame = selectedInstance?.startFrame ?? 0;
  const screenEndFrame = selectedInstance
    ? Math.max(selectedInstance.startFrame, selectedInstance.endFrame - 1)
    : maxFrame;
  const screenMaxRelativeFrame = Math.max(0, screenEndFrame - screenStartFrame);
  const sliderRelativeFrame = Math.max(
    0,
    Math.min(relativeModuleFrame, screenMaxRelativeFrame),
  );

  useEffect(() => {
    latestSelectionRef.current = selection;
    onSelectionChangeRef.current = onSelectionChange;
  }, [onSelectionChange, selection]);

  useEffect(() => {
    if (!playing) return undefined;
    tickStatsRef.current = { count: 0, startedAt: performance.now() };
    const interval = window.setInterval(() => {
      const current = latestSelectionRef.current;
      const nextFrame = Math.min(screenEndFrame, clampFrame(current.frame + 1, maxFrame));
      onSelectionChangeRef.current({ ...current, frame: nextFrame });
      if (nextFrame >= screenEndFrame) {
        setPlaying(false);
      }
      const stats = tickStatsRef.current;
      stats.count += 1;
      const elapsed = performance.now() - stats.startedAt;
      if (elapsed >= 500) {
        setAchievedFps((stats.count * 1000) / elapsed);
        tickStatsRef.current = { count: 0, startedAt: performance.now() };
      }
    }, Math.max(16, Math.round(1000 / targetFps)));
    return () => window.clearInterval(interval);
  }, [maxFrame, playing, screenEndFrame, targetFps]);

  function setFrame(frame: number) {
    onSelectionChange({
      ...selection,
      frame: clampFrame(frame, maxFrame),
    });
  }

  function setRelativeScreenFrame(frame: number) {
    setFrame(screenStartFrame + clampFrame(frame, screenMaxRelativeFrame));
  }

  function setFrameInsideScreen(frame: number) {
    setFrame(Math.max(screenStartFrame, Math.min(screenEndFrame, frame)));
  }

  function selectModule(offset: -1 | 1) {
    const nextInstance = screenInstances[selectedIndex + offset];
    if (!nextInstance) return;
    onSelectionChange({
      ...selection,
      screenInstanceId: nextInstance.id,
      frame: previewFrameForScreen(nextInstance, selection.frame),
    });
  }

  function goToPreviousScreenStart() {
    if (!selectedInstance) return;
    if (selection.frame > selectedInstance.startFrame) {
      onSelectionChange({
        ...selection,
        frame: selectedInstance.startFrame,
      });
      return;
    }
    selectModule(-1);
  }

  function goToNextScreenStart() {
    selectModule(1);
  }

  return (
    <details
      className="preview-navigation-card"
      open={open}
      aria-label="Preview navigation"
      onToggle={(event) => setOpen(event.currentTarget.open)}
    >
      <summary className="preview-navigation-header">
        <span className="preview-options-icon ui-glyph" aria-hidden="true">
          ▶
        </span>
        <div>
          <h2>Navigation</h2>
          <p>
            f{selection.frame} · rel {relativeModuleFrame} · {targetFps} fps
            target{playing ? ` · ${achievedFps.toFixed(1)} fps` : ""}
          </p>
        </div>
        <input
          aria-label="Frame navigation slider"
          className="preview-navigation-slider"
          type="range"
          min={0}
          max={screenMaxRelativeFrame}
          value={sliderRelativeFrame}
          onClick={(event) => event.stopPropagation()}
          onChange={(event) => setRelativeScreenFrame(Number(event.target.value))}
        />
      </summary>

      <div className="preview-navigation-body">
        <div className="preview-transport-controls">
          <button type="button" onClick={() => setFrame(0)} title="Start">
            ⏮
          </button>
          <button
            type="button"
            disabled={selection.frame <= screenStartFrame}
            onClick={() => setFrameInsideScreen(selection.frame - 1)}
            title="Previous frame"
          >
            ‹
          </button>
          <button
            type="button"
            disabled={
              !selectedInstance ||
              (selection.frame <= selectedInstance.startFrame && selectedIndex <= 0)
            }
            onClick={goToPreviousScreenStart}
            title="Current/previous screen start"
          >
            |&lt;
          </button>
          <button
            type="button"
            className="is-primary"
            onClick={() => setPlaying((value) => !value)}
            title={playing ? "Pause" : "Play"}
          >
            {playing ? "⏸" : "▶"}
          </button>
          <button
            type="button"
            disabled={selectedIndex < 0 || selectedIndex >= screenInstances.length - 1}
            onClick={goToNextScreenStart}
            title="Next screen start"
          >
            &gt;|
          </button>
          <button
            type="button"
            disabled={selection.frame >= screenEndFrame}
            onClick={() => setFrameInsideScreen(selection.frame + 1)}
            title="Next frame"
          >
            ›
          </button>
          <button type="button" onClick={() => setFrame(maxFrame)} title="End">
            ⏭
          </button>
        </div>

        <label className="preview-frame-field">
          Frame{" "}
          <span className="preview-frame-relative">
            screen {screenStartFrame}–{screenEndFrame}
          </span>
          <input
            aria-label="Frame number"
            data-testid="frame-input"
            type="number"
            min={0}
            max={maxFrame}
            value={selection.frame}
            onChange={(event) => setFrame(Number(event.target.value))}
          />
        </label>
      </div>
    </details>
  );
}
