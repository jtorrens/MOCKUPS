import type { JsonObject, ScreenInstance } from "../schemas/index.js";

export interface TimelineScreenLike {
  id: string;
  layer_order?: number | null;
  start_frame?: number | null;
  end_frame?: number | null;
  duration_frames?: number | null;
  transition_in_json?: JsonObject | null;
}

export interface ComputedScreenTimelineEntry<T extends TimelineScreenLike> {
  screen: T;
  order: number;
  startFrame: number;
  endFrame: number;
  durationFrames: number;
  transitionFrames: number;
  transitionType: string;
}

function numeric(value: unknown) {
  const numberValue = Number(value);
  return Number.isFinite(numberValue) ? numberValue : null;
}

export function screenDurationFrames(screen: TimelineScreenLike) {
  const explicitDuration = numeric(screen.duration_frames);
  if (explicitDuration !== null && explicitDuration > 0) {
    return Math.floor(explicitDuration);
  }
  const startFrame = numeric(screen.start_frame) ?? 0;
  const endFrame = numeric(screen.end_frame);
  if (endFrame !== null && endFrame > startFrame) {
    return Math.floor(endFrame - startFrame);
  }
  return 1;
}

export function transitionInFrames(screen: TimelineScreenLike) {
  const transition = screen.transition_in_json;
  if (!transition || typeof transition !== "object") {
    return 0;
  }
  const duration =
    numeric(transition.durationFrames) ?? numeric(transition.duration_frames);
  return Math.max(0, Math.floor(duration ?? 0));
}

export function transitionInType(screen: TimelineScreenLike) {
  const transition = screen.transition_in_json;
  if (!transition || typeof transition !== "object") {
    return "none";
  }
  const type = transition.type;
  return typeof type === "string" && type.trim() ? type : "none";
}

export function computeScreenTimeline<T extends TimelineScreenLike>(
  screens: T[],
): ComputedScreenTimelineEntry<T>[] {
  const sorted = [...screens].sort(
    (left, right) =>
      Number(left.layer_order ?? 0) - Number(right.layer_order ?? 0) ||
      left.id.localeCompare(right.id),
  );
  let cursor = 0;
  return sorted.map((screen, index) => {
    const durationFrames = screenDurationFrames(screen);
    const startFrame = cursor;
    const endFrame = startFrame + durationFrames;
    cursor = endFrame;
    return {
      screen,
      order: index,
      startFrame,
      endFrame,
      durationFrames,
      transitionFrames: transitionInFrames(screen),
      transitionType: transitionInType(screen),
    };
  });
}

export function computedShotDuration(screens: TimelineScreenLike[]) {
  const timeline = computeScreenTimeline(screens);
  const last = timeline.at(-1);
  return last ? last.endFrame : 0;
}

export function timelineScreenForFrame<T extends TimelineScreenLike>(
  entry: ComputedScreenTimelineEntry<T>,
  shotFrame: number,
) {
  const transitionStart = Math.max(0, entry.startFrame - entry.transitionFrames);
  return shotFrame >= transitionStart && shotFrame < entry.endFrame;
}

export function localFrameForTimelineEntry<T extends TimelineScreenLike>(
  entry: ComputedScreenTimelineEntry<T>,
  shotFrame: number,
) {
  return Math.max(0, shotFrame - entry.startFrame);
}

export function applyTimelineToScreenInstance(
  entry: ComputedScreenTimelineEntry<ScreenInstance>,
): ScreenInstance {
  return {
    ...entry.screen,
    start_frame: entry.startFrame,
    end_frame: entry.endFrame,
    layer_order: entry.order,
  };
}
