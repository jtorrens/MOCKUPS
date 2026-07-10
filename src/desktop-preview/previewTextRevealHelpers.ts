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
  const durationFrames = Math.max(1, Math.floor(plan.durationFrames));
  const frame = Math.max(0, Math.floor(plan.frame));
  if (frame >= durationFrames) return characters.length;

  const progress = Math.max(0, Math.min(1, (frame + 1) / durationFrames));
  return Math.floor(characters.length * progress);
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
