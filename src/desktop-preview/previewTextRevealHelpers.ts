export interface SimpleWriteOnFramePlan {
  enabled: boolean;
  frame: number;
  durationFrames: number;
}

export function simpleWriteOnFrameText(text: string, plan: SimpleWriteOnFramePlan) {
  if (!plan.enabled) return text;
  if (plan.durationFrames <= 0) return text;

  return textGraphemes(text)
    .slice(0, simpleWriteOnFrameVisibleCount(text, plan))
    .join("");
}

export function simpleWriteOnFrameVisibleCount(
  text: string,
  plan: SimpleWriteOnFramePlan,
) {
  const characters = textGraphemes(text);
  return writeOnFrameStepCount(
    characters.length,
    plan.frame,
    plan.durationFrames,
  );
}

export function writeOnFrameStepCount(
  stepCount: number,
  frame: number,
  durationFrames: number,
) {
  const total = Math.max(0, Math.floor(stepCount));
  if (total === 0) return 0;

  const duration = Math.max(1, Math.floor(durationFrames));
  const elapsed = Math.max(0, Math.floor(frame));
  if (elapsed === 0) return 0;
  if (elapsed >= duration || duration === 1) return total;
  if (total === 1) return 1;

  return 1 + Math.floor(((total - 1) * (elapsed - 1)) / (duration - 1));
}

export function writeOnFrameForVisibleCount(
  stepCount: number,
  visibleCount: number,
  durationFrames: number,
) {
  const total = Math.max(0, Math.floor(stepCount));
  const visible = Math.max(0, Math.min(total, Math.floor(visibleCount)));
  if (total === 0 || visible === 0) return 0;

  const duration = Math.max(1, Math.floor(durationFrames));
  if (visible >= total) return duration;
  if (total === 1 || duration === 1) return 1;
  return 1 + Math.ceil(((visible - 1) * (duration - 1)) / (total - 1));
}

export function simpleWriteOnFrameInProgress(
  text: string,
  plan: SimpleWriteOnFramePlan,
) {
  if (!plan.enabled || plan.durationFrames <= 0) return false;
  const total = textGraphemes(text).length;
  const visible = simpleWriteOnFrameVisibleCount(text, plan);
  return visible > 0 && visible < total;
}

export function textGraphemes(value: string) {
  const segmenterConstructor = (Intl as unknown as {
    Segmenter?: new (
      locale?: string,
      options?: { granularity?: "grapheme" },
    ) => { segment(input: string): Iterable<{ segment: string }> };
  }).Segmenter;

  if (segmenterConstructor) {
    return Array.from(
      new segmenterConstructor(undefined, { granularity: "grapheme" }).segment(value),
      (entry) => entry.segment,
    );
  }

  return Array.from(value);
}
